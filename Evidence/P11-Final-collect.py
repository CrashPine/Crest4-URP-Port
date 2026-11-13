"""Validate completed P11.4 evidence and refresh the normal-build checkpoint."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import re
from xml.etree import ElementTree
from zipfile import ZipFile, ZIP_DEFLATED


ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "Evidence"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def record(path):
    path = ROOT / path
    return {
        "path": path.relative_to(ROOT).as_posix(),
        "sha256": sha256(path.read_bytes()).hexdigest().upper(),
        "bytes": path.stat().st_size,
        "modifiedUtc": datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat(),
    }


def log_text(path):
    return path.read_text(encoding="utf-8", errors="replace")


def refresh(row, path):
    return {**row, **{key: value for key, value in record(path).items() if key != "path"}}


def main():
    operation = read_json(EVIDENCE / "P11-Final-operation.json")
    assert operation["state"] == "complete" and operation["step"] == "All commands", operation
    steps = {row["step"]: datetime.fromisoformat(row["startedUtc"]) for row in operation["history"]}
    expected = [f"{mode} {api}" for mode in ("Full", "Interactive") for api in ("DX11", "DX12")]
    expected += ["Restore known fixture material normalization", "ValidateAssets", "Build Development", "Build Release"]
    expected += [f"Smoke {label} {api}" for label in ("Development", "Release") for api in ("DX11", "DX12")]
    assert list(steps) == expected, steps

    def fresh(path, step):
        result = record(path)
        assert datetime.fromisoformat(result["modifiedUtc"]) >= steps[step], (path, step)
        return result

    baseline = EVIDENCE / "P11-Final-before.zip"
    with ZipFile(baseline) as before:
        checkpoint = json.loads(before.read("Evidence/Build-checkpoint.json"))
    log_paths = [EVIDENCE / "P11-Final-operation.json", EVIDENCE / "P11-Final.stdout.log", EVIDENCE / "P11-Final.stderr.log"]
    reports = []
    for api in ("DX11", "DX12"):
        for interactive, expected_count in ((False, 121), (True, 2)):
            name = f"EditMode-{api}" + ("-interactive" if interactive else "")
            path = EVIDENCE / f"{name}.xml"
            run = ElementTree.parse(path).getroot()
            cases = run.findall(".//test-case")
            assert run.get("result") == "Passed" and len(cases) == expected_count, (path, run.attrib)
            assert int(run.get("total")) == expected_count and int(run.get("passed")) == expected_count, run.attrib
            assert int(run.get("failed")) == int(run.get("skipped")) == 0, run.attrib
            assert all(case.get("result") == "Passed" for case in cases), path
            step = f"{'Interactive' if interactive else 'Full'} {api}"
            log = EVIDENCE / f"{name}.log"
            log_paths.extend([log, path])
            text = log_text(log)
            reports.append({**fresh(path, step), "api": api, "interactive": interactive, "total": expected_count,
                            "result": "Passed", "warnings": warning_counts(text)})
            assert "error CS" not in text and "RenderData validation failed" not in text, log
    checkpoint["testReports"] = reports
    validation = EVIDENCE / "ValidateAssets-DX11.log"
    assert "CREST_ASSET_VALIDATION: no reported asset errors." in log_text(validation), validation
    checkpoint["assetValidation"] = fresh(validation, "ValidateAssets")
    log_paths.append(validation)
    checkpoint["buildLogs"] = []
    for label in ("Development", "Release"):
        log = EVIDENCE / f"{label}-DX11.log"
        text = log_text(log)
        assert "Result: Success" in text or "Build completed with a result of 'Succeeded'" in text, log
        checkpoint["buildLogs"].append({**fresh(log, f"Build {label}"), "warnings": warning_counts(text)})
        log_paths.append(log)

    for smoke in checkpoint["smokes"]:
        label, api = smoke["configuration"], smoke["graphicsAPI"]
        step = f"Smoke {label} {api}"
        directory = EVIDENCE / f"Player-{label}-{api}"
        report = directory / "player-smoke.txt"
        text = log_text(report)
        assert re.search(r"^Errors=0\.", text, re.M), report
        captures = re.findall(r"^CaptureFrame: scene=([^;]+); frame=(\d+); lodFrame=(\d+)", text, re.M)
        assert len(captures) == 7 and all(frame == lod for _, frame, lod in captures), report
        images = sorted(directory.glob("Ocean_*.png")) + sorted(directory.glob("ReflectionOptions-*.png"))
        assert len(images) == 11, directory
        log = directory / "Player.log"
        log_output = log_text(log)
        assert "RenderData validation failed" not in log_output, log
        smoke.update(report=fresh(report, step), playerLog=fresh(log, step),
                     images=[fresh(image, step) for image in images],
                     reflectionReport=fresh(directory / "reflection-options.txt", step), warnings=warning_counts(log_output))
        log_paths.extend([log, report, directory / "reflection-options.txt", *images])

    for path in (
        "Assets/Crest/Crest/Scripts/Helpers/OceanDebugGUI.cs",
        "Assets/Crest/Crest/Scripts/LodData/OceanDepthCache.cs",
        "Assets/Crest/Crest-Examples/BoatDev/Scripts/BoatAlignNormal.cs",
        "Assets/Crest/Crest/Scripts/Interaction/BoatProbes.cs",
        "ProjectSettings/GraphicsSettings.asset",
    ):
        if not any(row["path"] == path for row in checkpoint["runtimeSources"]):
            checkpoint["runtimeSources"].append({"path": path})
    checkpoint["runtimeSources"] = [refresh(row, row["path"]) for row in checkpoint["runtimeSources"]]
    checkpoint["materialSources"] = [refresh(row, row["path"]) for row in checkpoint["materialSources"]]
    for key, field in (("builds", "assembly"), ("packedAssets", "path")):
        checkpoint[key] = [
            refresh(row, f"Builds/{row['configuration']}/{row[field]}") for row in checkpoint[key]
        ]
    foreign = read_json(EVIDENCE / "P11-SavedInputs-foreign-files.json")
    with ZipFile(baseline) as before:
        for row in foreign:
            path = row["path"]
            data = before.read(path) if path.startswith("Evidence/") and path not in {p.relative_to(ROOT).as_posix() for p in log_paths} else (ROOT / path).read_bytes()
            assert sha256(data).hexdigest() == row["sha256"].lower(), path
    checkpoint["recordedUtc"] = datetime.now(timezone.utc).isoformat()
    checkpoint["previousNormalCheckpoint"] = {"path": "Evidence/P11-Final-previous-build.json"}
    checkpoint["validationStatus"] = "P11.4 full 121 plus interactive 2 per API, asset validation, both builds and four Player smokes passed."
    checkpoint["limitation"] = "Acceptance covers the recorded Windows scope. Local Reflection Probe coverage, Player keyboard and physical XR remain outstanding. See TEST_RESULTS for warning and resource outcomes."

    archive = EVIDENCE / "P11-Final-evidence.zip"
    keep = {path.relative_to(ROOT).as_posix() for path in log_paths}
    mutations = []
    created = []
    with ZipFile(baseline) as before, ZipFile(archive, "x", ZIP_DEFLATED) as output:
        initial = set(before.namelist())
        for path in sorted(set(log_paths)):
            output.write(path, path.relative_to(ROOT).as_posix())
        for path in sorted(EVIDENCE.glob("P11-Final-mutated-*")):
            output.write(path, path.relative_to(ROOT).as_posix())
        variant = "Assets/CrestPortDemos/ReflectionVertexVariants.asset"
        original_variant = before.read(variant)
        generated_variant = (ROOT / variant).read_bytes()
        assert original_variant.split() == generated_variant.split(), variant
        if original_variant != generated_variant:
            output.write(ROOT / variant, variant)
        for name in before.namelist():
            path = ROOT / name
            if name in keep or not name.startswith("Evidence/") or not path.is_file():
                continue
            old = before.read(name)
            if path.read_bytes() != old:
                output.write(path, name)
                mutations.append(name)
        for path in sorted(EVIDENCE.iterdir()):
            if path.is_file() and not path.name.startswith("P11-Final-") and path.suffix in (".txt", ".png", ".gz", ".json") and path.stat().st_mtime >= steps["Full DX11"].timestamp():
                name = path.relative_to(ROOT).as_posix()
                if name not in keep and name not in initial:
                    output.write(path, name)
                    created.append(name)
    with ZipFile(archive) as output:
        assert output.testzip() is None
    with ZipFile(baseline) as before:
        for name in mutations:
            (ROOT / name).write_bytes(before.read(name))
        if original_variant != generated_variant:
            (ROOT / variant).write_bytes(original_variant)
        (EVIDENCE / "P11-Final-previous-build.json").write_bytes(before.read("Evidence/Build-checkpoint.json"))
    assert all(record(row["path"])["sha256"].lower() == row["sha256"].lower() for row in foreign)
    checkpoint["evidenceArchive"] = record(archive)
    checkpoint["archivedSideEffects"] = {"restoredHistorical": mutations, "newEvidence": created}
    (EVIDENCE / "Build-checkpoint.json").write_text(json.dumps(checkpoint, indent=2) + "\n", encoding="utf-8")
    print(checkpoint["validationStatus"], f"Archived {len(mutations)} historical output changes and {len(created)} new reports.")


def warning_counts(text):
    return {term: text.count(term) for term in (
        "Persistent allocates", "JobTempAlloc", "evictable", "RenderData validation failed",
        "Releasing render texture that is set to be RenderTexture.active!",
        "D3D12PipelineLibrary must be recreated",
    )}


if __name__ == "__main__":
    main()
