"""Check package integrity and actual Windows/Linux executable architectures."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path, PurePosixPath
import struct
import tarfile
import zipfile


def verify(directory, project_copy=None):
    source_matches = None
    if project_copy:
        source = json.loads((directory / "source-manifest.json").read_text(encoding="utf-8-sig"))
        for entry in source["files"]:
            if entry["path"].startswith("game/Assets/") and entry["path"].endswith(".cs"):
                content = project_copy.joinpath(*PurePosixPath(entry["path"]).parts[1:]).read_bytes().replace(b"\r\n", b"\n")
                assert hashlib.sha256(content).hexdigest() == entry["sha256"], "Build copy code differs: " + entry["path"]
        source_matches = True
    with zipfile.ZipFile(directory / "TuringSignalRelay-windows.zip") as archive:
        assert archive.testzip() is None, "Windows package CRC failure."
        executable = archive.read("TuringSignalRelay.exe")
        assert executable[:2] == b"MZ", "Windows executable is not PE."
        offset = struct.unpack_from("<I", executable, 0x3C)[0]
        assert executable[offset:offset + 4] == b"PE\0\0" and struct.unpack_from("<H", executable, offset + 4)[0] == 0x8664
        windows_info = json.loads(archive.read("BUILD-INFO.json").decode("utf-8-sig"))
        assert archive.read("source-manifest.json") == (directory / "source-manifest.json").read_bytes()
    linux_path = directory / "TuringSignalRelay-linux.tar.gz"
    with gzip.open(linux_path, "rb") as stream:
        while stream.read(1024 * 1024):
            pass  # Reading through EOF validates the gzip trailer.
    with tarfile.open(linux_path) as archive:
        names = {}
        for item in archive:
            path = PurePosixPath(item.name)
            assert not path.is_absolute() and ".." not in path.parts, "Unsafe archive path."
            names[item.name.removeprefix("./")] = item
        executable = archive.extractfile(names["TuringSignalRelay"]).read(64)
        assert executable[:6] == b"\x7fELF\x02\x01" and struct.unpack_from("<H", executable, 18)[0] == 62, "Linux executable is not ELF64 x86_64."
        linux_info = json.loads(archive.extractfile(names["BUILD-INFO.json"]).read().decode("utf-8-sig"))
        assert linux_info == windows_info, "Packages disagree about build provenance."
        assert archive.extractfile(names["source-manifest.json"]).read() == (directory / "source-manifest.json").read_bytes()
    report = {"status": "passed", "windows": "PE-x86_64", "linux": "ELF64-x86_64", "buildInfoMatches": True, "codeSourceMatches": source_matches}
    (directory / "package-checks.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("PACKAGE_CHECKS_PASSED Windows=PE-x86_64 Linux=ELF64-x86_64")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--project-copy", type=Path)
    args = parser.parse_args()
    verify(args.directory.resolve(strict=True), args.project_copy)
