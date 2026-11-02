"""검증 궤적을 확인할 수 있는 로컬 HTML과 근거 문서를 만듭니다. 씬은 읽기만 합니다."""
from pathlib import Path
import csv
import json
import hashlib
from collections import Counter

root = Path.cwd()
out = root / "Docs/Art/PlayableRandomLayout20261029"
design = json.loads((out / "설계데이터.json").read_text(encoding="utf-8"))
proof = json.loads((out / "완주검증.json").read_text(encoding="utf-8"))
capacity = json.loads((out / "최대점프_실측.json").read_text(encoding="utf-8"))
with (out / "완주궤적_60Hz.csv").open(encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))
frames = [[0, -10, 0, 0, 0, 1, 0, 0, 0, "Start"]]
frames += [[int(s["frame"]), float(s["x"]), float(s["y"]), float(s["vx"]), float(s["vy"]), int(s["facing"]),
            int(s["support"]), int(s["jump"]), int(s["batch"]), s["event"]] for s in rows]
data = {"frames": frames, "legs": design["Legs"], "platforms": design["Platforms"], "proof": proof, "capacity": capacity}
html = r'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>일시정지! · 검증된 랜덤 맵 시안</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#0b1020;color:#e8edf7;font:15px/1.65 'Malgun Gothic',sans-serif}main{max-width:1460px;margin:auto;padding:24px}h1{margin:0;font-size:28px;letter-spacing:-1px}p{margin:5px 0 16px;color:#a8b7ce}a{color:#87ebce}header{margin-bottom:20px}.badge{display:inline-block;background:#173b36;color:#8af1d5;border:1px solid #316357;border-radius:7px;padding:4px 11px;margin:6px 6px 0 0}.layout{display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:20px}.card{background:#121c2e;border:1px solid #29384f;border-radius:14px;overflow:hidden}canvas{display:block;width:100%;height:auto;image-rendering:pixelated;background:#09101d}.controls{padding:15px}.buttons{display:flex;align-items:center;gap:10px;flex-wrap:wrap}button,select{font:inherit;color:#e8edf7;background:#243b57;border:1px solid #426181;border-radius:7px;padding:7px 14px;cursor:pointer}button:hover{background:#30506d}input[type=range]{width:100%;accent-color:#6de3c4}label{cursor:pointer}aside .card{padding:17px;margin-bottom:14px}h2{font-size:18px;margin:0 0 12px}dl{margin:0}dt{color:#95abc5;font-size:12px}dd{margin:0 0 10px;font-size:20px;font-weight:bold}.small{font-size:12px;color:#99abc4}.jump{font-variant-numeric:tabular-nums;padding:5px 0;border-bottom:1px solid #253650}.section{margin-top:25px}.section img{width:100%;height:auto;display:block;border-radius:10px}.links{display:flex;gap:20px;flex-wrap:wrap;margin:12px 0}.ok{color:#89ebca}.note{border-left:3px solid #5e87b4;padding-left:11px}#batch select{width:100%}@media(max-width:960px){.layout{grid-template-columns:1fr}aside{display:grid;grid-template-columns:1fr 1fr;gap:10px}aside .card{margin:0}h1{font-size:23px}main{padding:14px}}
</style><main><header><h1>일시정지! · 검증된 랜덤 맵 시안</h1><p>기존 09번 RGB 도트로 만든 24m × 175m 배치안. 아래 화면은 원본 물리 코드의 검증 궤적을 보여주는 뷰어입니다.</p>
<span class="badge">100개 발판 전체 방문</span><span class="badge">99회 도약 · 25개 예약 묶음</span><span class="badge">씬 미적용</span></header>
<div class="layout"><section class="card"><canvas id="view" width="1120" height="760"></canvas><div class="controls"><div class="buttons"><button id="play">재생</button><button id="reset">처음</button><select id="speed" aria-label="재생 속도"><option value=".5">0.5배</option><option selected value="1">1배</option><option value="2">2배</option><option value="4">4배</option></select><label><input id="route" type="checkbox" checked>검증 경로</label><span id="time"></span></div><input id="seek" type="range" min="0" step="1" value="0" aria-label="검증 궤적 탐색"><div class="small">ESC 입력·충전 중 정지 시간은 화면에서 생략합니다. 타이머 검증에는 그 시간도 포함했습니다. Unity 플레이 녹화가 아닙니다.</div></div></section>
<aside><div class="card"><h2>현재 위치</h2><dl><dt>높이</dt><dd id="height">0.0m / 175m</dd><dt>도약 / 묶음</dt><dd id="jump">0 / 0</dd><dt>발판 폭 구성</dt><dd style="font-size:15px">0–55m: 4·6칸<br>55–115m: 4칸<br>115m 이상: 2·4칸</dd></dl><p class="small">같은 Y의 발판 그룹 15개. 벽에서 실제로 튕겨야 방향이 바뀝니다. 공중 방향 조작과 중간 텔레포트는 쓰지 않았습니다.</p></div><div class="card" id="batch"><h2>예약 입력 보기</h2><select id="jumpSelect" aria-label="도약으로 이동"></select><div id="commandList"></div></div><div class="card"><h2>검증 범위</h2><p class="small">원본 물리 C# 코드 + Unity 수학 DLL의 별도 60Hz 실행. 일반·기름·금간·타이머 접촉, 관성, 외벽 반사, 예약 제한을 포함합니다.</p><p class="small ok">각 도약만 ±2프레임 다르게 누른 개별 검사 198/198 완주.</p><p class="small note">모든 도약에 오차가 동시에 누적되면 실패할 수 있습니다. 마지막 한 번은 현재 게임의 옥상 중앙 안내 규칙을 사용합니다.</p></div></aside></div>
<div class="links"><a href="검증된_랜덤맵_전체.png">전체 지도 원본</a><a href="검증된_랜덤맵_완주경로.png">전체 완주 경로</a><a href="배치좌표.csv">발판 좌표 CSV</a><a href="완주입력.csv">충전 시간 CSV</a><a href="README.md">설계·검증 문서</a></div>
<section class="section"><h2>하단 → 중단 → 상단</h2><img src="검증된_랜덤맵_하중상.png" alt="검증에 사용한 동일한 한 개 맵의 세 구간 비교"></section></main>
<script>
const DATA = __DATA__;
const frames=DATA.frames, canvas=document.getElementById('view'),ctx=canvas.getContext('2d');
const seek=document.getElementById('seek'),playButton=document.getElementById('play');
const map=new Image(), player=new Image(); map.src='검증된_랜덤맵_전체.png'; player.src='preview_sprites/Player.png';
let index=0,playing=false,lastTime=0,accumulator=0; seek.max=frames.length-1;
const firstJump=new Map(); frames.forEach((f,i)=>{if(!firstJump.has(f[7]))firstJump.set(f[7],i)});
const select=document.getElementById('jumpSelect');
DATA.legs.forEach((leg,i)=>{const o=document.createElement('option');o.value=i+1;o.textContent=`${String(i+1).padStart(2,'0')}번 도약 · ${leg.HoldFrames}프레임 충전`;select.appendChild(o)});
function draw(){
 const f=frames[Math.min(index,frames.length-1)];ctx.imageSmoothingEnabled=false;
 const worldWidth=28,scale=canvas.width/worldWidth,worldHeight=canvas.height/scale;
 const cy=Math.min(175-worldHeight/2+2,Math.max(worldHeight/2-1,f[2]+3));const top=cy+worldHeight/2;
 ctx.fillStyle='#0a1428';ctx.fillRect(0,0,canvas.width,canvas.height);
 if(map.complete&&map.naturalWidth)ctx.drawImage(map,0,0,map.width,map.height,0,(top-178)*scale,map.width/32*scale,map.height/32*scale);
 const xy=(x,y)=>[(x+14)*scale,(top-y)*scale];
 if(document.getElementById('route').checked){ctx.strokeStyle='#58e4c697';ctx.lineWidth=2;ctx.beginPath();let started=false;for(let i=0;i<frames.length;i+=2){const p=frames[i];if(Math.abs(p[2]-cy)>worldHeight)continue;const q=xy(p[1],p[2]);if(!started){ctx.moveTo(...q);started=true}else ctx.lineTo(...q)}ctx.stroke()}
 const q=xy(f[1],f[2]);ctx.save();ctx.translate(q[0],q[1]);ctx.scale(f[5],1);if(player.complete&&player.naturalWidth)ctx.drawImage(player,-.75*scale,-1.35*scale,1.5*scale,1.5*scale);ctx.restore();
 ctx.fillStyle='#ffde92';ctx.beginPath();ctx.arc(q[0],q[1],3,0,Math.PI*2);ctx.fill();
 ctx.fillStyle='#07101fdc';ctx.fillRect(18,18,345,75);ctx.fillStyle='#eaf9f5';ctx.font="bold 23px 'Malgun Gothic'";ctx.fillText(f[2]>=175?'옥상 중앙 도달':`${f[2].toFixed(1)}m · ${f[5]>0?'오른쪽':'왼쪽'}`,32,48);ctx.fillStyle='#a0b8d3';ctx.font="15px 'Malgun Gothic'";ctx.fillText('별도 물리 모델 궤적 · 실제 씬 미적용',32,77);
 document.getElementById('height').textContent=`${f[2].toFixed(1)}m / 175m`;document.getElementById('jump').textContent=`${f[7]} / ${f[8]}`;document.getElementById('time').textContent=`${(f[0]/60).toFixed(1)}초 / ${(frames.at(-1)[0]/60).toFixed(1)}초`;
 seek.value=index;select.value=Math.max(1,f[7]);
 const current=Math.max(0,f[7]-1),start=current-current%4,end=Math.min(start+4,DATA.legs.length);
 document.getElementById('commandList').innerHTML=DATA.legs.slice(start,end).map((l,j)=>`<div class="jump" style="color:${start+j===current?'#83ebcf':'#bac8dc'}">${start+j+1}번 · ${(l.HoldFrames/60).toFixed(3)}초 → 발판 ${l.To===998?'옥상':l.To===997?'진입':l.To}</div>`).join('');
}
function tick(t){if(playing&&lastTime){accumulator+=(t-lastTime)*.06*Number(document.getElementById('speed').value);if(accumulator>=1){const n=Math.floor(accumulator);index=Math.min(index+n,frames.length-1);accumulator-=n;if(index===frames.length-1){playing=false;playButton.textContent='다시 재생'}draw()}}lastTime=t;requestAnimationFrame(tick)}
playButton.onclick=()=>{if(index===frames.length-1)index=0;playing=!playing;playButton.textContent=playing?'일시정지':'재생';lastTime=0;draw()};
document.getElementById('reset').onclick=()=>{index=0;playing=false;playButton.textContent='재생';draw()};seek.oninput=()=>{index=Number(seek.value);draw()};
select.onchange=()=>{index=firstJump.get(Number(select.value))||0;draw()};document.getElementById('route').onchange=draw;
map.onload=draw;player.onload=draw;draw();requestAnimationFrame(tick);
</script></html>'''
(out / "미리보기.html").write_text(html.replace("__DATA__", json.dumps(data, ensure_ascii=False, separators=(",", ":"))), encoding="utf-8")

platforms = design["Platforms"]
same_y = [y for y in sorted({p["Y"] for p in platforms}) if sum(p["Y"] == y for p in platforms) > 1]
band_counts = []
for title, low, high in [("초반", 0.01, 55), ("중반", 55, 115), ("후반", 115, 174)]:
    counts = Counter(int(p["Width"]) for p in platforms if low <= p["Y"] < high)
    band_counts.append(f'| {title} | {low if low > 1 else 0:g}~{high:g}m | {counts[2]} | {counts[4]} | {counts[6]} |')
kinds = Counter(p["Kind"] for p in platforms)
readme = f'''# 일시정지! 도달 가능한 랜덤 맵 시안

실제 씬에 적용하지 않은 **한 개의 완성된 24m × 175m 배치안**입니다. 고정 시드: `{design["Seed"]}`.

사용자가 에디터를 닫겠다고 한 뒤에는 Unity 에디터를 호출하거나 실행하지 않았습니다. 이 폴더의 이미지·문서·좌표·검증 프로그램만 만들었습니다. 배경은 배치를 알아보기 위한 기존 RGB 도트 합성입니다. 현재 씬 화면을 촬영한 결과가 아닙니다.

## 배치

총 **100개 발판**: 바닥 1, 등반 발판 98, 옥상 1. **99회 도약으로 모든 발판을 방문**합니다. 같은 Y의 발판 그룹이 **{len(same_y)}개** 있습니다. 가로 위치, 층간 거리, 같은 층의 이동, 재질, 길이를 무작위 후보로 정하고 실제 물리 재생을 통과한 후보만 채택했습니다.

| 구간 | 높이 | 짧은 2칸 | 중간 4칸 | 긴 6칸 |
|---|---|---:|---:|---:|
{chr(10).join(band_counts)}

바닥과 정상은 별도의 24칸 연결 표면입니다. 초반에 2칸 발판은 없고 55m 이후에 6칸 발판은 없습니다. 일반 계열 {kinds["Stone"] + kinds["Goal"]}개(바닥·옥상 포함), 기름 {kinds["Ice"]}개, 금간 {kinds["Cloud"]}개, 타이머 {kinds["Crumble"]}개입니다.

## 원본 물리로 확인한 내용

- `PhysicsStudy.csproj`가 **Assets의 원본 7개 C# 파일을 직접 링크**합니다. 물리를 다른 언어로 다시 구현하거나 소스를 복사·수정하지 않았습니다. Unity 6000.3.22f1의 `UnityEngine.CoreModule.dll`에 있는 Vector2/Mathf를 사용합니다.
- MainScene 파일에서 `_movement`의 현재 Inspector 수치를 텍스트로 읽습니다. 점프 충전 시간 0.85초, 수직 속도 6~12.5m/s, 수평 속도 3.2~6.4m/s, 중력 22m/s², 벽 반동 0.82, 일반/기름 마찰 32/0.65입니다.
- 연속식 최대 상승 높이는 {capacity["continuousFormulaHeight"]:.3f}m, 같은 높이까지 거리는 {capacity["continuousFormulaRange"]:.3f}m입니다. **실제 60Hz 코드 실측은 상승 {capacity["measured60HzPeak"]:.3f}m / 거리 {capacity["measured60HzFlatRange"]:.3f}m**여서 배치 판정에는 실제 고정 시간 코드를 사용했습니다.
- 수직·수평 속도가 같은 충전량에 결합되는 포물선, 0.22m 몸 반폭, 착지 후 관성, 외벽 충돌, 금간 0.3초, 타이머 3초를 포함합니다. 모든 수치는 원본 코드가 계산합니다.
- 출발 `(-10, 0)`부터 정상 `(0, 175)`까지 **초기화·순간이동·직접 방향 변경 없이** 재생합니다. 방향 전환은 실제 양옆 외벽에서 20회 반동하여 일어납니다. 중간 반사벽은 넣지 않았습니다.
- 25개 예약 묶음, 최대 4개 도약씩(마지막 묶음은 나머지 도약). 12개 예약 제한을 지킵니다. 금간 발판에서는 새 녹화를 시작하지 않습니다. 묶음 끝 일반 발판에서 18프레임 관성 감속 후 다음 묶음을 녹화합니다.
- ESC 녹화 중에는 물리는 정지하지만 **타이머 발판의 실제 시간은 계속 갱신**합니다. 각 충전 시간, 점프 사이 6프레임 입력 여유, 묶음당 18프레임 준비 시간도 타이머 검증에 넣었습니다.
- 완성된 전체 발판 목록과 저장된 JSON을 다시 읽어 독립 재생했습니다. 나중에 생긴 발판에 조기 착지하여 경로가 달라지는지도 검사했습니다.

## 결과와 한계

**정상 도달 PASS.** {proof["Frames"]} 물리 프레임, 재생 {proof["simulatedSeconds"]:.2f}초, 별도 녹화·준비 시간 {proof["recordingSeconds"]:.2f}초. 모든 100개 발판을 방문했습니다. 기본 입력의 착지 시 발 중심과 가장 가까운 발판 끝 사이 최소 여유는 {proof["MinimumFootMargin"]:.3f}m입니다(몸 반폭 0.22m를 빼면 약 {proof["MinimumFootMargin"]-.22:.3f}m).

각 도약 **하나만** 2프레임(약 0.033초) 짧게 또는 길게 누르고 나머지 입력을 유지하는 검사는 **198/198 완주**했습니다. 이 검사는 각 후보에서 재출발하지 않고 매번 출발점부터 끝까지 재생했습니다.

모든 도약에 동일한 오차가 계속 누적되는 4개 추가 검사는 실패했습니다. 따라서 모든 입력을 대충 눌러도 완주하는 맵이라는 의미는 아닙니다. 충전량을 확인·조정하며 진행하는 게임 방식이 전제입니다. 상세 결과를 `완주검증.json`에 그대로 남겼습니다.

옥상 직전 높이 173.25m 발판에서 마지막 도약은 **현재 게임에 이미 있는 옥상 중앙 안내 규칙**(1.05초, 추가 곡선 높이 1.8m)을 사용합니다. 그 이전의 98회 도약은 일반 점프와 외벽 반동으로 검증했습니다. 완주의 범위는 옥상 중앙 도착이며 엔딩 UI·크레딧은 이 배치안의 검증 대상에 포함하지 않았습니다.

이 결과는 **원본 물리 코드의 독립 실행 검증**입니다. Unity Play 모드, 사람 플레이, Android 실기기 검증은 아닙니다. 에디터를 사용하지 않고 검증했다는 점을 분명히 구분합니다.

## 보기 및 재현

- `미리보기.html`: 슬라이더·배속·도약 선택이 있는 물리 궤적 뷰어. 같은 배치의 전체 지도와 하·중·상 사진을 포함합니다.
- `검증된_랜덤맵_하중상.png`: 한 개 코스의 세 구간 비교 사진.
- `검증된_랜덤맵_전체.png`: 1칸=32px, 896×5744 원본 지도.
- `검증된_랜덤맵_완주경로.png`: 실제 재생 궤적과 발판 ID를 겹친 지도.
- `배치좌표.csv`, `완주입력.csv`, `완주궤적_60Hz.csv`: 좌표, 예약 입력, 매 프레임 상태.
- `씬변경없음_확인.json`: 작업 전후 16개 씬과 사용한 물리 소스의 해시 비교.

프로젝트 루트에서 `.NET 10 SDK`로 저장된 한 개 시안을 다시 검증합니다. 에디터를 실행하지 않습니다. 빌드 캐시는 프로젝트 밖 `GamePaused-StudyCache`에 둡니다.

```powershell
dotnet run --project Docs/Art/PlayableRandomLayout20261029/PhysicsStudy/PhysicsStudy.csproj -c Release -- --replay
```

`--replay` 없이 실행하면 새로운 탐색을 수행하여 이 폴더의 제안 파일을 다시 씁니다. 현재 사진과 좌표를 보존하려면 `--replay`를 사용하세요.
'''
(out / "README.md").write_text(readme, encoding="utf-8")

# 사용자가 요청한 씬 무변경 조건과 실제로 링크한 소스 무변경 조건을 해시로 확인합니다.
before = json.loads((out / "씬해시_작업전.json").read_text(encoding="utf-8-sig"))
scene_results = []
for entry in before:
    path = Path(entry["Path"])
    after = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    scene_results.append({"path": str(path), "before": entry["Hash"], "after": after, "unchanged": entry["Hash"] == after})
source_before = json.loads((out / "검증에_사용한_원본코드_해시.json").read_text(encoding="utf-8"))
source_results = [{"path": name, "unchanged": hashlib.sha256((root / name).read_bytes()).hexdigest().upper() == digest} for name, digest in source_before.items()]
audit = {"all16SceneFilesUnchanged": len(scene_results) == 16 and all(s["unchanged"] for s in scene_results), "linkedPhysicsSourcesUnchanged": all(s["unchanged"] for s in source_results), "unityEditorUsedAfterUserClosureInstruction": False, "scenes": scene_results, "physicsSources": source_results}
if not audit["all16SceneFilesUnchanged"] or not audit["linkedPhysicsSourcesUnchanged"]:
    raise RuntimeError("씬 또는 원본 물리 소스가 작업 중 바뀌었습니다.")
(out / "씬변경없음_확인.json").write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"sceneAudit": audit["all16SceneFilesUnchanged"], "sourceAudit": audit["linkedPhysicsSourcesUnchanged"], "sameYGroups": len(same_y), "sameYHeights": same_y, "kinds": kinds, "frames": len(frames)}, ensure_ascii=False, indent=2))
