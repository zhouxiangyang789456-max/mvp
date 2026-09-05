# -*- coding: utf-8 -*-
"""Recolor soldier basecolor textures with the user-approved palette.

Uses the exact hex palette from the user, removes the heavy dark paper grain
(smooth luminance shading), and segments semantic parts:
  - Uniform (TorsoL/Chest-tunic/ArmL/R/Pelvis + thigh) -> brick red gradient
  - Backpack + waist bag (Chest gray islands / Waist mesh) -> neutral gray
  - Gaiters (lower legs) -> lighter gray
  - Helmet -> neutral gray, Boots -> dark gray
  - Face/hands -> warm beige, hair -> dark brown
  - Rifle -> dark red-brown handguard + dark gray metal

Shading is a heavily blurred luminance so the paper grain / baked AO no longer
reads as black noise. Alpha is preserved; originals live in _backup_infantry_textures.
"""
import os
import json
from PIL import Image, ImageDraw, ImageFilter, ImageStat

D = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Textures"
BACKUP = r"D:/prounity/mvp/mvp/Tools/_backup_infantry_textures"
UV_POLYS = r"D:/prounity/mvp/mvp/Tools/_uv_polys.json"

# --- user palette (hex -> rgb) ---
UNIFORM_LO   = (117, 64, 56)    # #754038  uniform shadow
UNIFORM_BASE = (155, 85, 73)    # #9B5549  uniform base
UNIFORM_HI   = (181, 107, 90)   # #B56B5A  uniform highlight
HELMET       = (119, 118, 119)  # #777677  helmet
PACK         = (105, 102, 103)  # #696667  backpack / waist bag
GAITERS      = (133, 129, 131)  # #858183  gaiters / puttees
SKIN         = (216, 196, 170)  # #D8C4AA  face / hands
HANDGUARD    = (116, 69, 59)    # #74453B  rifle stock / handguard
METAL        = (70, 73, 75)     # #46494B  rifle metal
BOOTS        = (87, 82, 83)     # #575253  boots
HAIR         = (58, 47, 38)     # dark brown hair

LEG_THRESHOLD = 0.40  # world-Z below this -> gaiters (gray), above -> trousers (uniform)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def shade_uniform(t):
    """Map normalized smooth luminance to the user's 3-stop uniform gradient."""
    if t < 0.5:
        return lerp(UNIFORM_LO, UNIFORM_BASE, t / 0.5)
    return lerp(UNIFORM_BASE, UNIFORM_HI, (t - 0.5) / 0.5)


def shade_flat(flat, t):
    f = 0.78 + 0.27 * t
    return tuple(int(c * f) for c in flat)


def luminance(px):
    return [int(0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) for c in px]


def build_smooth_lum(px, alpha, w, h):
    """Blurred luminance with transparent background filled by opaque median."""
    n = len(px)
    lum = [0] * n
    op_lum = []
    for i in range(n):
        v = int(0.299 * px[i][0] + 0.587 * px[i][1] + 0.114 * px[i][2])
        lum[i] = v
        if alpha[i] > 0:
            op_lum.append(v)
    med = int(sorted(op_lum)[len(op_lum) // 2]) if op_lum else 128
    for i in range(n):
        if alpha[i] == 0:
            lum[i] = med
    im = Image.new("L", (w, h))
    im.putdata(lum)
    radius = max(12, min(w, h) // 96)
    im = im.filter(ImageFilter.GaussianBlur(radius))
    return list(im.getdata())


def norm_lum(s, alpha):
    vals = [s[i] for i in range(len(s)) if alpha[i] > 0]
    if not vals:
        return [0.5] * len(s)
    sv = sorted(vals)
    lo = sv[int(len(sv) * 0.05)]
    hi = sv[min(len(sv) - 1, int(len(sv) * 0.95))]
    span = max(1, hi - lo)
    return [max(0.0, min(1.0, (v - lo) / span)) for v in s]


def leg_mask(name, w, h):
    """Label image: 0=uniform(trousers), 1=gaiters. Built from UV islands."""
    uv = json.load(open(UV_POLYS, encoding="utf-8"))
    islands = uv.get(name)
    if not islands:
        return None
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    for iso in islands:
        zmid = (iso["z_min"] + iso["z_max"]) / 2.0
        label = 1 if zmid < LEG_THRESHOLD else 0
        for poly in iso["polys"]:
            pts = [(int(u * w), int((1.0 - v) * h)) for u, v in poly]
            d.polygon(pts, fill=label)
    return img


def process(fname, rule):
    # classification + shading come from the ORIGINAL backup; output goes to D.
    src = os.path.join(BACKUP, fname)
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    px = list(im.getdata())
    n = len(px)
    alpha = [c[3] for c in px]

    s = build_smooth_lum(px, alpha, w, h)
    sn = norm_lum(s, alpha)

    if rule == "chest":
        # backpack (gray, B>=R) vs tunic (warm). Uses original pixel color.
        pal = [("uniform", None), ("flat", PACK)]
        cat = [0] * n
        for i in range(n):
            if alpha[i] > 0 and px[i][2] >= px[i][0]:
                cat[i] = 1
    elif rule == "leg":
        mask = leg_mask(fname.replace("_BaseColor.png", ""), w, h)
        pal = [("uniform", None), ("flat", GAITERS)]
        cat = [0] * n
        if mask is not None:
            ml = list(mask.getdata())
            for i in range(n):
                if alpha[i] > 0:
                    cat[i] = 1 if ml[i] else 0
    elif rule == "uniform":
        pal = [("uniform", None)]
        cat = [0] * n
    elif rule == "head":
        pal = [("flat", SKIN), ("flat", HAIR)]
        cat = [0] * n
        for i in range(n):
            if alpha[i] > 0 and s[i] < 80:
                cat[i] = 1
    elif rule == "rifle":
        pal = [("flat", HANDGUARD), ("flat", METAL)]
        cat = [0] * n
        for i in range(n):
            if alpha[i] > 0 and px[i][0] - px[i][2] >= 20:
                cat[i] = 0
            else:
                cat[i] = 1
    else:
        # single flat color rule
        pal = [("flat", rule)]
        cat = [0] * n

    out = [None] * n
    for i in range(n):
        if alpha[i] == 0:
            out[i] = px[i]
            continue
        kind, col = pal[cat[i]]
        t = sn[i]
        if kind == "uniform":
            rgb = shade_uniform(t)
        else:
            rgb = shade_flat(col, t)
        out[i] = (rgb[0], rgb[1], rgb[2], alpha[i])

    im.putdata(out)
    im.save(os.path.join(D, fname))
    # verify
    r = sum(o[0] for o in out if o[3] > 0)
    g = sum(o[1] for o in out if o[3] > 0)
    b = sum(o[2] for o in out if o[3] > 0)
    cnt = sum(1 for o in out if o[3] > 0)
    print("recolored %-42s avg=(%d,%d,%d) opaque=%d"
          % (fname, r // cnt, g // cnt, b // cnt, cnt))


RULES = {
    "Soldier_ArmL_BaseColor.png": "uniform",
    "Soldier_ArmR_BaseColor.png": "uniform",
    "Soldier_TorsoL_BaseColor.png": "uniform",
    "Soldier_Pelvis_BaseColor.png": "uniform",
    "Soldier_Helmet_BaseColor.png": HELMET,
    "Soldier_Waist_BaseColor.png": PACK,
    "Soldier_FootR_BaseColor.png": BOOTS,
    "Soldier_HandL_BaseColor.png": SKIN,
    "Soldier_HandR_BaseColor.png": SKIN,
    "Soldier_Chest_BaseColor.png": "chest",
    "Soldier_LegL_BaseColor.png": "leg",
    "Soldier_LegR_BaseColor.png": "leg",
    "Soldier_Head_BaseColor.png": "head",
    "Infantry_Rifle_BaseColor.png": "rifle",
}

for f in sorted(RULES):
    process(f, RULES[f])

print("done")
