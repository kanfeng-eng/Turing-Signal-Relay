"""Validate a delivery on Ubuntu over pinned-host SSH. Passwords stay in memory."""
import argparse
import getpass
import hashlib
import json
import os
from pathlib import Path
import platform
import posixpath
import re
import shlex
import stat
import subprocess
import tarfile
import time
import urllib.request
import urllib.error
import uuid


def run_local(arguments, root, log, env=None):
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(arguments, cwd=root, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=240)
    if result.returncode:
        raise RuntimeError("Validation failed. Inspect " + str(log))


def validate_payload(args):
    root, runtime = args.payload_root.resolve(strict=True), Path(args.runtime).resolve(strict=True)
    assert platform.system() == "Linux" and platform.machine() == "x86_64"
    payload = root / "validation.tar.gz"
    assert hashlib.sha256(payload.read_bytes()).hexdigest() == args.payload_sha, "Payload digest mismatch."
    with tarfile.open(payload) as archive:
        archive.extractall(root, filter="data")
    judge = root / "judge"
    judge.mkdir()
    with tarfile.open(root / "judge.tar.gz") as archive:
        archive.extractall(judge, filter="data")
    player = judge / "TuringSignalRelay"
    player.chmod(player.stat().st_mode | stat.S_IXUSR)
    checks = root / "worker-checks/WorkerChecks"
    checks.chmod(checks.stat().st_mode | stat.S_IXUSR)
    env = dict(os.environ, DOTNET_ROOT=str(runtime.parent), TSR_DATA=str(root / 'platform-data'), TSR_REPOSITORY=str(root))
    run_local(["python3", str(root / "tools/check-bootstrap-headless.py"), "--player", str(player), "--output", str(root / "headless")], root, root / "headless.log")
    run_local([str(runtime), str(root / "worker-checks/WorkerChecks.dll"), str(root / "worker-results")], root, root / "worker-checks.log", env)
    run_local([str(runtime), str(root / "worker/Worker.dll"), str(player), str(root / "pair"), "60"], root, root / "pair.log", env)
    result = json.loads((root / "headless/default.result.json").read_text())
    assert result["stateHash"] == args.expected_hash, "Windows/Ubuntu final state mismatch."
    worker = json.loads((root / "worker-results/CHECKS.json").read_text())
    assert worker["status"] == "passed" and len(worker["cases"]) == 24
    pair = json.loads((root / "pair/runs.json").read_text())
    assert len(pair) == 2 and all(item["Succeeded"] and item["StateHash"] == args.expected_hash for item in pair)
    full_game = None
    if args.full_expected_hash:
        for name in ('courier', 'transporter'):
            run_local(['/usr/bin/g++', '-std=c++17', '-O2', '-pipe', '-static', '-I', str(root / 'sdk/cpp'),
                       '-I', str(root / 'sdk/cpp/examples'), str(root / 'sdk/cpp/examples' / (name + '.cpp')),
                       '-o', str(root / name)], root, root / (name + '-compile.log'))
        run_local(['python3', str(root / 'tools/check-relay-headless.py'), '--player', str(player),
                   '--repository', str(root), '--red', str(root / 'courier'), '--blue', str(root / 'transporter'),
                   '--output', str(root / 'full-game'), '--expected-hash', args.full_expected_hash],
                  root, root / 'full-game.log', env)
        full_game = json.loads((root / 'full-game/CHECKS.json').read_text())
    service = root / "platform"
    http_log = root / "platform.log"
    with http_log.open("w") as output:
        process = subprocess.Popen([str(runtime), str(service / "Platform.dll"), "--urls", "http://127.0.0.1:0", "--contentRoot", str(service)], cwd=service, env=env, stdout=output, stderr=subprocess.STDOUT)
    try:
        origin = None
        for _ in range(100):
            match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", http_log.read_text())
            if match:
                origin = match[1]
                break
            if process.poll() is not None:
                raise RuntimeError("Platform exited during startup.")
            time.sleep(.2)
        assert origin, "Platform startup timeout."
        http = {}
        for route in ["/", "/History", "/Judge", "/Results", "/css/site.css"]:
            with urllib.request.urlopen(origin + route, timeout=10) as response:
                body = response.read().decode("utf-8")
                assert response.status == 200
                if route.endswith('.css'):
                    assert '@media' in body
                else:
                    assert '<h1>' in body and '登录电波接力' in body and response.geturl().endswith('/Login')
                http[route] = {"status": response.status, "bytes": len(body.encode("utf-8"))}
        try:
            urllib.request.urlopen(origin + '/api/submissions', timeout=10)
        except urllib.error.HTTPError as error:
            assert error.code == 401
        else:
            raise AssertionError('Anonymous submissions API access was accepted')
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    report = {"status": "passed", "architecture": platform.machine(), "stateHash": result["stateHash"],
              "headlessCaseCount": len(json.loads((root / "headless/CHECKS.json").read_text())),
              "workerCaseCount": len(worker["cases"]), "pairSucceeded": 2, "platformHttp": http,
              "payloadSha256": args.payload_sha, "fullGame": full_game, "anonymousApiStatus": 401}
    (root / "UBUNTU.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("UBUNTU_PACKAGE_CHECKS_PASSED", flush=True)


def validate_ssh(args):
    import paramiko
    directory = args.delivery.resolve(strict=True)
    delivery = json.loads((directory / "DELIVERY.json").read_text(encoding="utf-8-sig"))
    assert delivery["status"] == "passed"
    if (directory / "UBUNTU.json").exists():
        raise FileExistsError("This delivery already has Ubuntu evidence.")
    linux = directory / "TuringSignalRelay-linux.tar.gz"
    expected_package = next(item for item in delivery["packages"] if item["name"] == linux.name)
    assert hashlib.sha256(linux.read_bytes()).hexdigest() == expected_package["sha256"]
    expected_hash = json.loads((directory / "windows-headless/default.result.json").read_text())["stateHash"]
    repository = Path(__file__).resolve().parent.parent
    payload = directory / ("ubuntu-payload-" + uuid.uuid4().hex + ".tar.gz")
    with tarfile.open(payload, "w:gz") as archive:
        archive.add(linux, arcname="judge.tar.gz")
        archive.add(directory / "server-linux/Worker.Checks", arcname="worker-checks")
        for name in ('Worker', 'Platform'):
            linux_service = directory / 'server-linux' / name
            archive.add(linux_service if linux_service.is_dir() else directory / 'server' / name, arcname=name.lower())
        for name in ["check-bootstrap-headless.py", "verify-bootstrap-artifacts.py"]:
            archive.add(repository / "tools" / name, arcname="tools/" + name)
        if 'windowsFullGame' in delivery:
            for name in ('fixtures', 'sdk/cpp'):
                archive.add(repository / name, arcname=name)
            archive.add(repository / 'tools/check-relay-headless.py', arcname='tools/check-relay-headless.py')
    digest = hashlib.sha256(payload.read_bytes()).hexdigest()
    client = paramiko.SSHClient()
    client.load_host_keys(str(args.known_hosts.resolve(strict=True)))
    client.set_missing_host_key_policy(paramiko.RejectPolicy())
    password = getpass.getpass("Ubuntu SSH password: ")
    try:
        client.connect(args.host, port=args.port, username=args.user, password=password, allow_agent=False, look_for_keys=False, timeout=15)
        password = None
        client.get_transport().set_keepalive(15)
        with client.open_sftp() as sftp:
            home = sftp.normalize(".").rstrip("/")
            base = sftp.normalize(args.remote_root).rstrip("/")
            if base != home and not base.startswith(home + "/"):
                raise ValueError("Validation directory must be under the SSH user's home.")
            runtime = sftp.normalize(args.runtime)
            if not stat.S_ISREG(sftp.stat(runtime).st_mode):
                raise ValueError("Runtime executable is missing.")
            remote = posixpath.join(base, "validation-" + uuid.uuid4().hex)
            sftp.mkdir(remote)
            print("UBUNTU_VALIDATION_DIRECTORY " + remote, flush=True)
            sftp.put(str(payload), remote + "/validation.tar.gz")
            sftp.put(str(Path(__file__).resolve()), remote + "/check-ubuntu-package.py")
        arguments = ["python3", remote + "/check-ubuntu-package.py", "--payload-root", remote, "--runtime", runtime,
                     "--expected-hash", expected_hash, "--payload-sha", digest]
        if 'windowsFullGame' in delivery:
            full = json.loads((directory / delivery['windowsFullGame']).read_text(encoding='utf-8-sig'))
            arguments += ['--full-expected-hash', full['stateHash']]
        _, stdout, stderr = client.exec_command(" ".join(shlex.quote(value) for value in arguments), timeout=300)
        text, errors = stdout.read().decode("utf-8", "replace"), stderr.read().decode("utf-8", "replace")
        if stdout.channel.recv_exit_status() != 0 or "UBUNTU_PACKAGE_CHECKS_PASSED" not in text:
            raise RuntimeError(text + errors)
        pending = directory / "UBUNTU.pending.json"
        with client.open_sftp() as sftp:
            sftp.get(remote + "/UBUNTU.json", str(pending))
            report = json.loads(pending.read_text(encoding="utf-8"))
            assert report["status"] == "passed" and report["stateHash"] == expected_hash and report["payloadSha256"] == digest
            for source, target in [("worker-results/CHECKS.json", "ubuntu-worker-checks.json"), ("headless/CHECKS.json", "ubuntu-headless-checks.json")]:
                sftp.get(remote + "/" + source, str(directory / target))
        pending.replace(directory / "UBUNTU.json")
        print("UBUNTU_PACKAGE_CHECKS_PASSED", flush=True)
    finally:
        password = None
        client.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--delivery", type=Path)
    parser.add_argument("--host")
    parser.add_argument("--user", default="ubuntu")
    parser.add_argument("--port", type=int, default=22)
    parser.add_argument("--known-hosts", type=Path)
    parser.add_argument("--remote-root")
    parser.add_argument("--runtime", required=True)
    parser.add_argument("--payload-root", type=Path, help=argparse.SUPPRESS)
    parser.add_argument("--expected-hash", help=argparse.SUPPRESS)
    parser.add_argument("--full-expected-hash", help=argparse.SUPPRESS)
    parser.add_argument("--payload-sha", help=argparse.SUPPRESS)
    args = parser.parse_args()
    if args.payload_root:
        validate_payload(args)
    else:
        if not all([args.delivery, args.host, args.known_hosts, args.remote_root]):
            parser.error("SSH validation requires --delivery, --host, --known-hosts and --remote-root.")
        validate_ssh(args)
