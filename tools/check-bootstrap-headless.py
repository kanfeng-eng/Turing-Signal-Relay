"""Run real bootstrap players and verify success, determinism and failure handling."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def check(player, output):
    output.mkdir(parents=True)  # A new directory keeps previous evidence intact.
    results = {}
    startup = None
    if os.name == "nt":
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW

    def launch(name, options, expected):
        log = output / (name + ".log")
        process = subprocess.run([str(player), "-batchmode", "-nographics", "-logFile", str(log),
                                  *map(str, options)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                 timeout=90, startupinfo=startup)
        assert process.returncode == expected, (name, process.returncode, str(log))
        completed = "BOOTSTRAP_COMPLETED" in log.read_text(encoding="utf-8-sig")
        assert completed == (expected == 0), name
        results[name] = {"exitCode": process.returncode, "completionMarker": completed}
        print(f"PASS: {name}; exit {process.returncode}", flush=True)

    def verify(replay, result):
        subprocess.run([sys.executable, str(Path(__file__).with_name("verify-bootstrap-artifacts.py")),
                        "--replay", str(replay), "--result", str(result)], check=True)

    for name in ["default", "repeat"]:
        replay = output / (name + ".jsonl")
        launch(name, ["--bootstrap-replay", replay], 0)
        verify(replay, replay.with_suffix(".result.json"))
    first = json.loads((output / "default.result.json").read_text())
    second = json.loads((output / "repeat.result.json").read_text())
    assert first["stateHash"] == second["stateHash"] and first["replaySha256"] == second["replaySha256"]
    config = output / "config.json"
    config.write_text(json.dumps({"protocolVersion": 1, "ticksPerSecond": 10, "durationTicks": 3,
                                  "seed": 7, "rulesVersion": "bootstrap-provisional"}), encoding="utf-8")
    replay, result = output / "configured.jsonl", output / "nested/custom-result.json"
    launch("configured", ["--bootstrap-config", config, "--bootstrap-replay", replay, "--bootstrap-result", result], 0)
    verify(replay, result)
    assert json.loads(result.read_text())["finalSnapshot"]["tick"] == 3
    invalid = output / "invalid-config.json"
    invalid.write_text('{"durationTicks":0}', encoding="utf-8")
    for name, options in [
        ("invalid-config", ["--bootstrap-config", invalid, "--bootstrap-replay", output / "invalid.jsonl"]),
        ("missing-value", ["--bootstrap-replay"]),
        ("option-as-value", ["--bootstrap-replay", "-nographics"]),
        ("duplicate-option", ["--bootstrap-replay", output / "dup.jsonl", "--bootstrap-replay", output / "dup2.jsonl"]),
        ("unknown-option", ["--bootstrap-replay", output / "unknown.jsonl", "--bootstrap-typo", "x"]),
        ("missing-replay", ["--bootstrap-config", config]),
    ]:
        launch(name, options, 1)
    for name in ["invalid.jsonl", "dup.jsonl", "dup2.jsonl", "unknown.jsonl"]:
        assert not (output / name).exists(), name
    original = hashlib.sha256((output / "default.jsonl").read_bytes()).hexdigest()
    launch("refuse-overwrite", ["--bootstrap-replay", output / "default.jsonl"], 1)
    assert hashlib.sha256((output / "default.jsonl").read_bytes()).hexdigest() == original
    (output / "CHECKS.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print("PASS: real-player checks completed; repeated hashes identical, existing replay preserved.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New directory for test evidence")
    args = parser.parse_args()
    check(args.player.resolve(strict=True), args.output.resolve())
