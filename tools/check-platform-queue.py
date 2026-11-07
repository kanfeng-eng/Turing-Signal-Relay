"""Check duplicate requests and concurrent upload limits with the idle worker stopped."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
from pathlib import Path
import secrets
import threading

module_spec = importlib.util.spec_from_file_location('platform_checks', Path(__file__).with_name('check-platform.py'))
platform_checks = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(platform_checks)
Client = platform_checks.Client


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', required=True)
    parser.add_argument('--credentials', type=Path, required=True)
    parser.add_argument('--previous-checks', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    credentials = json.loads(args.credentials.read_text(encoding='utf-8-sig'))
    judge = Client(args.url)
    judge.login(credentials['username'], credentials['password'])
    previous = json.loads(args.previous_checks.read_text(encoding='utf-8'))
    match_id = previous['matches'][0]['id']
    code, body = judge.request('/api/matches/' + match_id)
    assert code == 200
    match = json.loads(body)['match']
    assert match['seed'] == 17
    code, body = judge.create_match(match['teamASubmissionId'], match['teamBSubmissionId'], match['requestKey'])
    assert code == 202 and json.loads(body)['id'] == match_id
    assert judge.create_match(match['teamBSubmissionId'], match['teamASubmissionId'], match['requestKey'])[0] == 400

    suffix = secrets.token_hex(4)
    username, password = 'queue-' + suffix, secrets.token_urlsafe(24) + 'aA9!'
    assert judge.form('/Judge?handler=Account', {'Username': username, 'Password': password, 'TeamName': 'queue ' + suffix})[0] == 200
    clients = [Client(args.url) for _ in range(6)]
    for client in clients:
        client.login(username, password)
    barrier = threading.Barrier(6)

    def upload(client):
        barrier.wait(timeout=15)
        return client.upload(b'int main() { return 0; }\n')[0]

    with ThreadPoolExecutor(max_workers=6) as executor:
        statuses = sorted(executor.map(upload, clients))
    assert statuses == [202] * 5 + [400], statuses
    code, body = clients[0].request('/api/submissions')
    submissions = json.loads(body)
    assert code == 200 and len(submissions) == 5 and all(item['status'] == 'queued' for item in submissions)
    report = {'status': 'passed', 'sameRequestIdempotency': 'passed', 'conflictingRequestRejected': 'passed',
              'concurrentUploads': 6, 'accepted': 5, 'rejected': 1, 'queuedCount': len(submissions)}
    (args.output / 'CHECKS.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('PLATFORM_QUEUE_CHECKS_PASSED', flush=True)


if __name__ == '__main__':
    main()
