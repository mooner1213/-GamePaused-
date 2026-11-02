"""씬 변경 범위와 커밋 자산·메타·참조 누락을 검사합니다. 프로젝트 파일은 수정하지 않습니다."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

root = Path.cwd()
out = root / "Docs/Validation/RandomStage09_20261102"
before = json.loads((out / "ScenesBefore.json").read_text(encoding="utf-8-sig"))
scenes = []
for item in before:
    path = Path(item["Path"])
    digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    scenes.append({"path": path.relative_to(root).as_posix(), "unchanged": digest == item["Hash"], "after": digest})
changed = [s["path"] for s in scenes if not s["unchanged"]]
expected = ["Assets/Scenes/DesignStages/Stage09_RGBKeyboard.unity"]
if changed != expected:
    raise RuntimeError(f"승인되지 않은 씬 변경: {changed}")
(out / "SceneScope.json").write_text(json.dumps({"onlyStage09Changed": True, "scenes": scenes}, ensure_ascii=False, indent=2), encoding="utf-8")

files = subprocess.check_output(["git", "diff", "--cached", "--name-only", "-z"], cwd=root).decode().split("\0")
files = [p for p in files if p]
missing_meta, orphan_meta, forbidden, oversized, missing_guid = [], [], [], [], []
guid_map = {}
# 등록된 패키지의 스크립트 GUID도 해결하되 캐시 자체는 커밋 목록에서 금지합니다.
package_paths = json.loads((root / "Docs/Validation/PixelUI20261102/PackagePaths.json").read_text(encoding="utf-8-sig"))
for base in [root / "Assets", root / "Packages"] + [Path(p) for p in package_paths]:
    for meta in base.rglob("*.meta"):
        text = meta.read_text(encoding="utf-8-sig", errors="replace")[:500]
        match = re.search(r"^guid: ([0-9a-f]{32})\r?$", text, re.M)
        if match:
            guid_map[match[1]] = str(meta)
for name in files:
    path = root / name
    if not path.exists():
        continue
    if name.startswith(("Library/", "Temp/", "Logs/", "Builds/", "UserSettings/")) or "/obj/" in name or path.suffix in (".dll", ".exe", ".apk", ".aab", ".pdb"):
        forbidden.append(name)
    if path.stat().st_size > 50 * 1024 * 1024:
        oversized.append(name)
    if not name.startswith("Assets/"):
        continue
    if path.suffix == ".meta":
        if not path.with_suffix("").exists():
            orphan_meta.append(name)
        continue
    if not Path(str(path) + ".meta").exists():
        missing_meta.append(name)
    if path.suffix in (".unity", ".asset", ".prefab", ".mat"):
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        for guid in set(re.findall(r"guid: ([0-9a-f]{32})", text)):
            if guid.startswith("0000000000000000") or guid in guid_map:
                continue
            missing_guid.append({"file": name, "guid": guid})
report = {"stagedCount": len(files), "missingMeta": missing_meta, "orphanMeta": orphan_meta, "forbidden": forbidden,
          "oversized": oversized, "missingGuidReferences": missing_guid}
report["passed"] = not any((missing_meta, orphan_meta, forbidden, oversized, missing_guid))
(out / "CommitAssetAudit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({k: v for k, v in report.items() if k != "missingGuidReferences"}, ensure_ascii=False))
if not report["passed"]:
    raise RuntimeError("커밋 자산 검사 실패. CommitAssetAudit.json 참조")
