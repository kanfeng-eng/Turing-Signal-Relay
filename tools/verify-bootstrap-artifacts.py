"""Independent bootstrap v2 replay/result verification; Python standard library only."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def state_hash(config, snapshot):
    rules = config["rulesVersion"].encode("utf-8")
    data = b"TSR_BOOTSTRAP_STATE_V1"
    data += struct.pack("<iiiii", config["protocolVersion"], config["ticksPerSecond"],
                        config["durationTicks"], config["seed"], len(rules))
    data += rules
    data += struct.pack("<i?ii", snapshot["tick"], snapshot["completed"], *snapshot["scoreHalfPoints"])
    return hashlib.sha256(data).hexdigest()


def verify(replay_path, result_path):
    with replay_path.open(encoding="utf-8-sig") as stream:
        header = json.loads(next(stream))
        assert header["type"] == "bootstrap-replay" and header["replayVersion"] == 2, "Expected bootstrap replay v2"
        config = header["config"]
        assert config["protocolVersion"] == 1 and config["ticksPerSecond"] > 0 and config["durationTicks"] > 0
        assert isinstance(config["rulesVersion"], str) and config["rulesVersion"].strip()
        last = None
        frames = 0
        for line in stream:
            frame = json.loads(line)
            snapshot = frame["snapshot"]
            assert snapshot["tick"] == frames <= config["durationTicks"], "Noncontiguous tick"
            assert snapshot["protocolVersion"] == config["protocolVersion"]
            assert snapshot["rulesVersion"] == config["rulesVersion"]
            assert type(snapshot["completed"]) is bool
            assert snapshot["completed"] == (frames == config["durationTicks"])
            assert len(snapshot["scoreHalfPoints"]) == 2
            assert frame["stateHash"] == state_hash(config, snapshot), "State hash mismatch at tick " + str(frames)
            last = snapshot
            frames += 1
        assert frames == config["durationTicks"] + 1, "Incomplete replay"
    result = json.loads(result_path.read_text(encoding="utf-8-sig"))
    assert result["type"] == "bootstrap-result" and result["resultVersion"] == 1
    assert result["status"] == "bootstrap-completed" and result["replayVersion"] == 2
    assert result["config"] == config and result["finalSnapshot"] == last
    assert result["stateHash"] == state_hash(config, last)
    with replay_path.open("rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(65536), b""):
            digest.update(block)
    assert result["replaySha256"] == digest.hexdigest(), "Replay file digest mismatch"
    print(f"PASS: {frames} snapshots, final tick {last['tick']}, stateHash={result['stateHash']}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--replay", type=Path, required=True)
    parser.add_argument("--result", type=Path, required=True)
    args = parser.parse_args()
    verify(args.replay, args.result)
