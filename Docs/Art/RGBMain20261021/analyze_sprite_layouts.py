"""Read alpha bounds and write SpriteRect metadata; never modify PNG pixels."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Docs/Art/RGBMain20261021"

def bounds(path, region, square=False):
    image = np.asarray(Image.open(ROOT / path).convert("RGBA"))
    x0, y0, x1, y1 = region
    y, x = np.where(image[y0:y1, x0:x1, 3] > 100)
    if len(x) == 0:
        raise RuntimeError(f"Empty sprite: {path} {region}")
    left, top = x0 + int(x.min()), y0 + int(y.min())
    right, bottom = x0 + int(x.max()) + 1, y0 + int(y.max()) + 1
    if square:
        size = max(right-left, bottom-top)
        center_x, center_y = (left+right)/2, (top+bottom)/2
        left, top = round(center_x-size/2), round(center_y-size/2)
        right, bottom = left+size, top+size
    return dict(x=left, y=image.shape[0]-bottom, width=right-left, height=bottom-top)

def source_columns(path, y0, y1):
    alpha = np.asarray(Image.open(ROOT/path).convert("RGBA"))[y0:y1,:,3]
    occupied = np.flatnonzero((alpha > 100).any(axis=0))
    cuts = np.flatnonzero(np.diff(occupied) > 16)
    starts = np.r_[occupied[0], occupied[cuts+1]]
    ends = np.r_[occupied[cuts]+1, occupied[-1]+1]
    if len(starts) != 4:
        raise RuntimeError(f"Expected four separated source modules: {path} {y0}")
    return list(zip(starts.tolist(),ends.tolist()))

def main():
    path = "Assets/Art/RGBMain20261021/PlatformFacade.png"
    atlas = []
    groups = [
        ("Normal", (0, 0, 384, 256)),
        ("Oil", (384, 0, 768, 256)),
        ("Cracked", (768, 0, 1152, 256)),
        ("Timer", (1152, 0, 1536, 256)),
        ("Moving", (0, 256, 384, 450)),
        ("Trampoline", (384, 256, 768, 450)),
        ("Goal", (768, 256, 1152, 450)),
        ("Digit3", (1175, 300, 1287, 435)),
        ("Digit2", (1291, 300, 1402, 435)),
        ("Digit1", (1403, 300, 1525, 435)),
        ("FacadePlain", (0, 450, 384, 737)),
        ("FacadeCircuit", (384, 450, 768, 737)),
        ("FacadeWindow", (768, 450, 1152, 737)),
        ("FacadeVent", (1152, 450, 1536, 737)),
        ("Joint", (0, 737, 384, 1024)),
        ("Window", (384, 737, 768, 1024)),
        ("Pillar", (768, 737, 1152, 1024)),
        ("Banner", (1152, 737, 1536, 1024)),
    ]
    for name, region in groups:
        atlas.append(dict(name=name, rect=bounds(path, region, name.startswith("Facade"))))
    atlas.append(dict(name="OilCoating", rect=dict(x=407,y=896,width=340,height=31)))
    atlas.append(dict(name="TimerStripe", rect=dict(x=1176,y=869,width=330,height=24)))
    source_sets = []
    themes = json.loads((ROOT / "Docs/Art/StageThemes20261020/Themes.json").read_text(encoding="utf-8-sig"))["themes"]
    for theme in themes:
        for source in theme["sources"]:
            texture_override = "Assets/Art/RGBMain20261021/초기벽_투명파생본.png" if source.endswith("KakaoTalk_20261020_180550229.png") else ""
            texture_source = texture_override or source
            width, height = Image.open(ROOT/texture_source).size
            sprites = []
            roles = ["L_Top", "L_Body", "L_Foot", "L_Broken", "R_Top", "R_Body", "R_Foot", "R_Broken"]
            columns = [source_columns(texture_source,round(row*height/2),round((row+1)*height/2)) for row in range(2)]
            for i, role in enumerate(roles):
                col, row = i % 4, i // 4
                left,right = columns[row][col]
                region = (left,round(row*height/2),right,round((row+1)*height/2))
                sprites.append(dict(name=role, rect=bounds(texture_source, region)))
            source_sets.append(dict(theme=theme["index"], asset=source, textureOverride=texture_override, sprites=sprites))
    layer_path = "Assets/Art/RGBMain20261021/ParallaxLayers.png"
    layers = [dict(name=name,rect=bounds(layer_path,region)) for name,region in [
        ("CloudLayer",(0,0,1536,320)),("FarCastle",(0,320,1536,658)),("NearCastle",(0,658,1536,1024))]]
    result = dict(method="Alpha-bounds SpriteRect metadata only. PNG pixels unchanged.",
                  atlas=path, sprites=atlas, layerAtlas=layer_path, layers=layers, sources=source_sets)
    (OUT/"SpriteLayouts.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(dict(newSprites=len(atlas),sourceSheets=len(source_sets),sourceModules=sum(len(s["sprites"]) for s in source_sets)),ensure_ascii=False))

if __name__ == "__main__":
    main()
