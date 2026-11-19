"""Run as tsr-worker on the installed Ubuntu host. Every AI stays inside nsjail."""
import argparse
import json
import os
from pathlib import Path
import selectors
import subprocess
import time
import uuid

HELPER = '/usr/local/libexec/tsr-sandbox'


def invoke(operation, token, extra=(), source=None, timeout=45):
    result = subprocess.run(['sudo', '-n', HELPER, operation, token, *extra], input=source, capture_output=True, timeout=timeout)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.decode(errors='replace')[:2000])
    return json.loads(result.stdout) if result.stdout.strip() else None


def line(process, timeout):
    with selectors.DefaultSelector() as selector:
        selector.register(process.stdout, selectors.EVENT_READ)
        if not selector.select(timeout):
            return None
        return process.stdout.readline(65538)


def compile_source(source):
    token = uuid.uuid4().hex
    report = invoke('compile', token, source=source)
    return token, report


def start(submission):
    token = uuid.uuid4().hex
    resources = invoke('prepare', token, [submission])['resourceDirectory']
    process = subprocess.Popen(['sudo', '-n', HELPER, 'run', token], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, bufsize=0)
    return token, Path(resources), process


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--stress', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    root = Path(__file__).resolve().parent.parent
    reports = []
    hello, compiled = compile_source((root / 'fixtures/sandbox/hello.cpp').read_bytes())
    assert compiled['success'], compiled
    token, resources, process = start(hello)
    try:
        output, diagnostic = process.communicate(timeout=3)
        assert process.returncode == 0 and b'SANDBOX_HELLO' in output, diagnostic
        reports.append({'case': 'legal-static-program', 'status': 'passed', 'exitCode': process.returncode})
    finally:
        invoke('release', token)
    assert not resources.exists()
    identifier, rejected = compile_source(b'#include "/etc/shadow"\nint main(){}\n')
    assert not rejected['success'] and 'No such file' in rejected['diagnostic'], rejected
    reports.append({'case': 'compiler-host-file', 'status': 'denied', 'exitCode': rejected['exitCode']})
    violations, compiled = compile_source((root / 'fixtures/sandbox/violations.cpp').read_bytes())
    assert compiled['success'], compiled
    reports.append({'case': 'test-fixture-compilation', 'status': 'passed', 'compiler': compiled['compiler'], 'memoryPeakBytes': compiled['memoryPeakBytes']})
    for violation, name in enumerate(['host-file', 'network', 'fork', 'thread', 'infinite-loop', 'memory', 'stdout-flood', 'cpu-budget-sample']):
        token, resources, process = start(violations)
        try:
            assert int((resources / 'memory.max').read_text()) == 256 * 1024 * 1024
            assert int((resources / 'memory.swap.max').read_text()) == 0
            assert int((resources / 'pids.max').read_text()) == 1
            process.stdin.write((json.dumps({'type': 'relay-init', 'protocolVersion': 2, 'side': 0, 'testViolation': violation}) + '\n').encode())
            process.stdin.flush()
            ready = line(process, 2)
            assert ready and json.loads(ready)['type'] == 'relay-ready', (name, ready)
            before_cpu = int((resources / 'cpu.stat').read_text().split('usage_usec ')[1].splitlines()[0])
            started = time.monotonic()
            process.stdin.write(b'{"type":"relay-frame","protocolVersion":2,"side":0,"tick":0,"completed":false}\n')
            process.stdin.flush()
            reply = line(process, 0.3)
            if name == 'cpu-budget-sample':
                after_cpu = int((resources / 'cpu.stat').read_text().split('usage_usec ')[1].splitlines()[0])
                assert reply and json.loads(reply)['tick'] == 0 and after_cpu - before_cpu > 10000 and time.monotonic() - started < 0.1
            elif name == 'infinite-loop':
                assert reply is None, (name, reply)
                # A separate namespace/cgroup still runs while this one consumes CPU.
                other_token, other_resources, other = start(hello)
                try:
                    output, _ = other.communicate(timeout=3)
                    assert other.returncode == 0 and b'SANDBOX_HELLO' in output
                finally:
                    invoke('release', other_token)
                assert not other_resources.exists()
            elif name == 'stdout-flood':
                assert reply is not None and len(reply) > 65536, (name, reply)
            else:
                process.wait(timeout=3)
                assert process.returncode != 0, (name, process.returncode)
            invoke('stop', token)
            process.wait(timeout=3)
            reports.append({'case': name, 'status': 'denied', 'exitCode': process.returncode,
                            'memoryPeakBytes': int((resources / 'memory.peak').read_text()),
                            'memoryEvents': (resources / 'memory.events').read_text(),
                            'pidsEvents': (resources / 'pids.events').read_text(),
                            'diagnostic': process.stderr.read(4096).decode(errors='replace')})
            assert reports[-1]['memoryPeakBytes'] <= 256 * 1024 * 1024
            if name == 'memory':
                assert reports[-1]['memoryPeakBytes'] >= 200 * 1024 * 1024, 'Memory fixture did not exercise resident growth near the cap'
        finally:
            invoke('release', token)
            process.wait(timeout=3)
        assert not resources.exists(), 'Sandbox cgroup was not removed'
        print('SANDBOX_CASE_PASSED', name, flush=True)
    if args.stress:
        _, memory_limited = compile_source((root / 'fixtures/sandbox/compile-memory.cpp').read_bytes())
        assert not memory_limited['success'] and memory_limited['memoryPeakBytes'] <= 512 * 1024 * 1024
        reports.append({'case': 'compile-memory-budget', 'status': 'denied', 'report': memory_limited})
        source = (root / 'fixtures/sandbox/compile-timeout.cpp').read_bytes()
        started = time.monotonic()
        _, timed = compile_source(source)
        elapsed = time.monotonic() - started
        assert not timed['success'] and 25 <= elapsed <= 40, (elapsed, timed)
        reports.append({'case': 'compile-time-budget', 'status': 'denied', 'seconds': elapsed, 'report': timed})
        token = uuid.uuid4().hex
        process = subprocess.Popen(['sudo', '-n', HELPER, 'compile', token], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        process.stdin.write(source); process.stdin.close(); process.stdin = None
        record = Path('/var/lib/tsr-sandbox/compiling', token)
        deadline = time.monotonic() + 5
        while not record.exists() and time.monotonic() < deadline:
            time.sleep(0.05)
        assert record.exists(), 'Compile cancellation fixture did not start'
        group = json.loads(record.read_text())['group']
        invoke('cancel-compile', token)
        process.communicate(timeout=5)
        assert not Path('/sys/fs/cgroup/tsr', group).exists() and not record.exists()
        reports.append({'case': 'compile-cancellation-cleanup', 'status': 'passed'})
    report = {'status': 'passed', 'cases': reports, 'allRuntimeGroupsRemoved': True, 'isolation': 'nsjail-namespaces-seccomp-cgroupv2-static-elf'}
    (args.output / 'CHECKS.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('SANDBOX_CHECKS_PASSED cases=' + str(len(reports)), flush=True)


if __name__ == '__main__':
    main()
