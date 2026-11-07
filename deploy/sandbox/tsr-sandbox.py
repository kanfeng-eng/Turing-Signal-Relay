#!/usr/bin/python3
"""Root-owned entry point. sudoers exposes only this fixed-protocol helper."""
import hashlib
import fcntl
import json
import os
from pathlib import Path
import re
import shutil
import signal
import stat
import struct
import subprocess
import sys
import tempfile
import time

ROOT = Path('/var/lib/tsr-sandbox')
TOOLS = Path('/opt/tsr/sandbox')
CGROUP = Path('/sys/fs/cgroup/tsr')
JAIL = str(TOOLS / 'nsjail')


def identifier(value):
    if not re.fullmatch('[a-f0-9]{32}', value):
        raise ValueError('Invalid sandbox identifier')
    return value


def write(path, value):
    path.write_text(str(value), encoding='utf-8')


def make_cgroup(token, memory, pids):
    path = CGROUP / token
    path.mkdir(mode=0o755)
    write(path / 'memory.max', memory)
    write(path / 'memory.swap.max', 0)
    write(path / 'pids.max', pids)
    write(path / 'cgroup.subtree_control', '+memory +pids +cpu')
    return path


def stop(token):
    group = CGROUP / token
    if group.is_dir():
        write(group / 'cgroup.kill', 1)
    record = ROOT / 'runs' / token / 'supervisor.pid'
    if record.exists():
        pid = int(record.read_text())
        try:
            args = Path('/proc', str(pid), 'cmdline').read_bytes().split(b'\0')
            if os.fsencode(str(group)) in args and os.fsencode(JAIL) in args:
                os.kill(pid, signal.SIGKILL)
        except (FileNotFoundError, ProcessLookupError):
            pass


def release(token):
    stop(token)
    group = CGROUP / token
    if group.exists():
        for _ in range(100):
            if 'populated 0' in (group / 'cgroup.events').read_text():
                break
            time.sleep(0.01)
        for child in sorted(group.rglob('*'), key=lambda p: len(p.parts), reverse=True):
            if child.is_dir():
                child.rmdir()
        group.rmdir()
    directory = ROOT / 'runs' / token
    if directory.exists():
        shutil.rmtree(directory)


def cancel_compile(token):
    record = ROOT / 'compiling' / token
    if record.exists():
        value = json.loads(record.read_text())
        group = CGROUP / identifier(value['group'])
        if group.exists():
            write(group / 'cgroup.kill', 1)
        try:
            args = Path('/proc', str(value['pid']), 'cmdline').read_bytes().split(b'\0')
            if os.fsencode(str(group)) in args and os.fsencode(JAIL) in args:
                os.kill(value['pid'], signal.SIGKILL)
        except (FileNotFoundError, ProcessLookupError):
            pass


def inspect_run(token):
    path = ROOT / 'runs' / token / 'jail.log'
    log = path.read_bytes()[:32768].decode('utf-8', 'replace') if path.exists() else ''
    print(json.dumps({'supervisorLog': log, 'infrastructureFailure': not path.is_file() or '[E]' in log or '[F]' in log}))


def base_args(group, rootfs, memory, pids, seconds, policy):
    return [JAIL, '-Mo', '--quiet', '--disable_clone_newuser', '--user', '65534', '--group', '65534',
            '--env', 'PATH=/usr/bin:/bin', '--env', 'LANG=C',
            '--chroot', str(rootfs), '--cwd', '/', '--hostname', 'relay-ai', '--time_limit', str(seconds),
            '--rlimit_as', str(memory), '--rlimit_cpu', str(seconds), '--rlimit_fsize', '16',
            '--rlimit_nofile', '64', '--rlimit_stack', '8', '--use_cgroupv2',
            '--cgroupv2_mount', str(group), '--cgroup_mem_max', str(memory * 1024 * 1024),
            '--cgroup_pids_max', str(pids), '--seccomp_policy', str(TOOLS / policy)]


def compile_source(token):
    source = sys.stdin.buffer.read(256 * 1024 + 1)
    if not source or len(source) > 256 * 1024:
        raise ValueError('Source must be 1..262144 bytes')
    source_hash = hashlib.sha256(source).hexdigest()
    destination = ROOT / 'submissions' / token
    if destination.exists():
        cached = json.loads((destination / 'compilation.json').read_text())
        if cached['sourceHash'] != source_hash:
            raise ValueError('Submission identifiers are immutable')
        print(json.dumps(cached))
        return
    token_group = identifier(os.urandom(16).hex())
    group = make_cgroup(token_group, 512 * 1024 * 1024, 64)
    directory = Path(tempfile.mkdtemp(prefix='compile-', dir=ROOT / 'work'))
    rootfs, work = directory / 'root', directory / 'work'
    rootfs.mkdir(mode=0o755)
    work.mkdir(mode=0o777)
    os.chmod(work, 0o777)
    (work / 'ai.cpp').write_bytes(source)
    os.chmod(work / 'ai.cpp', 0o444)
    args = base_args(group, rootfs, 512, 64, 30, 'compile.policy')
    args += ['--log', str(directory / 'jail.log')]
    for host, guest in [('/usr', '/usr'), ('/lib', '/lib'), ('/lib64', '/lib64'), (str(TOOLS / 'sdk'), '/sdk'), ('/dev/null', '/dev/null')]:
        if Path(host).exists():
            args += ['--bindmount_ro', host + ':' + guest]
    args += ['--tmpfsmount', '/tmp', '--bindmount', str(work) + ':/work', '--cwd', '/work', '--', '/usr/bin/g++',
             '-std=c++17', '-O2', '-pipe', '-static', '-B/usr/bin/', '-fno-diagnostics-color', '-I/sdk', '-I/sdk/examples',
             '/work/ai.cpp', '-o', '/work/ai']
    exit_code = -1
    timed_out = False
    started = time.monotonic()
    try:
        with (directory / 'compiler.log').open('wb') as log:
            process = subprocess.Popen(args, stdin=subprocess.DEVNULL, stdout=log, stderr=log, env={'LANG': 'C', 'PATH': '/usr/bin:/bin'})
            (ROOT / 'compiling' / token).write_text(json.dumps({'pid': process.pid, 'group': token_group}))
            try:
                exit_code = process.wait(timeout=35)
            except subprocess.TimeoutExpired:
                timed_out = True
                write(group / 'cgroup.kill', 1)
                process.kill()
                exit_code = process.wait()
        diagnostic = (directory / 'compiler.log').read_bytes()[:32768].decode('utf-8', 'replace')
        supervisor_log = (directory / 'jail.log').read_text(errors='replace')[:32768] if (directory / 'jail.log').exists() else ''
        if exit_code == 255:
            raise RuntimeError('Compilation jail failed to initialize: ' + supervisor_log[:2000])
        elapsed = time.monotonic() - started
        memory_events = (group / 'memory.events').read_text()
        timed_out = timed_out or 'time limit' in supervisor_log.lower() or 'time_limit' in supervisor_log.lower() or (exit_code in (137, 152, -9, -24) and elapsed >= 29 and 'oom_kill 0' in memory_events)
        sdk_hash = hashlib.sha256()
        for path in sorted((TOOLS / 'sdk').rglob('*')):
            if path.is_file():
                sdk_hash.update(path.relative_to(TOOLS / 'sdk').as_posix().encode() + b'\0' + path.read_bytes())
        report = {'sourceHash': source_hash, 'exitCode': exit_code, 'timedOut': timed_out,
                  'diagnostic': diagnostic, 'compiler': subprocess.check_output(['/usr/bin/g++', '--version'], text=True).splitlines()[0],
                  'nsjailCommit': 'd8415bc8eea43cdd413daba25af63127019a1189', 'binaryHash': '', 'success': False,
                  'sdkHash': sdk_hash.hexdigest(),
                  'compilePolicyHash': hashlib.sha256((TOOLS / 'compile.policy').read_bytes()).hexdigest(),
                  'runPolicyHash': hashlib.sha256((TOOLS / 'run.policy').read_bytes()).hexdigest(),
                  'memoryPeakBytes': int((group / 'memory.peak').read_text()),
                  'memoryEvents': memory_events, 'supervisorLog': supervisor_log, 'elapsedSeconds': elapsed}
        if exit_code == 0:
            fd = os.open(work / 'ai', os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
            with os.fdopen(fd, 'rb') as binary:
                metadata = os.fstat(binary.fileno())
                if not stat.S_ISREG(metadata.st_mode) or metadata.st_size > 16 * 1024 * 1024:
                    raise ValueError('Compiler output is not a bounded regular file')
                data = binary.read(16 * 1024 * 1024 + 1)
            if data[:4] != b'\x7fELF':
                raise ValueError('Compiler output is not ELF')
            offset = struct.unpack_from('<Q', data, 32)[0]
            entry_size, count = struct.unpack_from('<HH', data, 54)
            static_elf = len(data) >= 64 and data[4:6] == b'\x02\x01' and struct.unpack_from('<H', data, 18)[0] == 62 and entry_size == 56 and offset + count * entry_size <= len(data)
            if static_elf:
                static_elf = not any(struct.unpack_from('<I', data, offset + index * entry_size)[0] == 3 for index in range(count))
            if not static_elf:
                report['diagnostic'] = 'Only static x86_64 ELF programs are supported.'
                report['exitCode'] = 2
            else:
                staging = directory / 'published'
                staging.mkdir(mode=0o755)
                (staging / 'ai').write_bytes(data)
                os.chmod(staging / 'ai', 0o555)
                report['binaryHash'] = hashlib.sha256(data).hexdigest()
                report['success'] = True
                (staging / 'compilation.json').write_text(json.dumps(report), encoding='utf-8')
                os.rename(staging, destination)
        print(json.dumps(report))
    finally:
        (ROOT / 'compiling' / token).unlink(missing_ok=True)
        write(group / 'cgroup.kill', 1)
        for child in sorted(group.rglob('*'), key=lambda p: len(p.parts), reverse=True):
            if child.is_dir():
                child.rmdir()
        group.rmdir()
        shutil.rmtree(directory)


def prepare(token, submission):
    binary = ROOT / 'submissions' / submission / 'ai'
    if not binary.is_file():
        raise ValueError('Compiled submission does not exist')
    directory = ROOT / 'runs' / token
    directory.mkdir(mode=0o755)
    (directory / 'root').mkdir(mode=0o755)
    write(directory / 'submission', submission)
    group = make_cgroup(token, 256 * 1024 * 1024, 1)
    print(json.dumps({'resourceDirectory': str(group)}))


def run(token):
    directory = ROOT / 'runs' / token
    submission = identifier((directory / 'submission').read_text())
    binary = ROOT / 'submissions' / submission / 'ai'
    args = base_args(CGROUP / token, directory / 'root', 256, 1, 180, 'run.policy')
    args += ['--log', str(directory / 'jail.log')]
    args += ['--bindmount_ro', str(binary) + ':/ai', '--', '/ai']
    write(directory / 'supervisor.pid', os.getpid())
    os.execve(JAIL, args, {'LANG': 'C', 'PATH': '/usr/bin:/bin'})


if __name__ == '__main__':
    os.umask(0o022)
    if os.geteuid() != 0:
        raise SystemExit('Must be invoked through the project sudoers entry')
    try:
        command, token = sys.argv[1], identifier(sys.argv[2])
        if command == 'compile' and len(sys.argv) == 3:
            with (ROOT / 'compile-locks' / token).open('a') as lock:
                fcntl.flock(lock, fcntl.LOCK_EX)
                compile_source(token)
        elif command == 'cancel-compile' and len(sys.argv) == 3:
            cancel_compile(token)
        elif command == 'prepare' and len(sys.argv) == 4:
            prepare(token, identifier(sys.argv[3]))
        elif command == 'run' and len(sys.argv) == 3:
            run(token)
        elif command == 'stop' and len(sys.argv) == 3:
            stop(token)
        elif command == 'inspect' and len(sys.argv) == 3:
            inspect_run(token)
        elif command == 'release' and len(sys.argv) == 3:
            release(token)
        else:
            raise ValueError('Unsupported helper operation')
    except Exception as error:
        print(json.dumps({'infrastructureError': type(error).__name__ + ': ' + str(error)}), file=sys.stderr)
        raise SystemExit(70)
