"""Validate public Unity metadata, assembly references and local documentation links."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess


def verify(root, include_local_art=False):
    names = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
                                    cwd=root).decode("utf-8").split("\0")
    files = [name for name in names if name and (name.startswith(("game/Assets/", "game/Packages/", "game/ProjectSettings/", "server/", "tools/", "docs/", "fixtures/"))
                                               or name in ("README.md", ".gitignore", ".gitattributes"))]
    if include_local_art:
        for folder in ["game/Assets/Game/Art/LegacyModels", "game/Assets/Game/Art/LegacyMaterials", "game/Assets/Game/Resources/LegacySprites"]:
            path = root / folder
            if not path.is_dir():
                raise FileNotFoundError("Local art directory missing: " + folder)
            files.extend(item.relative_to(root).as_posix() for item in path.rglob("*") if item.is_file())
            files.append(folder + ".meta")
        files = list(dict.fromkeys(files))
    approval = json.loads((root / "fixtures/rules/approved-v1.json").read_text(encoding="utf-8"))
    specification = (root / approval["specification"]).read_bytes().replace(b"\r\n", b"\n")
    assert approval["rulesVersion"] == "signal-relay-1.0"
    assert approval["decisions"] == {"R%02d" % number: "A" for number in range(1, 19)}
    assert hashlib.sha256(specification).hexdigest() == approval["specificationSha256"], "Approved specification changed without updating its record"
    guids, assemblies = {}, {}
    for name in files:
        path = root / name
        if name.startswith("game/Assets/"):
            if path.suffix == ".meta":
                match = re.search(r"^guid: ([a-f0-9]{32})$", path.read_text(encoding="utf-8"), re.M)
                assert match, "Missing GUID in " + name
                assert match[1] not in guids, "Duplicate GUID in " + name
                guids[match[1]] = name
                assert Path(str(path)[:-5]).exists(), "Orphan metadata " + name
            else:
                assert name + ".meta" in files, "Missing metadata " + name
            if path.suffix == ".asmdef":
                assembly = json.loads(path.read_text(encoding="utf-8"))
                assert assembly["name"] not in assemblies, "Duplicate assembly " + assembly["name"]
                assemblies[assembly["name"]] = assembly.get("references", [])

    def visit(name, stack):
        assert name not in stack, "Assembly cycle " + str(stack + [name])
        for dependency in assemblies[name]:
            if dependency in assemblies:
                visit(dependency, stack + [name])
            else:
                assert dependency == "UnityEngine.UI", "Unknown assembly " + dependency

    for name in assemblies:
        visit(name, [])
    links = 0
    for name in files:
        if not name.endswith(".md"):
            continue
        for target in re.findall(r"(?<!!)\[[^\]]+\]\(([^)]+)\)", (root / name).read_text(encoding="utf-8-sig")):
            if "://" in target or target.startswith(("#", "mailto:")):
                continue
            target = target.split("#")[0].strip("<>")
            if target:
                assert (root / name).parent.joinpath(target).exists(), "Broken link " + name + " -> " + target
                links += 1
    text_extensions = {".cs", ".csproj", ".json", ".md", ".ps1", ".py", ".asmdef", ".meta", ".unity", ".prefab", ".asset", ".css", ".cshtml", ".sh"}
    manifest = []
    for name in sorted(files):
        data = (root / name).read_bytes()
        if (root / name).suffix in text_extensions or name in (".gitignore", ".gitattributes"):
            data = data.replace(b"\r\n", b"\n")
        manifest.append({"path": name, "sha256": hashlib.sha256(data).hexdigest()})
    return {"status": "passed", "metadataGuids": len(guids), "assemblies": len(assemblies), "localLinks": links,
            "hashNormalization": "CRLF to LF for source text; binary bytes unchanged", "files": manifest}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--include-local-art", action="store_true")
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError("Use a new source report.")
    report = verify(Path(__file__).resolve().parent.parent, args.include_local_art)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("SOURCE_CHECKS_PASSED metadata=" + str(report["metadataGuids"]) + " assemblies=" + str(report["assemblies"]) + " links=" + str(report["localLinks"]))
