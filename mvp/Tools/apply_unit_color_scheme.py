# -*- coding: utf-8 -*-
"""Apply the unit visual color scheme (单位视觉配色方案).

Recolors soldier + tank BaseColor textures to a cool blue-gray scheme so units
no longer blend into forest / sand / yellow-green terrain. Shading is preserved:
a per-part [2%, 98%] luminance-percentile normalization maps each part's baked
lighting to a 3-stop shadow / base / highlight gradient, so the camo/baked-AO
*shape* stays intact while the hue becomes uniformly cool.

Per the approved plan:
  - Only *_BaseColor.png assets are modified. Normal textures (geometry, not
    color) and the FBX/prefabs are untouched; Unity re-imports the PNGs
    automatically because the in-engine _MainTex references them by GUID.
  - Originals are copied to Tools/backup_orig_colors/<unit>/ before overwriting
    so the change is easy to revert.
  - Multi-material parts use per-material luminance thresholds:
      head  -> skin (t >= 0.40) vs hair (kept near-black)
      rifle -> single cool gunmetal scale; the top ~2% luminance pixels are
               clamped to t = 1 and therefore resolve to the highlight stop.
  - Tank parts are classified by index (armor / tracks / barrel+metal). The
    split is approximate by design; the index sets are listed explicitly below
    so they can be tuned after a visual check in Unity.
"""
import os
import shutil
from PIL import Image

UNITS_ROOT = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units"
BACKUP_ROOT = r"D:/prounity/mvp/mvp/Tools/backup_orig_colors"

# ---------------------------------------------------------------------------
# Palette (hex -> rgb). Scales are (shadow, base, highlight).
# ---------------------------------------------------------------------------
def hex2rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def scale(*hexes):
    return tuple(hex2rgb(h) for h in hexes)


# --- Soldier ---
S_UNIFORM = scale('#293E52', '#3F6077', '#66869A')  # 主制服 (torso/arms/legs/pelvis)
S_ARMOR   = scale('#34465A', '#59636C', '#7896A5')  # 防弹衣 (chest/waist)
S_HELMET  = scale('#34465A', '#718696', '#8EA6B8')  # 头盔
S_SKIN    = scale('#916650', '#D1A07D', '#E8C4A3')  # 皮肤 (hands/face)
S_BOOTS   = scale('#1B2733', '#293E52', '#34465A')  # 靴子
S_RIFLE   = scale('#101820', '#202D39', '#314757')  # 枪械 (近似亮边由 2%/98% 分位自然产生)

# --- Tank ---
T_ARMOR   = scale('#2D4658', '#48677A', '#7896A5')  # 主装甲 (all other parts)
T_TRACK   = scale('#182A37', '#202A32', '#53616A')  # 履带 (deep red-brown / tiny dark parts)
T_BARREL  = scale('#182A37', '#314757', '#53616A')  # 炮管 / 金属件 (cool blue-gray metal)

HEAD_SKIN_THRESHOLD = 0.40  # normalized luminance >= this -> skin, else hair

# ---------------------------------------------------------------------------
# Part -> scale mapping.
# ---------------------------------------------------------------------------
INFANTRY_RULES = {
    'Soldier_TorsoL_BaseColor.png': S_UNIFORM,
    'Soldier_ArmL_BaseColor.png':   S_UNIFORM,
    'Soldier_ArmR_BaseColor.png':   S_UNIFORM,
    'Soldier_LegL_BaseColor.png':   S_UNIFORM,
    'Soldier_LegR_BaseColor.png':   S_UNIFORM,
    'Soldier_Pelvis_BaseColor.png': S_UNIFORM,
    'Soldier_Chest_BaseColor.png':  S_ARMOR,
    'Soldier_Waist_BaseColor.png':  S_ARMOR,
    'Soldier_Helmet_BaseColor.png': S_HELMET,
    'Soldier_HandL_BaseColor.png':  S_SKIN,
    'Soldier_HandR_BaseColor.png':  S_SKIN,
    'Soldier_FootR_BaseColor.png':  S_BOOTS,
    'Soldier_Head_BaseColor.png':   S_SKIN,   # special: skin + hair
    'Infantry_Rifle_BaseColor.png': S_RIFLE,
}
INFANTRY_HEAD = 'Soldier_Head_BaseColor.png'

# Tank part index -> category. Approximate split; tune after a visual check.
TANK_TRACK_PARTS = {26, 39, 43, 45}                 # 履带 (dark red-brown / tiny dark)
TANK_BARREL_PARTS = {23, 24, 30, 31, 37}            # 炮管 / 金属件 (cool blue-gray)


def tank_scale_for(part_index):
    if part_index in TANK_TRACK_PARTS:
        return T_TRACK
    if part_index in TANK_BARREL_PARTS:
        return T_BARREL
    return T_ARMOR


# ---------------------------------------------------------------------------
# Core mapping helpers.
# ---------------------------------------------------------------------------
def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def shade(t, stops):
    """3-stop gradient: shadow -> base -> highlight, split at t = 0.5."""
    shadow, base, highlight = stops
    if t < 0.5:
        return lerp(shadow, base, t * 2.0)
    return lerp(base, highlight, (t - 0.5) * 2.0)


def luminance(px):
    return [int(0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) for c in px]


def normalize(lums, alpha):
    """Map opaque-pixel luminance to t in [0,1] via [2%, 98%] percentiles.

    The bottom ~2% clamp to 0 (shadow) and the top ~2% clamp to 1 (highlight),
    which is exactly what the rifle '近似亮边' note relies on.
    """
    op = [v for i, v in enumerate(lums) if alpha[i] > 0]
    if not op:
        return [0.5] * len(lums)
    op.sort()
    lo = op[int(len(op) * 0.02)]
    hi = op[min(len(op) - 1, int(len(op) * 0.98))]
    span = max(1, hi - lo)
    out = []
    for i, v in enumerate(lums):
        if alpha[i] == 0:
            out.append(0.5)
        else:
            t = (v - lo) / span
            out.append(0.0 if t < 0.0 else (1.0 if t > 1.0 else t))
    return out


# ---------------------------------------------------------------------------
# Per-image processing.
# ---------------------------------------------------------------------------
def process(src, dst, stops, special=None):
    im = Image.open(src).convert('RGBA')
    w, h = im.size
    px = list(im.getdata())
    n = len(px)
    alpha = [c[3] for c in px]
    t = normalize(luminance(px), alpha)

    out = [None] * n
    for i in range(n):
        if alpha[i] == 0:
            out[i] = px[i]
            continue
        if special == 'head':
            if t[i] >= HEAD_SKIN_THRESHOLD:
                rgb = shade(t[i], stops)
            else:
                rgb = (px[i][0], px[i][1], px[i][2])  # hair: keep near-black
        else:
            rgb = shade(t[i], stops)
        out[i] = (rgb[0], rgb[1], rgb[2], alpha[i])

    im.putdata(out)
    im.save(dst)

    # verification stats (opaque pixels only)
    op = [o for o in out if o[3] > 0]
    cnt = len(op)
    avg = tuple(sum(o[k] for o in op) // cnt for k in range(3))
    return w, h, avg, cnt


def backup(src_path, unit_dir):
    os.makedirs(unit_dir, exist_ok=True)
    dst = os.path.join(unit_dir, os.path.basename(src_path))
    if not os.path.exists(dst):
        shutil.copy2(src_path, dst)
        return True
    return False


# ---------------------------------------------------------------------------
# Main.
# ---------------------------------------------------------------------------
def run():
    total = 0

    # --- Infantry ---
    inf_dir = os.path.join(UNITS_ROOT, 'Infantry', 'Textures')
    inf_bak = os.path.join(BACKUP_ROOT, 'Infantry')
    for name in sorted(INFANTRY_RULES):
        src = os.path.join(inf_dir, name)
        if not os.path.exists(src):
            print('MISSING: %s' % src)
            continue
        special = 'head' if name == INFANTRY_HEAD else None
        bak = backup(src, inf_bak)
        w, h, avg, cnt = process(src, src, INFANTRY_RULES[name], special)
        total += 1
        print('recolored %-42s %dx%d avg=(%3d,%3d,%3d) opaque=%d %s'
              % (name, w, h, avg[0], avg[1], avg[2], cnt,
                 'backup=NEW' if bak else 'backup=exists'))

    # --- Tank ---
    tank_dir = os.path.join(UNITS_ROOT, 'Tank', 'Textures')
    tank_bak = os.path.join(BACKUP_ROOT, 'Tank')
    for idx in range(46):
        name = 'Tank_part%d_BaseColor.png' % idx
        src = os.path.join(tank_dir, name)
        if not os.path.exists(src):
            continue
        bak = backup(src, tank_bak)
        w, h, avg, cnt = process(src, src, tank_scale_for(idx))
        total += 1
        cat = ('track ' if idx in TANK_TRACK_PARTS
               else 'barrel' if idx in TANK_BARREL_PARTS else 'armor ')
        print('recolored %-28s %dx%d avg=(%3d,%3d,%3d) opaque=%d %s [%s]'
              % (name, w, h, avg[0], avg[1], avg[2], cnt,
                 'backup=NEW' if bak else 'backup=exists', cat))

    print('\ndone: %d textures recolored; backups in %s' % (total, BACKUP_ROOT))


if __name__ == '__main__':
    run()
