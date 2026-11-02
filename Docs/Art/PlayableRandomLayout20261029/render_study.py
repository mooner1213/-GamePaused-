"""검증에 사용한 좌표와 기존 팔레트 도트를 합성합니다. Unity/Assets/씬에는 쓰지 않습니다."""
from pathlib import Path
import csv
import json
import re
import math
from PIL import Image, ImageDraw, ImageFont, ImageEnhance

ROOT = Path.cwd()
OUT = ROOT / "Docs/Art/PlayableRandomLayout20261029"
SPRITES = OUT / "preview_sprites"
SPRITES.mkdir(exist_ok=True)
GUIDS = {}
for meta in (ROOT / "Assets").rglob("*.meta"):
    text = meta.read_text(encoding="utf-8-sig", errors="replace")[:160]
    match = re.search(r"^guid: ([0-9a-f]{32})", text, re.M)
    if match:
        GUIDS[match[1]] = meta.with_suffix("")


def read_sprite(asset):
    """Sprite 자산의 원본 사각형과 텍스처를 읽어 정확히 같은 도트를 추출합니다."""
    text = Path(asset).read_text(encoding="utf-8-sig")
    rect = re.search(r"m_Rect:\s+serializedVersion: \d+\s+x: ([\d.]+)\s+y: ([\d.]+)\s+width: ([\d.]+)\s+height: ([\d.]+)", text)
    guid = re.search(r"    texture: \{fileID: \d+, guid: ([0-9a-f]{32})", text).group(1)
    x, y, w, h = [int(float(v)) for v in rect.groups()]
    texture_path = GUIDS[guid]
    if texture_path.suffix == ".asset":
        raw = texture_path.read_text(encoding="utf-8-sig")
        tw = int(re.search(r"  m_Width: (\d+)", raw).group(1))
        th = int(re.search(r"  m_Height: (\d+)", raw).group(1))
        texture_format = int(re.search(r"  m_TextureFormat: (\d+)", raw).group(1))
        if texture_format != 4:
            raise ValueError(f"RGBA32가 아닌 텍스처: {texture_path}")
        pixels = bytes.fromhex(re.search(r"  _typelessdata: ([0-9a-f]+)", raw).group(1))
        texture = Image.frombytes("RGBA", (tw, th), pixels[:tw*th*4]).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    else:
        texture = Image.open(texture_path).convert("RGBA")
    return texture.crop((x, texture.height - y - h, x + w, texture.height - y))


grid = ROOT / "Assets/Art/RGBMain20261021/Grid32/09_RGBKeyboard"
longs = ROOT / "Assets/Art/PlatformLengths20261026/09_RGBKeyboard"
names = {"Stone": "일반 발판", "Ice": "기름 발판", "Cloud": "금간 발판", "Crumble": "타이머 발판"}
images = {}
manifest = {}
for kind, name in names.items():
    for width in (2, 4, 6):
        path = grid / name / "Full.asset" if width == 4 else longs / f"{name} · {'짧은 2칸' if width == 2 else '긴 6칸'}" / "Full.asset"
        image = read_sprite(path)
        key = f"{kind}_{width}"
        image.save(SPRITES / f"{key}.png")
        images[key] = image
        manifest[key] = {"source": str(path.relative_to(ROOT)), "width": image.width, "height": image.height}
for key, name in {"Facade": "성 내부_FacadePlain", "Circuit": "성 내부_FacadeCircuit", "Vent": "성 내부_FacadeVent",
                  "Window": "창틀_투명한 창문", "Wall": "연결벽_Repeat", "WallTop": "연결벽_WallTop"}.items():
    path = grid / name / "Full.asset"
    images[key] = read_sprite(path)
    images[key].save(SPRITES / f"{key}.png")
    manifest[key] = {"source": str(path.relative_to(ROOT)), "width": images[key].width, "height": images[key].height}
idle = Image.open(ROOT / "Assets/Resources/Characters/HumanPlayer/Idle/Idle_Right.png").convert("RGBA")
idle.save(SPRITES / "Player.png")
manifest["Player"] = {"source": "Assets/Resources/Characters/HumanPlayer/Idle/Idle_Right.png", "width": idle.width, "height": idle.height}
(OUT / "그림_원본참조.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

font = lambda size, bold=False: ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf" if bold else "C:/Windows/Fonts/malgun.ttf", size)
swatches = Image.new("RGB", (960, 800), "#111827")
draw = ImageDraw.Draw(swatches)
for i, (key, im) in enumerate(images.items()):
    x, y = (i % 3) * 320 + 14, (i // 3) * 125 + 20
    draw.text((x, y), key + f" {im.size}", fill="white", font=font(16))
    copy = im.copy()
    copy.thumbnail((280, 86), Image.Resampling.NEAREST)
    swatches.paste(copy, (x, y + 30), copy)
swatches.save(OUT / "사용도트_확인.png")

SCALE = 32
LEFT, RIGHT, BOTTOM, TOP = -14.0, 14.0, -1.5, 178.0
W, H = round((RIGHT - LEFT) * SCALE), round((TOP - BOTTOM) * SCALE)


def point(x, y):
    return round((x - LEFT) * SCALE), round((TOP - y) * SCALE)


def place(canvas, image, x, top, width=None, height=None):
    """월드의 왼쪽 위 좌표에 1칸=32픽셀 비율로 도트를 배치합니다."""
    if width is not None:
        height = width * image.height / image.width if height is None else height
        image = image.resize((round(width * SCALE), round(height * SCALE)), Image.Resampling.NEAREST)
    canvas.alpha_composite(image, point(x, top))


design = json.loads((OUT / "설계데이터.json").read_text(encoding="utf-8"))
platforms = design["Platforms"]
with (OUT / "완주궤적_60Hz.csv").open(encoding="utf-8-sig", newline="") as handle:
    trace = list(csv.DictReader(handle))
proof = json.loads((OUT / "완주검증.json").read_text(encoding="utf-8"))

# 창문 너머와 탑 바깥의 하늘만 합성합니다. 물리 배치와 별개인 검토용 배경입니다.
canvas = Image.new("RGBA", (W, H))
draw = ImageDraw.Draw(canvas)
for py in range(H):
    world_y = TOP - py / SCALE
    t = min(1, max(0, world_y / 175))
    low, high = (92, 167, 229), (225, 119, 144)
    c = tuple(round(a + (b - a) * t) for a, b in zip(low, high))
    draw.line((0, py, W, py), fill=c)
cloud = Image.open(ROOT / "Assets/Art/DesktopSky/clouds.png").convert("RGBA")
for row in range(15):
    place(canvas, cloud, -14 + (-.8 if row % 2 else .2), 12 + row * 12.6, 28)
for y in range(175):
    for x in range(-12, 12):
        key = "Circuit" if (x * 7 + y * 11) % 29 == 0 else "Vent" if (x * 3 + y * 5) % 43 == 0 else "Facade"
        tile = images[key].resize((32, 32), Image.Resampling.NEAREST)
        # 내부 벽은 플레이 가능한 발판보다 어둡게 보이게 합니다.
        tile = ImageEnhance.Brightness(tile).enhance(.57)
        place(canvas, tile, x, y + 1, 1, 1)

# 같은 구간의 공기감을 읽기 쉽도록 창틀을 별도 그림으로 덧댑니다.
for y in range(8, 173, 10):
    for x in (-8, 3):
        left, top = point(x, y + 2)
        right, bottom = point(x + 4, y - 2)
        # 벽 레이어에 난 개구부 너머는 원래의 하늘 색과 구름을 그대로 노출합니다.
        sky_patch = Image.new("RGBA", (right-left, bottom-top), (0, 0, 0, 0))
        for sy in range(sky_patch.height):
            t = min(1, max(0, (y + 2 - sy / SCALE) / 175))
            col = tuple(round(a + (b-a)*t) for a, b in zip((92,167,229),(225,119,144)))
            ImageDraw.Draw(sky_patch).line((0, sy, sky_patch.width, sy), fill=col)
        cloud_im = cloud.resize((256, 96), Image.Resampling.NEAREST)
        sky_patch.alpha_composite(cloud_im, (-48, 32))
        # 창틀의 바깥 투명 영역까지 하늘로 덮지 않고 닫힌 창문 안쪽만 뚫습니다.
        frame = images["Window"].resize(sky_patch.size, Image.Resampling.NEAREST)
        alpha = frame.getchannel("A")
        mask = Image.new("L", frame.size, 0)
        frontier = [(frame.width // 2, frame.height // 2)]
        seen = set()
        while frontier:
            px, py = frontier.pop()
            if (px, py) in seen or px < 0 or py < 0 or px >= frame.width or py >= frame.height:
                continue
            seen.add((px, py))
            if alpha.getpixel((px, py)) > 70:
                continue
            mask.putpixel((px, py), 255)
            frontier.extend(((px-1, py), (px+1, py), (px, py-1), (px, py+1)))
        sky_patch.putalpha(mask)
        canvas.alpha_composite(sky_patch, (left, top))
        place(canvas, images["Window"], x, y + 2, 4, 4)

for side in (-13, 12):
    for y in range(0, 174, 3):
        art = images["Wall"] if side < 0 else images["Wall"].transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        place(canvas, art, side, y + 3, 1, 3)
    place(canvas, images["WallTop"], side, 176, 1, 3)

background = canvas.copy()
background.save(OUT / "배경_검토용.png")


def paint_platforms(target, labels=False):
    """검증 좌표표의 발판을 그대로 그립니다. 접촉면은 도트 상단에서 0.2칸 아래입니다."""
    d = ImageDraw.Draw(target)
    for p in platforms:
        width = p["Width"]
        if p["Id"] in (0, 998):
            # 바닥과 정상은 4칸 도트 여섯 장으로 같은 접촉 표면을 표현합니다.
            for x in range(-12, 12, 4):
                place(target, images["Stone_4"], x, p["Y"] + .2, 4)
        else:
            place(target, images[f'{p["Kind"]}_{round(width)}'], p["X"] - width / 2, p["Y"] + .2, width)
        if labels:
            x, y = point(p["X"] - width / 2, p["Y"] + .7)
            text = "옥상" if p["Id"] == 998 else "진입" if p["Id"] == 997 else f'{p["Id"]:02}'
            d.rounded_rectangle((x - 3, y - 1, x + len(text) * 9 + 4, y + 18), 3, fill=(8, 15, 28, 230))
            d.text((x, y), text, font=font(12), fill="#d3f8ee")


paint_platforms(canvas)
place(canvas, idle.crop(idle.getbbox()), -10.25, .9, .7, .9)
canvas.convert("RGB").save(OUT / "검증된_랜덤맵_전체.png")
with_route = background.copy()
d = ImageDraw.Draw(with_route, "RGBA")
pts = [point(float(s["x"]), float(s["y"])) for s in trace if s["event"] != "Settle"]
d.line(pts, fill=(80, 236, 205, 165), width=2)
for s in trace:
    if s["event"] == "Land":
        x, y = point(float(s["x"]), float(s["y"]))
        d.ellipse((x-3, y-3, x+3, y+3), fill=(255, 220, 132, 245))
paint_platforms(with_route, True)
with_route.convert("RGB").save(OUT / "검증된_랜덤맵_완주경로.png")

# 세 구간을 옆으로 비교할 수 있는 한 장의 사진을 만듭니다.
BOARD_W, BOARD_H = 2240, 1960
board = Image.new("RGB", (BOARD_W, BOARD_H), "#0a1020")
bd = ImageDraw.Draw(board)
bd.text((40, 25), "도달 가능한 랜덤 배치 · 한 개의 175m 코스", font=font(34, True), fill="#f3f6ff")
bd.text((42, 78), "실제 물리 코드 60Hz 완주 검증  /  같은 높이의 발판 + 무작위 재질 + 구간별 길이 제한", font=font(20), fill="#a8b9cf")
panels = [(-1, 60, "초반   0–55m", "4·6칸  ·  중간 + 긴 발판"), (57, 118, "중반   55–115m", "4칸 중심  ·  재질과 벽 반동 응용"), (116, 177, "후반   115–175m", "2·4칸  ·  중간 + 짧은 발판")]
for i, (lo, hi, title, subtitle) in enumerate(panels):
    x = 30 + i * 738
    bd.text((x + 6, 128), title, font=font(25, True), fill="#77e0c7")
    bd.text((x + 6, 168), subtitle, font=font(17), fill="#c0cadb")
    y0, y1 = point(0, hi)[1], point(0, lo)[1]
    panel = canvas.crop((30, y0, W - 30, y1))
    panel = panel.resize((round(panel.width * 1560 / panel.height), 1560), Image.Resampling.NEAREST)
    board.paste(panel, (x + (700-panel.width)//2, 208))
    bd.rectangle((x, 208, x + 700, 1768), outline="#28364f", width=2)
bd.text((40, 1790), f'발판 {proof["platformCount"]}개 전부 방문  ·  {proof["Jumps"]}회 점프  ·  {proof["Batches"]}개 예약 묶음  ·  벽 반동 {proof["WallBounces"]}회', font=font(23, True), fill="#f4f7ff")
bd.text((42, 1834), "일반 / 기름 / 금간 / 타이머 혼합  ·  각 점프 충전량 ±2프레임 개별 오차 198/198 완주", font=font(19), fill="#bcc9dd")
bd.text((42, 1873), "별도 물리 모델 검증용 시안 · 실제 씬 미적용 · 최종 도약은 기존 옥상 중앙 착지 규칙 적용", font=font(17), fill="#8699b2")
board.save(OUT / "검증된_랜덤맵_하중상.png")

print(json.dumps({"sprites": manifest, "same_y_groups": sum(1 for y in {p["Y"] for p in platforms} if sum(1 for p in platforms if p["Y"] == y) > 1), "pixels": [W, H]}, ensure_ascii=False, indent=2))
