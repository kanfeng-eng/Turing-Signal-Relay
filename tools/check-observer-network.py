"""Exercise Unity's production observer polling against a loopback HTTP fixture."""
import argparse
import gzip
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import threading
import time


def check(editor, project, log):
    root = Path(__file__).resolve().parent.parent
    if (root / "artifacts").resolve() not in project.parents:
        raise ValueError("Use an isolated project under artifacts, keeping the team project untouched.")
    if log.exists():
        raise FileExistsError("Use a new log file.")
    log.parent.mkdir(parents=True, exist_ok=True)
    with gzip.open(project / "Assets/Game/Resources/ObserverDemo.bytes", "rt", encoding="utf-8") as stream:
        frame = json.load(stream)["frames"][0]
    counts = {}
    authorization_errors = []

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_GET(self):
            route = self.path
            counts[route] = counts.get(route, 0) + 1
            expected = "observer-rotated-fixture" if route == "/fresh" else "observer-network-fixture"
            status, body, location = 200, None, None
            if self.headers.get("Authorization") != "Bearer " + expected:
                authorization_errors.append(route)
                status, body = 401, b"Unauthorized"
            elif route == "/sequence":
                item = [10, 10, 9, 11, "json-error", "http-error", 12][min(counts[route] - 1, 6)]
                if item == "json-error":
                    body = b"{broken-json"
                elif item == "http-error":
                    status, body = 503, b"Unavailable"
                else:
                    body = snapshot(item)
            elif route.startswith("/slow"):
                time.sleep(1)
                body = snapshot(30)
            elif route == "/fresh":
                body = snapshot(20)
            elif route == "/redirect":
                status, body, location = 302, b"", "/leak"
            elif route == "/unauthorized":
                status, body = 401, b"Unauthorized"
            elif route == "/timeout":
                time.sleep(7)
                body = snapshot(40)
            else:
                status, body = 404, b"Unknown fixture route"
            try:
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                if location:
                    self.send_header("Location", location)
                self.end_headers()
                self.wfile.write(body)
            except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                pass  # Expected when the client cancels a slow fixture request.

    def snapshot(tick):
        data = dict(frame, tick=tick)
        return json.dumps({"protocolVersion": 1, "tick": tick, "status": "running", "snapshot": data},
                          separators=(",", ":")).encode("utf-8")

    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    server.daemon_threads = True
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    arguments = [str(editor), "-batchmode", "-nographics", "-projectPath", str(project),
                 "-executeMethod", "TuringSignalRelay.Editor.ObserverNetworkChecks.Run",
                 "-observerTestEndpoint", "http://127.0.0.1:" + str(server.server_port),
                 "-logFile", str(log), "-bootstrapDevelopmentCopy"]
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    try:
        result = subprocess.run(arguments, timeout=180, startupinfo=startup,
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        text = log.read_text(encoding="utf-8-sig")
        assert result.returncode == 0, (result.returncode, str(log))
        assert "OBSERVER_NETWORK_CHECKS_PASSED" in text, str(log)
        assert "Exception:" not in text, str(log)
        assert counts.get("/sequence") == 7, counts
        assert counts.get("/fresh") == 1, counts
        assert counts.get("/leak", 0) == 0, "Observer followed a redirect."
        assert not authorization_errors, authorization_errors
        report = {"status": "passed", "requests": counts, "authorizationErrors": authorization_errors,
                  "redirectRequests": 0, "unityLog": log.name}
        log.with_suffix(".json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print("PASS: real observer HTTP sequence, cancellation, reconnect, redirect, authorization, timeout and replay checks.")
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, required=True)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--log", type=Path, required=True)
    args = parser.parse_args()
    check(args.editor.resolve(strict=True), args.project.resolve(strict=True), args.log.resolve())
