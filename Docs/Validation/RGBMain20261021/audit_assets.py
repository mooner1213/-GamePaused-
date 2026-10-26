"""Unity 자산의 메타 누락·GUID 중복과 최신 검증 보고서·미리보기 수를 읽기 전용으로 확인합니다."""
from collections import defaultdict
from pathlib import Path
import hashlib
import json
import re


def main():
    # Library·백업을 탐색하지 않고 실제 Assets 아래의 파일과 폴더만 확인합니다.
    root = Path(__file__).resolve().parents[3]
    assets = root / "Assets"
    guids = defaultdict(list)
    missing = []
    for path in assets.rglob("*"):
        if path.name.startswith(".") or path.suffix == ".meta":
            continue
        if not Path(str(path) + ".meta").exists():
            missing.append(path.relative_to(root).as_posix())
    for path in assets.rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})\r?$", path.read_text(encoding="utf-8-sig"), re.M)
        if match:
            guids[match.group(1)].append(path.relative_to(root).as_posix())
    duplicates = {guid: paths for guid, paths in guids.items() if len(paths) > 1}
    # 16씬이 실제 저장되어 있고 현재 검증 문서가 모두 성공인지 확인합니다.
    report_dir = Path(__file__).parent
    reports = {name: json.loads((report_dir / name).read_text(encoding="utf-8-sig"))
               for name in ["DesignValidation.json", "Runtime/RuntimeValidation.json", "ExpansionValidation.json", "FeedbackValidation.json"]}
    scenes = [assets / "Scenes/MainScene.unity", *sorted((assets / "Scenes/DesignStages").glob("*.unity"))]
    previews = list((root / "Docs/Art/StageThemes20261020/Previews").glob("*.png"))
    status_ok = all(data["status"] in ("PASS", "MODEL_AND_SCENE_PASS") for data in reports.values())
    result = {"status": "PASS" if not missing and not duplicates and status_ok and len(scenes) == 16 and len(previews) == 45 else "FAIL",
              "missingMeta": missing, "duplicateGuids": duplicates, "guidCount": len(guids),
              "previewCount": len(previews), "reports": {name: data["status"] for name, data in reports.items()},
              "savedSceneSha256": {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in scenes}}
    (report_dir / "AssetAudit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key != "savedSceneSha256"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
