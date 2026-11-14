"""Package validated Windows binaries under a versioned filename."""
import argparse
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("revision")
    parser.add_argument("--expected-tests", type=int, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    builds = root / "Builds"
    evidence = root / "Evidence"
    checkpoint = json.loads((evidence / "Build-checkpoint.json").read_text())
    assert checkpoint["validationStatus"].startswith(f"P11.4 full {args.expected_tests} plus interactive 2 per API")
    for row in checkpoint["builds"] + checkpoint["packedAssets"]:
        path = builds / row["configuration"] / (row.get("assembly") or row["path"])
        assert digest(path) == row["sha256"].lower(), path
    for row in checkpoint["runtimeSources"]:
        assert digest(root / row["path"]) == row["sha256"].lower(), row["path"]
    packages = []
    for config in ("Development", "Release"):
        source = builds / config
        files = {
            path.relative_to(source).as_posix(): path
            for path in sorted(source.rglob("*"))
            if path.is_file() and not any(part.endswith("_ButDontShipItWithYourGame") for part in path.relative_to(source).parts)
        }
        assert "CrestPort.exe" in files and "UnityPlayer.dll" in files
        manifest = {name: digest(path) for name, path in files.items()}
        archive = builds / f"Crest4-URP-Windows-{config}-{args.revision}.zip"
        with ZipFile(archive, "x", ZIP_DEFLATED, compresslevel=3) as output:
            for name, path in files.items():
                output.write(path, name)
            for name in ("LICENSE.Crest", "THIRD_PARTY_NOTICES.md"):
                output.write(root / name, name)
            output.writestr("SHA256.json", json.dumps(manifest, indent=2) + "\n")
        with ZipFile(archive) as output:
            for name, expected in manifest.items():
                with output.open(name) as stream:
                    assert hashlib.file_digest(stream, "sha256").hexdigest() == expected, name
        packages.append({"configuration": config, "path": archive.relative_to(root).as_posix(),
                         "files": len(files), "bytes": archive.stat().st_size, "sha256": digest(archive)})
    report = evidence / f"P12-Windows-packages-{args.revision}.json"
    report.write_text(json.dumps(packages, indent=2) + "\n", encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
