#!/usr/bin/env python3
"""Start the published editor with a disposable database and run HTTP tests."""
import argparse
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, required=True)
    args = parser.parse_args()
    app, catalog = args.app.resolve(), args.catalog.resolve()
    editor = Path(__file__).resolve().parents[1]
    expected = json.loads((catalog / "catalog.json").read_text(encoding="utf-8"))
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    url = f"http://127.0.0.1:{port}"
    token = secrets.token_urlsafe(32)
    with tempfile.TemporaryDirectory(prefix="economy-editor-http-") as temporary:
        env = os.environ.copy()
        env.update(EDITOR_TOKEN=token, EDITOR_TEST_TOKEN=token, EDITOR_URL=url)
        with open(Path(temporary) / "server.log", "w+", encoding="utf-8") as log:
            process = subprocess.Popen(["dotnet", str(app / "EconomyEditor.dll"), "--catalog", str(catalog),
                                        "--data", str(Path(temporary) / "projects"), "--urls", url],
                                       cwd=app, env=env, stdout=log, stderr=subprocess.STDOUT)
            try:
                deadline = time.monotonic() + 90
                while True:
                    if process.poll() is not None:
                        raise RuntimeError(f"Editor exited during startup ({process.returncode})")
                    try:
                        with urllib.request.urlopen(url + "/health", timeout=2) as response:
                            health = json.load(response)
                        break
                    except (OSError, ValueError):
                        if time.monotonic() >= deadline:
                            raise RuntimeError("Editor startup timed out")
                        time.sleep(0.5)
                if health.get("catalogVersion") != expected["version"] or health.get("catalogCommit") != expected["commit"]:
                    raise RuntimeError("Published editor did not load the expected snapshot")
                probe_env = env.copy()
                probe_env["EDITOR_HEALTHCHECK_URL"] = url + "/health"
                subprocess.run(["dotnet", str(app / "EconomyEditor.dll"), "--healthcheck"], env=probe_env, check=True, timeout=10)
                probe_env["EDITOR_HEALTHCHECK_URL"] = url + "/missing-health-endpoint"
                if subprocess.run(["dotnet", str(app / "EconomyEditor.dll"), "--healthcheck"], env=probe_env, timeout=10).returncode == 0:
                    raise RuntimeError("Health-check command incorrectly accepted a missing endpoint")
                subprocess.run(["python3" if os.name != "nt" else "python", "-m", "unittest", "discover", "-s",
                                str(editor / "tests"), "-p", "test_api.py", "-v"], env=env, check=True)
            except BaseException:
                log.flush()
                log.seek(0)
                print(log.read())
                raise
            finally:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
    print("Published editor HTTP checks passed with the fresh catalog and an isolated database.")


if __name__ == "__main__":
    main()
