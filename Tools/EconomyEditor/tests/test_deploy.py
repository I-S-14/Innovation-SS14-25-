import importlib.util
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import sys
import threading
import unittest

spec = importlib.util.spec_from_file_location("editor_deploy", Path(__file__).parents[1] / "scripts/deploy.py")
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)


class FakeDocker:
    def __init__(self, settings, active=True):
        self.settings = settings
        self.containers = {}
        self.events = []
        self.volumes = set()
        self.labels = {module.REVISION_LABEL: settings.commit, module.CATALOG_LABEL: settings.catalog_version}
        if active:
            self.add(settings.name, "old-image")

    def add(self, name, image):
        self.containers[name] = {"Image": image, "Config": {"Labels": {module.CHANNEL_LABEL: self.settings.channel}}, "State": {"Running": True}}

    def available(self):
        pass

    def image(self, image):
        return {"Config": {"Labels": self.labels}}

    def inspect(self, name):
        return self.containers.get(name)

    def create_volume(self, name):
        self.volumes.add(name)

    def run(self, name, settings, candidate):
        self.events.append(("run", name, settings.volume, candidate))
        if name in self.containers:
            raise RuntimeError("Container already exists")
        self.add(name, settings.image)

    def remove(self, name):
        self.events.append(("remove", name))
        del self.containers[name]

    def stop(self, name):
        self.events.append(("stop", name))
        self.containers[name]["State"]["Running"] = False

    def start(self, name):
        self.events.append(("start", name))
        self.containers[name]["State"]["Running"] = True

    def rename(self, old, new):
        self.events.append(("rename", old, new))
        self.containers[new] = self.containers.pop(old)


class DeployTests(unittest.TestCase):
    def setUp(self):
        self.settings = module.Settings("editor:" + "a" * 40, "a" * 40, "b" * 16, token="test-only-secret")
        self.docker = FakeDocker(self.settings)

    def test_candidate_checked_before_old_container_is_stopped(self):
        checked = []

        def health(docker, name, settings):
            checked.append(name)
            if name.endswith("-candidate"):
                self.assertTrue(docker.inspect(settings.name)["State"]["Running"])

        module.deploy(self.settings, self.docker, health)
        self.assertEqual([self.settings.name + "-candidate", self.settings.name], checked)
        self.assertEqual(self.settings.image, self.docker.inspect(self.settings.name)["Image"])
        self.assertEqual("old-image", self.docker.inspect(self.settings.name + "-previous")["Image"])
        self.assertEqual({self.settings.volume}, self.docker.volumes)
        self.assertTrue(all(e[2] == self.settings.volume for e in self.docker.events if e[0] == "run"))

    def test_bad_candidate_keeps_existing_editor_running(self):
        def health(*_):
            raise RuntimeError("Unhealthy snapshot")
        with self.assertRaisesRegex(RuntimeError, "Unhealthy"):
            module.deploy(self.settings, self.docker, health)
        self.assertTrue(self.docker.inspect(self.settings.name)["State"]["Running"])
        self.assertEqual("old-image", self.docker.inspect(self.settings.name)["Image"])
        self.assertFalse(any(e[0] == "stop" for e in self.docker.events))
        self.assertNotIn(self.settings.name + "-candidate", self.docker.containers)

    def test_failure_after_cutover_restores_old_image_and_same_volume(self):
        def health(docker, name, settings):
            if not name.endswith("-candidate"):
                raise RuntimeError("Port health check failed")
        with self.assertRaisesRegex(RuntimeError, "Port health"):
            module.deploy(self.settings, self.docker, health)
        self.assertEqual("old-image", self.docker.inspect(self.settings.name)["Image"])
        self.assertTrue(self.docker.inspect(self.settings.name)["State"]["Running"])
        self.assertEqual({self.settings.volume}, self.docker.volumes)

    def test_cancellation_also_rolls_back(self):
        def health(docker, name, settings):
            if not name.endswith("-candidate"):
                raise KeyboardInterrupt()
        with self.assertRaises(KeyboardInterrupt):
            module.deploy(self.settings, self.docker, health)
        self.assertEqual("old-image", self.docker.inspect(self.settings.name)["Image"])
        self.assertTrue(self.docker.inspect(self.settings.name)["State"]["Running"])

    def test_cancellation_after_docker_renamed_old_container_rolls_back(self):
        rename = self.docker.rename

        def interrupted(old, new):
            rename(old, new)
            if new.endswith("-previous"):
                raise KeyboardInterrupt()
        self.docker.rename = interrupted
        with self.assertRaises(KeyboardInterrupt):
            module.deploy(self.settings, self.docker, lambda *_: None)
        self.assertEqual("old-image", self.docker.inspect(self.settings.name)["Image"])
        self.assertTrue(self.docker.inspect(self.settings.name)["State"]["Running"])

    def test_cancellation_after_stop_restarts_the_existing_container(self):
        stop = self.docker.stop

        def interrupted(name):
            stop(name)
            raise KeyboardInterrupt()
        self.docker.stop = interrupted
        with self.assertRaises(KeyboardInterrupt):
            module.deploy(self.settings, self.docker, lambda *_: None)
        self.assertEqual("old-image", self.docker.inspect(self.settings.name)["Image"])
        self.assertTrue(self.docker.inspect(self.settings.name)["State"]["Running"])

    def test_fresh_install_failure_does_not_leave_a_broken_container(self):
        docker = FakeDocker(self.settings, active=False)

        def health(docker, name, settings):
            if not name.endswith("-candidate"):
                raise RuntimeError("Failed")
        with self.assertRaises(RuntimeError):
            module.deploy(self.settings, docker, health)
        self.assertEqual({}, docker.containers)
        self.assertEqual({self.settings.volume}, docker.volumes)

    def test_wrong_commit_is_rejected_before_any_container_changes(self):
        self.docker.labels[module.REVISION_LABEL] = "c" * 40
        with self.assertRaisesRegex(ValueError, "does not match"):
            module.deploy(self.settings, self.docker)
        self.assertEqual([], self.docker.events)

    def test_unmanaged_container_is_never_replaced(self):
        self.docker.containers[self.settings.name]["Config"]["Labels"] = {}
        with self.assertRaisesRegex(ValueError, "unmanaged"):
            module.deploy(self.settings, self.docker)
        self.assertEqual([], self.docker.events)

    def test_interrupted_swap_is_recovered_before_next_deploy(self):
        self.docker.rename(self.settings.name, self.settings.name + "-previous")
        module.deploy(self.settings, self.docker, lambda *_: None)
        self.assertEqual(self.settings.image, self.docker.inspect(self.settings.name)["Image"])
        self.assertTrue(self.docker.inspect(self.settings.name)["State"]["Running"])

    def test_production_and_testing_have_separate_container_names_and_volumes(self):
        testing = module.Settings("editor:test", "a" * 40, "b" * 16, channel="testing", port=5188, token="test-only-secret")
        self.assertNotEqual(self.settings.name, testing.name)
        self.assertNotEqual(self.settings.volume, testing.volume)

    def test_missing_token_fails_preflight_and_token_is_not_in_repr(self):
        self.assertNotIn(self.settings.token, repr(self.settings))
        self.settings.token = ""
        with self.assertRaisesRegex(ValueError, "ECONOMY_EDITOR_TOKEN"):
            self.settings.validate(preflight=True)

    def test_docker_receives_token_in_environment_not_command_arguments(self):
        docker = module.Docker()
        recorded = []
        docker.call = lambda *args, **kwargs: recorded.append((args, kwargs))
        docker.run(self.settings.name, self.settings, candidate=False)
        args, kwargs = recorded[0]
        self.assertNotIn(self.settings.token, " ".join(args))
        self.assertEqual(self.settings.token, kwargs["env"]["EDITOR_TOKEN"])
        self.assertIn(f"type=volume,source={self.settings.volume},target=/data", args)

    def test_health_verifies_commit_and_snapshot_over_http(self):
        payload = {"status": "ok", "catalogCommit": self.settings.commit,
                   "catalogVersion": self.settings.catalog_version, "items": 100, "vendors": 22}

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                body = json.dumps(payload).encode()
                self.send_response(200)
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, *_):
                pass

        server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            self.docker.containers[self.settings.name]["NetworkSettings"] = {
                "Ports": {"8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": str(server.server_port)}]}}
            module.wait_for_health(self.docker, self.settings.name, self.settings)
            payload["catalogCommit"] = "c" * 40
            with self.assertRaisesRegex(RuntimeError, "different or invalid"):
                module.wait_for_health(self.docker, self.settings.name, self.settings)
        finally:
            server.shutdown()
            server.server_close()
            thread.join()


if __name__ == "__main__":
    unittest.main()
