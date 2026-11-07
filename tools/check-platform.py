"""Exercise authenticated platform routes against the actual deployed worker."""
import argparse
import hashlib
import html
import http.cookiejar
import json
from pathlib import Path
import re
import secrets
import time
import urllib.error
import urllib.parse
import urllib.request


class Client:
    def __init__(self, base):
        self.base = base.rstrip('/')
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, data=None, headers=None, method=None):
        request = urllib.request.Request(self.base + path, data=data, headers=headers or {}, method=method)
        try:
            with self.opener.open(request, timeout=30) as response:
                return response.status, response.read()
        except urllib.error.HTTPError as error:
            return error.code, error.read()

    def csrf(self):
        code, body = self.request('/api/csrf')
        assert code == 200, code
        return json.loads(body)['token']

    def form(self, path, fields):
        code, body = self.request(path.split('?')[0])
        assert code == 200, (path, code)
        token = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body.decode())[1])
        return self.request(path, urllib.parse.urlencode(dict(fields, __RequestVerificationToken=token)).encode(),
                            {'Content-Type': 'application/x-www-form-urlencoded'})

    def login(self, username, password):
        code, _ = self.form('/Login', {'Username': username, 'Password': password})
        assert code == 200 and self.request('/api/csrf')[0] == 200, 'Login failed'

    def upload(self, source, filename='ai.cpp', token=True, note='platform-check'):
        boundary = 'tsr-' + secrets.token_hex(12)
        payload = (f'--{boundary}\r\nContent-Disposition: form-data; name="note"\r\n\r\n{note}\r\n'
                   f'--{boundary}\r\nContent-Disposition: form-data; name="source"; filename="{filename}"\r\n'
                   'Content-Type: text/plain\r\n\r\n').encode() + source + f'\r\n--{boundary}--\r\n'.encode()
        headers = {'Content-Type': 'multipart/form-data; boundary=' + boundary}
        if token:
            headers['X-CSRF-TOKEN'] = self.csrf()
        return self.request('/api/submissions', payload, headers)

    def create_match(self, first, second, key):
        return self.request('/api/matches', json.dumps({'teamASubmissionId': first, 'teamBSubmissionId': second,
                            'seed': 17, 'requestKey': key}).encode(),
                            {'Content-Type': 'application/json', 'X-CSRF-TOKEN': self.csrf()})


def wait_json(client, path, accepted, seconds):
    deadline = time.monotonic() + seconds
    previous = None
    while time.monotonic() < deadline:
        code, body = client.request(path)
        assert code == 200, (path, code)
        value = json.loads(body)
        item = value.get('match', value)
        if item['status'] != previous:
            print('PLATFORM_PROGRESS', path, item['status'], flush=True)
            previous = item['status']
        if item['status'] in accepted:
            return value
        time.sleep(1)
    raise TimeoutError(path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--url', required=True)
    parser.add_argument('--credentials', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--auth-only', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    judge = Client(args.url)
    credentials = json.loads(args.credentials.read_text(encoding='utf-8-sig'))
    assert judge.request('/api/submissions')[0] == 401
    judge.login(credentials['username'], credentials['password'])
    suffix = secrets.token_hex(4)
    password = secrets.token_urlsafe(24) + 'aA9!'
    clients = []
    for label in ('alpha', 'beta', 'outsider'):
        username = label + '-' + suffix
        team = label + ' ' + suffix
        code, _ = judge.form('/Judge?handler=Account', {'Username': username, 'Password': password, 'TeamName': team})
        assert code == 200
        client = Client(args.url); client.login(username, password)
        assert client.request('/Judge')[0] == 403
        clients.append(client)
    first, second, outsider = clients
    source_a = b'#include "courier_policy.hpp"\nint main(){try{tsr::Client c; while(c.getframeinfo()){courier_policy(c, false); c.submit();}}catch(...){return 2;}}\n'
    source_b = source_a.replace(b'false', b'true')
    # Compile the actual distributed examples, preserving their known entry-point contract.
    repository = Path(__file__).resolve().parent.parent
    source_a = (repository / 'sdk/cpp/examples/courier.cpp').read_bytes()
    source_b = (repository / 'sdk/cpp/examples/transporter.cpp').read_bytes()
    assert first.upload(source_a, token=False)[0] == 400
    assert first.upload(source_a, filename='program.exe')[0] == 400
    assert first.upload(b'x' * (256 * 1024 + 1))[0] in (400, 413)
    assert first.upload(b'\xff\xfe')[0] == 400
    code, body = first.upload(source_a, note='<script>alert(1)</script>')
    assert code == 202, (code, body[:300])
    submission_a = json.loads(body)['id']
    code, body = second.upload(source_b)
    assert code == 202, (code, body[:300])
    submission_b = json.loads(body)['id']
    assert outsider.request('/api/submissions/' + submission_a)[0] == 404
    assert second.request('/api/submissions/' + submission_a + '/source')[0] == 404
    assert first.request('/api/submissions/' + submission_a + '/source')[1] == source_a
    assert b'&lt;script&gt;' in first.request('/History')[1]
    assert first.create_match(submission_a, submission_b, secrets.token_hex(16))[0] == 403
    report = {'authentication': 'passed', 'csrf': 'passed', 'ownership': 'passed', 'uploadBounds': 'passed',
              'escapedNotes': 'passed', 'submissionIds': [submission_a, submission_b], 'matches': []}
    if not args.auth_only:
        for client, identifier in ((first, submission_a), (second, submission_b)):
            value = wait_json(client, '/api/submissions/' + identifier, {'ready', 'failed'}, 180)
            assert value['status'] == 'ready', value['diagnostic'][:1000]
        code, body = first.upload(b'this is not valid c plus plus')
        invalid = json.loads(body)['id']
        assert wait_json(first, '/api/submissions/' + invalid, {'ready', 'failed'}, 120)['status'] == 'failed'
        key = secrets.token_hex(16)
        code, body = judge.create_match(submission_a, submission_b, key)
        assert code == 202, (code, body[:500])
        match_id = json.loads(body)['id']
        assert json.loads(judge.create_match(submission_a, submission_b, key)[1])['id'] == match_id
        assert outsider.request('/api/matches/' + match_id)[0] == 404
        assert Client(args.url).request('/api/matches/' + match_id + '/snapshot')[0] == 401
        code, body = first.request('/api/matches/' + match_id + '/spectator-token', b'', {'X-CSRF-TOKEN': first.csrf()})
        assert code == 200
        token = json.loads(body)['token']
        result = wait_json(judge, '/api/matches/' + match_id, {'completed', 'failed'}, 420)
        assert result['match']['status'] == 'completed', result['match']['diagnostic']
        for number in (1, 2):
            game = result['result']['games'][number - 1]
            assert game['result']['finalSnapshot']['tick'] == 9000 and not game['forfeit'], game
            snapshot_path = '/api/matches/' + match_id + '/snapshot?game=' + str(number)
            code, body = Client(args.url).request(snapshot_path, headers={'Authorization': 'Bearer ' + token})
            assert code == 200 and json.loads(body)['tick'] == 9000
        code, body = judge.request('/api/matches/' + match_id + '/artifacts')
        assert code == 200
        for artifact in json.loads(body):
            code, data = first.request('/api/matches/' + match_id + '/artifacts/' + artifact['name'])
            assert code == 200 and hashlib.sha256(data).hexdigest() == artifact['sha256']
            (args.output / (artifact['name'] + '.json')).write_bytes(data)
        assert outsider.request('/api/matches/' + match_id + '/artifacts/result')[0] == 404
        report['matches'].append({'id': match_id, 'status': result['match']['status'],
                                  'hashes': [game['result']['stateHash'] for game in result['result']['games']]})
        report['compilerFailure'] = 'passed'
        report['idempotentMatchCreation'] = 'passed'
        report['authorizedSnapshotsAndArtifacts'] = 'passed'
    report['status'] = 'passed'
    (args.output / 'CHECKS.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('PLATFORM_CHECKS_PASSED', args.output, flush=True)


if __name__ == '__main__':
    main()
