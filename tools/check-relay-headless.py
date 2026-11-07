"""Run the packaged Unity player with the complete rules and trusted example AIs."""
import argparse
import json
import os
from pathlib import Path
import subprocess


def check(player, repository, red, blue, output, expected_hash):
    output.mkdir(parents=True, exist_ok=False)
    log = output / 'player.log'
    startup = None
    if os.name == 'nt':
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    arguments = [str(player), '-batchmode', '-nographics', '-logFile', str(log),
                 '--relay-config', str(repository / 'fixtures/protocol/relay-full-config.json'),
                 '--relay-map', str(repository / 'fixtures/world/map-v1.json'),
                 '--relay-red-ai', str(red), '--relay-blue-ai', str(blue),
                 '--relay-replay', str(output / 'relay.replay.json'),
                 '--relay-result', str(output / 'relay.result.json')]
    result = subprocess.run(arguments, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                            timeout=180, startupinfo=startup)
    assert result.returncode == 0 and 'RELAY_CPP_COMPLETED' in log.read_text(encoding='utf-8-sig')
    replay = json.loads((output / 'relay.replay.json').read_text(encoding='utf-8-sig'))
    game = json.loads((output / 'relay.result.json').read_text(encoding='utf-8-sig'))
    assert not replay['forfeit'] and len(replay['frames']) == 9000
    assert game['status'] == 'full-rules-completed' and game['finalSnapshot']['tick'] == 9000
    assert game['stateHash'] == expected_hash
    report = {'status': 'passed', 'ticks': 9000, 'stateHash': game['stateHash'],
              'scoresHalfPoints': game['finalSnapshot']['scoreHalfPoints']}
    (output / 'CHECKS.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('FULL_RELAY_PLAYER_CHECKS_PASSED ticks=9000', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('player', 'repository', 'red', 'blue', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--expected-hash', required=True)
    args = parser.parse_args()
    check(args.player.resolve(strict=True), args.repository.resolve(strict=True), args.red.resolve(strict=True),
          args.blue.resolve(strict=True), args.output.resolve(), args.expected_hash)
