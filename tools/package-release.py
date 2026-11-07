"""Package the formal Linux services, public source, SDK and operations documents."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import tarfile
import zipfile


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def package(directory, repository):
    source = json.loads((directory / 'source-manifest.json').read_text(encoding='utf-8-sig'))
    info = json.loads((directory / 'BUILD-INFO.json').read_text(encoding='utf-8-sig'))
    server = directory / 'TuringSignalRelay-server-linux.tar.gz'
    sdk = directory / 'TuringSignalRelay-sdk.zip'
    snapshot = directory / 'TuringSignalRelay-source.zip'
    if any(path.exists() for path in (server, sdk, snapshot)):
        raise FileExistsError('Use a new delivery directory')
    entries = source['files']
    # Bundle the exact audited snapshot, including additions not yet committed to Git.
    for entry in entries:
        data = (repository / entry['path']).read_bytes()
        normalized = data.replace(b'\r\n', b'\n')
        assert hashlib.sha256(data).hexdigest() == entry['sha256'] or hashlib.sha256(normalized).hexdigest() == entry['sha256'], entry['path']
    with tarfile.open(server, 'w:gz') as archive:
        for name in ('Platform', 'Worker'):
            archive.add(directory / 'server-linux' / name, arcname=name.lower())
        for entry in entries:
            if entry['path'].startswith(('deploy/', 'fixtures/', 'sdk/cpp/', 'docs/')):
                data = (repository / entry['path']).read_bytes()
                if Path(entry['path']).suffix in ('.sh', '.py', '.policy'):
                    data = data.replace(b'\r\n', b'\n')
                member = tarfile.TarInfo(entry['path'])
                member.size = len(data)
                member.mode = 0o755 if Path(entry['path']).suffix in ('.sh', '.py') else 0o644
                archive.addfile(member, io.BytesIO(data))
        archive.add(directory / 'BUILD-INFO.json', arcname='BUILD-INFO.json')
        archive.add(directory / 'source-manifest.json', arcname='source-manifest.json')
    with zipfile.ZipFile(sdk, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        for entry in entries:
            if entry['path'].startswith(('sdk/cpp/', 'fixtures/')):
                archive.write(repository / entry['path'], entry['path'])
        archive.write(directory / 'BUILD-INFO.json', 'BUILD-INFO.json')
    with zipfile.ZipFile(snapshot, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        for entry in entries:
            archive.write(repository / entry['path'], entry['path'])
        if (repository / 'CONTRIBUTING.md').is_file():
            archive.write(repository / 'CONTRIBUTING.md', 'CONTRIBUTING.md')
        archive.write(directory / 'BUILD-INFO.json', 'BUILD-INFO.json')
        archive.write(directory / 'source-manifest.json', 'source-manifest.json')
    packages = [{'name': path.name, 'bytes': path.stat().st_size, 'sha256': digest(path)}
                for path in (server, sdk, snapshot)]
    report = {'status': 'passed', 'sourceCommit': info['sourceCommit'], 'sourceModified': info['sourceModified'], 'packages': packages}
    (directory / 'release-packages.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('FORMAL_RELEASE_PACKAGES_CREATED count=3', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--repository', type=Path, required=True)
    args = parser.parse_args()
    package(args.directory.resolve(strict=True), args.repository.resolve(strict=True))
