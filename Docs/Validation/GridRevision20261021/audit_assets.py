"""실제 Assets의 GUID·메타와 현재 보고서·저장 씬을 읽어 최종 자산 검증을 기록합니다."""
from collections import defaultdict
from pathlib import Path
import hashlib
import json
import re


def main():
    # 원본이나 Unity 자산에는 쓰지 않고 이 검증 폴더에만 결과를 저장합니다.
    report_dir = Path(__file__).resolve().parent
    root = report_dir.parents[2]
    guids = defaultdict(list)
    missing = []
    for path in (root / "Assets").rglob("*"):
        if path.name.startswith(".") or path.suffix == ".meta":
            continue
        if not Path(str(path) + ".meta").exists():
            missing.append(path.relative_to(root).as_posix())
    for path in (root / "Assets").rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})\r?$", path.read_text(encoding="utf-8-sig"), re.M)
        if match:
            guids[match[1]].append(path.relative_to(root).as_posix())
    duplicates = {guid: files for guid, files in guids.items() if len(files) > 1}
    names = ["Validation.json", "Runtime/RuntimeValidation.json", "ExpansionValidation.json", "FeedbackValidation.json"]
    reports = {name: json.loads((report_dir / name).read_text(encoding="utf-8-sig")) for name in names}
    scenes = [root / "Assets/Scenes/MainScene.unity", *sorted((root / "Assets/Scenes/DesignStages").glob("*.unity"))]
    previews = list((root / "Docs/Art/StageThemes20261020/Previews").glob("*.png"))
    passed = all(item["status"] in ("PASS", "MODEL_AND_SCENE_PASS") for item in reports.values())
    result = {"status": "PASS" if passed and not duplicates and not missing and len(scenes) == 16 and len(previews) == 45 else "FAIL",
              "missingMeta": missing, "duplicateGuids": duplicates, "guidCount": len(guids), "sceneCount": len(scenes), "previewCount": len(previews),
              "reports": {name: item["status"] for name, item in reports.items()},
              "sceneSha256": {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in scenes}}
    (report_dir / "AssetAudit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key != "sceneSha256"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
