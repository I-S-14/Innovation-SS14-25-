#!/usr/bin/env python3
"""Deploy on the current self-hosted runner's Docker daemon, retaining projects."""
from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import ipaddress
import json
import os
import re
import signal
import subprocess
import time
import urllib.request

CHANNEL_LABEL = "io.innovation.economy-editor.channel"
CATALOG_LABEL = "io.innovation.economy-editor.catalog-version"
REVISION_LABEL = "org.opencontainers.image.revision"


@dataclass
class Settings:
    image: str
    commit: str
    catalog_version: str
    channel: str = "production"
    bind: str = "127.0.0.1"
    port: int = 5187
    timeout: int = 90
    token: str = field(default="", repr=False)

    @property
    def name(self):
        return f"is14-economy-editor-{self.channel}"

    @property
    def volume(self):
        return self.name + "-projects"

    def validate(self, preflight=False):
        if not self.token.strip():
            raise ValueError("Set the GitHub secret ECONOMY_EDITOR_TOKEN before running Publish.")
        if not re.fullmatch(r"[a-z][a-z0-9-]{0,30}", self.channel):
            raise ValueError("Invalid deployment channel")
        if ipaddress.ip_address(self.bind).version != 4 or not 1 <= self.port <= 65535:
            raise ValueError("Deployment requires an IPv4 bind address and a valid port")
        if not 1 <= self.timeout <= 600:
            raise ValueError("Health timeout must be between 1 and 600 seconds")
        if not preflight and (not re.fullmatch(r"[a-z0-9./:_@-]+", self.image)
                              or not re.fullmatch(r"[0-9a-f]{40,64}", self.commit)
                              or not re.fullmatch(r"[0-9a-f]{16,64}", self.catalog_version)):
            raise ValueError("An image, exact game commit and catalog version are required")


class Docker:
    def call(self, *arguments, optional=False, env=None):
        result = subprocess.run(["docker", *arguments], text=True, capture_output=True, env=env, timeout=120)
        if result.returncode and not optional:
            raise RuntimeError(f"docker {arguments[0]} failed: {result.stderr.strip()}")
        return None if result.returncode else result.stdout.strip()

    def available(self):
        self.call("info", "--format", "{{.ServerVersion}}")

    def inspect(self, name):
        value = self.call("container", "inspect", name, optional=True)
        return json.loads(value)[0] if value else None

    def image(self, image):
        return json.loads(self.call("image", "inspect", image))[0]

    def create_volume(self, volume):
        self.call("volume", "create", volume)

    def run(self, name, settings, candidate):
        publish = "127.0.0.1::8080" if candidate else f"{settings.bind}:{settings.port}:8080"
        env = os.environ.copy()
        env["EDITOR_TOKEN"] = settings.token
        self.call("run", "--detach", "--name", name, "--restart", "no" if candidate else "unless-stopped",
                  "--label", f"{CHANNEL_LABEL}={settings.channel}", "--env", "EDITOR_TOKEN",
                  "--env", "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true",
                  "--mount", f"type=volume,source={settings.volume},target=/data",
                  "--publish", publish, settings.image, env=env)

    def remove(self, name):
        # Never pass --volumes: user projects live independently of containers.
        self.call("rm", "--force", name)

    def stop(self, name):
        self.call("stop", "--time", "20", name)

    def start(self, name):
        self.call("start", name)

    def rename(self, old, new):
        self.call("rename", old, new)


def wait_for_health(docker, name, settings):
    deadline = time.monotonic() + settings.timeout
    last_error = "container has not opened its port"
    http = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    while time.monotonic() < deadline:
        info = docker.inspect(name)
        if not info or not info["State"]["Running"]:
            raise RuntimeError(f"{name} exited before becoming healthy")
        bindings = info.get("NetworkSettings", {}).get("Ports", {}).get("8080/tcp")
        if bindings:
            binding = bindings[0]
            address = "127.0.0.1" if binding["HostIp"] == "0.0.0.0" else binding["HostIp"]
            try:
                with http.open(f"http://{address}:{binding['HostPort']}/health", timeout=3) as response:
                    health = json.load(response)
            except (OSError, ValueError) as error:
                last_error = str(error)
            else:
                if (health.get("status") != "ok" or health.get("catalogCommit") != settings.commit
                        or health.get("catalogVersion") != settings.catalog_version
                        or health.get("items", 0) <= 0 or health.get("vendors", 0) <= 0):
                    raise RuntimeError("Health endpoint returned a different or invalid game snapshot")
                return
        time.sleep(1)
    raise RuntimeError(f"Health check timed out for {name}: {last_error}")


def deploy(settings, docker=None, health=wait_for_health):
    settings.validate()
    docker = docker or Docker()
    docker.available()
    labels = docker.image(settings.image).get("Config", {}).get("Labels", {}) or {}
    if labels.get(REVISION_LABEL) != settings.commit or labels.get(CATALOG_LABEL) != settings.catalog_version:
        raise ValueError("Docker image does not match the game publish and fresh catalog")
    active, previous, candidate = settings.name, settings.name + "-previous", settings.name + "-candidate"
    for name in (active, previous, candidate):
        info = docker.inspect(name)
        if info and info.get("Config", {}).get("Labels", {}).get(CHANNEL_LABEL) != settings.channel:
            raise ValueError(f"Refusing to replace an unmanaged container: {name}")
    # Recover a deployment interrupted between renaming the old container and starting the new one.
    if not docker.inspect(active) and docker.inspect(previous):
        docker.rename(previous, active)
        docker.start(active)
    if docker.inspect(candidate):
        docker.remove(candidate)
    docker.create_volume(settings.volume)
    try:
        docker.run(candidate, settings, candidate=True)
        health(docker, candidate, settings)
    finally:
        if docker.inspect(candidate):
            docker.remove(candidate)
    # Do not disturb the working editor until the candidate has passed its health check.
    had_active = docker.inspect(active) is not None
    cutover_started = False
    try:
        if docker.inspect(previous):
            docker.remove(previous)
        cutover_started = True
        if had_active:
            docker.stop(active)
            docker.rename(active, previous)
        docker.run(active, settings, candidate=False)
        health(docker, active, settings)
    except BaseException:
        if cutover_started:
            # Inspect actual state: cancellation may arrive after Docker completed
            # a rename but before the CLI returned to this Python process.
            if had_active and docker.inspect(previous):
                if docker.inspect(active):
                    docker.remove(active)
                docker.rename(previous, active)
                docker.start(active)
            elif had_active and docker.inspect(active):
                docker.start(active)
            elif not had_active and docker.inspect(active):
                docker.remove(active)
        raise
    print(f"Deployed {settings.image}: {settings.name}, catalog {settings.catalog_version}, port {settings.port}.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--preflight", action="store_true")
    parser.add_argument("--image", default="")
    parser.add_argument("--commit", default="")
    parser.add_argument("--catalog-version", default="")
    parser.add_argument("--channel", default="production")
    parser.add_argument("--bind", default=os.getenv("ECONOMY_EDITOR_BIND_ADDRESS", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.getenv("ECONOMY_EDITOR_PORT", "5187")))
    parser.add_argument("--timeout", type=int, default=90)
    args = parser.parse_args()
    settings = Settings(args.image, args.commit, args.catalog_version, args.channel, args.bind, args.port,
                        args.timeout, os.getenv("ECONOMY_EDITOR_TOKEN", ""))
    settings.validate(preflight=args.preflight)
    if args.preflight:
        Docker().available()
        print(f"EconomyEditor deployment ready on this runner: {settings.name}, {settings.bind}:{settings.port}")
        return
    # Let an ordinary CI cancellation take the rollback path as well.
    def cancel(*_):
        raise KeyboardInterrupt()
    signal.signal(signal.SIGTERM, cancel)
    deploy(settings)


if __name__ == "__main__":
    main()
