#!/usr/bin/env python3
"""Project backup and restore into a new directory. Requires the host administrator."""
import argparse
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import pwd
import grp
import shutil
import sqlite3

DATA = Path('/var/lib/tsr/data')
CACHE = Path('/var/lib/tsr-sandbox/submissions')


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def inside(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Backup path escapes its root')
    return path


def verify(directory):
    report = json.loads((directory / 'manifest.json').read_text(encoding='utf-8'))
    for entry in report['files']:
        path = inside(directory, entry['path'])
        if not path.is_file() or digest(path) != entry['sha256']:
            raise ValueError('Backup digest mismatch ' + entry['path'])
    with closing(sqlite3.connect(directory / 'data/contest.db')) as database:
        if database.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
            raise ValueError('Backup database failed integrity_check')
        for relative, in database.execute('SELECT RelativePath FROM Artifacts'):
            if not inside(directory / 'data', relative).is_file():
                raise ValueError('Backup is missing an indexed artifact')
    return report


def create(destination):
    if destination.exists() or destination.resolve().is_relative_to(DATA.resolve()):
        raise ValueError('Use a new backup directory outside the live data directory')
    destination.mkdir(mode=0o700, parents=True)
    target = destination / 'data'; target.mkdir(mode=0o700)
    with closing(sqlite3.connect(DATA / 'contest.db')) as source, closing(sqlite3.connect(target / 'contest.db')) as database:
        source.backup(database)
    for name in ('submissions', 'matches', 'keys'):
        shutil.copytree(DATA / name, target / name)
    compiled = destination / 'sandbox-submissions'; compiled.mkdir(mode=0o700)
    with closing(sqlite3.connect(target / 'contest.db')) as database:
        for identifier, in database.execute("SELECT Id FROM Submissions WHERE Status='ready'"):
            shutil.copytree(inside(CACHE, identifier), compiled / identifier)
    files = [{'path': path.relative_to(destination).as_posix(), 'sha256': digest(path)}
             for path in sorted(destination.rglob('*')) if path.is_file()]
    report = {'version': 1, 'files': files}
    (destination / 'manifest.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    verify(destination)
    print('CONTEST_BACKUP_VERIFIED files=' + str(len(files)))


def restore(backup, destination, restore_cache):
    verify(backup)
    if destination.exists():
        raise ValueError('Restore requires a new data directory')
    shutil.copytree(backup / 'data', destination)
    uid, gid = pwd.getpwnam('tsr-platform').pw_uid, grp.getgrnam('tsr-data').gr_gid
    for path in [destination, *destination.rglob('*')]:
        if path.is_symlink():
            raise ValueError('Unexpected symbolic link in backup')
        os.chown(path, uid, gid)
        secret = path == destination / 'keys' or (destination / 'keys') in path.parents
        path.chmod((0o700 if secret else 0o2770) if path.is_dir() else (0o600 if secret else 0o660))
    if restore_cache:
        for directory in (backup / 'sandbox-submissions').iterdir():
            target = inside(CACHE, directory.name)
            if target.exists():
                if digest(target / 'ai') != digest(directory / 'ai'):
                    raise ValueError('Existing compiled cache differs from backup')
            else:
                shutil.copytree(directory, target)
                os.chown(target, 0, 0); target.chmod(0o755)
                for file in target.iterdir():
                    os.chown(file, 0, 0); file.chmod(0o555 if file.name == 'ai' else 0o644)
    print('CONTEST_RESTORE_VERIFIED ' + str(destination))


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest='operation', required=True)
    create_parser = commands.add_parser('create'); create_parser.add_argument('destination', type=Path)
    verify_parser = commands.add_parser('verify'); verify_parser.add_argument('backup', type=Path)
    restore_parser = commands.add_parser('restore'); restore_parser.add_argument('backup', type=Path); restore_parser.add_argument('destination', type=Path)
    restore_parser.add_argument('--restore-cache', action='store_true')
    args = parser.parse_args()
    if os.geteuid() != 0:
        raise SystemExit('Run this project maintenance command with sudo')
    os.umask(0o077)
    if args.operation == 'create': create(args.destination)
    elif args.operation == 'verify': verify(args.backup); print('CONTEST_BACKUP_VERIFIED')
    else: restore(args.backup, args.destination, args.restore_cache)


if __name__ == '__main__':
    main()
