"""Verify participant faults and wait for a controlled worker restart during a real job."""
import argparse
import json
from pathlib import Path
import secrets
import time
import importlib.util

module_spec = importlib.util.spec_from_file_location('platform_checks', Path(__file__).with_name('check-platform.py'))
platform_checks = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(platform_checks)
Client, wait_json = platform_checks.Client, platform_checks.wait_json


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--url', required=True)
    parser.add_argument('--credentials', type=Path, required=True)
    parser.add_argument('--previous-checks', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    credentials = json.loads(args.credentials.read_text(encoding='utf-8-sig'))
    previous = json.loads(args.previous_checks.read_text(encoding='utf-8'))
    judge = Client(args.url); judge.login(credentials['username'], credentials['password'])
    suffix = secrets.token_hex(4)
    username, password = 'ops-' + suffix, secrets.token_urlsafe(24) + 'aA9!'
    assert judge.form('/Judge?handler=Account', {'Username': username, 'Password': password, 'TeamName': 'ops ' + suffix})[0] == 200
    owner = Client(args.url); owner.login(username, password)
    root = Path(__file__).resolve().parent.parent
    fixture = (root / 'fixtures/sandbox/violations.cpp').read_bytes()
    reports = []
    for mode, name in [(0, 'file-access-forfeit'), (7, 'three-cpu-timeouts')]:
        code, body = owner.upload(('#define VIOLATION ' + str(mode) + '\n').encode() + fixture)
        assert code == 202, body[:400]
        identifier = json.loads(body)['id']
        submission = wait_json(owner, '/api/submissions/' + identifier, {'ready', 'failed'}, 180)
        assert submission['status'] == 'ready', submission['diagnostic']
        code, body = judge.create_match(identifier, previous['submissionIds'][1], secrets.token_hex(16))
        assert code == 202, body[:400]
        match_id = json.loads(body)['id']
        result = wait_json(judge, '/api/matches/' + match_id, {'completed', 'failed'}, 120)
        assert result['match']['status'] == 'completed', result['match']['diagnostic']
        assert result['result']['status'] == 'forfeit' and result['result']['winnerTeam'] == 1
        replay = json.loads(owner.request('/api/matches/' + match_id + '/artifacts/replay-1')[1])
        assert replay['forfeit'] and replay['faultSide'] == 0
        if mode == 7:
            assert len(replay['frames']) == 2 and 'Three computation timeouts' in replay['terminationReason']
        code, body = owner.request('/api/matches/' + match_id + '/snapshot')
        assert code == 200 and json.loads(body)['status'] == 'forfeit' and json.loads(body)['tick'] == len(replay['frames'])
        assert owner.request('/api/matches/' + match_id + '/logs/1/0')[0] == 200
        assert owner.request('/api/matches/' + match_id + '/logs/1/1')[0] == 403
        reports.append({'case': name, 'id': match_id, 'status': 'passed', 'reason': replay['terminationReason'], 'tick': len(replay['frames'])})
        print('PLATFORM_OPERATION_PASSED', name, flush=True)
    code, body = judge.create_match(*previous['submissionIds'], secrets.token_hex(16))
    assert code == 202, body[:400]
    match_id = json.loads(body)['id']
    running = wait_json(judge, '/api/matches/' + match_id, {'running', 'completed', 'failed'}, 60)
    assert running['match']['status'] == 'running' and running['match']['attempts'] == 1
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        code, body = judge.request('/api/matches/' + match_id + '/snapshot')
        if code == 200:
            assert json.loads(body)['status'] == 'running'
            break
        time.sleep(0.1)
    else:
        raise AssertionError('Running snapshot did not appear')
    (args.output / 'restart.json').write_text(json.dumps({'id': match_id}), encoding='utf-8')
    print('WORKER_RESTART_REQUIRED', match_id, flush=True)
    result = wait_json(judge, '/api/matches/' + match_id, {'completed', 'failed'}, 420)
    assert result['match']['status'] == 'completed' and result['match']['attempts'] == 2, result['match']
    assert all(not game['forfeit'] and game['result']['finalSnapshot']['tick'] == 9000 for game in result['result']['games'])
    reports.append({'case': 'worker-restart-recovery', 'id': match_id, 'status': 'passed', 'attempts': 2,
                    'hashes': [game['result']['stateHash'] for game in result['result']['games']]})
    (args.output / 'CHECKS.json').write_text(json.dumps({'status': 'passed', 'cases': reports}, indent=2) + '\n', encoding='utf-8')
    print('PLATFORM_OPERATIONS_CHECKS_PASSED', flush=True)


if __name__ == '__main__':
    main()
