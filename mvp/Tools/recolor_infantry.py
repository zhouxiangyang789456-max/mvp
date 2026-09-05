# -*- coding: utf-8 -*-
"""Recolor the soldier basecolor textures to the requested scheme.

  uniform   : low-saturation brick red
  helmet    : neutral gray
  boots     : dark gray
  skin      : warm beige (face + hands); dark head pixels -> dark hair
  rifle     : warm pixels -> dark red-brown handguard, cool pixels -> dark gray metal

Keeps the original luminance as subtle shading and preserves alpha.
Originals are backed up in Tools/_backup_infantry_textures/.
"""
import os
from PIL import Image

D = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Textures"

# target flat colors
BRICK = (148, 62, 54)      # 军服 低饱和砖红
GRAY = (138, 138, 138)     # 钢盔/装备 中性灰
BEIGE = (236, 212, 182)    # 面部/手 暖米白
HANDGUARD = (104, 42, 32)  # 护木 暗红棕
METAL = (72, 72, 72)       # 枪管/金属 深灰
HAIR = (50, 40, 32)        # 头发 深棕
BOOTS = (72, 70, 68)       # 靴 深灰

UNIFORM = {"Soldier_ArmL", "Soldier_ArmR", "Soldier_Chest", "Soldier_TorsoL",
           "Soldier_Pelvis", "Soldier_Waist", "Soldier_LegL", "Soldier_LegR"}
GRAY_PARTS = {"Soldier_Helmet"}
BEIGE_PARTS = {"Soldier_HandL", "Soldier_HandR"}
BOOT_PARTS = {"Soldier_FootR"}


def shade(flat, lum_norm, base=0.78, range_=0.22):
    """flat color * (base + range_*lum_norm) to keep shading while recoloring."""
    f = base + range_ * lum_norm
    return (int(flat[0] * f), int(flat[1] * f), int(flat[2] * f))


def recolor_flat(px, flat):
    out = []
    for c in px:
        a = c[3]
        if a == 0:
            out.append(c)
            continue
        lum = 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
        ln = min(1.0, lum / 255.0)
        r, g, b = shade(flat, ln)
        out.append((r, g, b, a))
    return out


def recolor_head(px):
    out = []
    for c in px:
        a = c[3]
        if a == 0:
            out.append(c)
            continue
        lum = 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
        if lum < 75:  # dark -> hair
            f = 0.75 + 0.25 * min(1.0, lum / 75.0)
            out.append((int(HAIR[0] * f), int(HAIR[1] * f), int(HAIR[2] * f), a))
        else:  # skin
            ln = min(1.0, lum / 255.0)
            r, g, b = shade(BEIGE, ln)
            out.append((r, g, b, a))
    return out


def recolor_rifle(px):
    out = []
    for c in px:
        a = c[3]
        if a == 0:
            out.append(c)
            continue
        lum = 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
        ln = min(1.0, lum / 255.0)
        if c[0] - c[2] >= 20:  # warm -> wood handguard
            r, g, b = shade(HANDGUARD, ln)
        else:  # cool -> metal
            r, g, b = shade(METAL, ln)
        out.append((r, g, b, a))
    return out


def process(fname, label, fn):
    p = os.path.join(D, fname)
    im = Image.open(p).convert("RGBA")
    px = list(im.getdata())
    out = fn(px)
    im.putdata(out)
    im.save(p)
    print("recolored %-42s %s" % (fname, label))


for f in sorted(os.listdir(D)):
    if not f.endswith("_BaseColor.png"):
        continue
    stem = f[: -len("_BaseColor.png")]
    if stem == "Infantry_Rifle":
        process(f, "handguard+metal", recolor_rifle)
    elif stem in UNIFORM:
        process(f, "uniform brick-red", lambda px, flat=BRICK: recolor_flat(px, flat))
    elif stem in GRAY_PARTS:
        process(f, "helmet gray", lambda px, flat=GRAY: recolor_flat(px, flat))
    elif stem in BEIGE_PARTS:
        process(f, "skin beige", lambda px, flat=BEIGE: recolor_flat(px, flat))
    elif stem in BOOT_PARTS:
        process(f, "boots dark", lambda px, flat=BOOTS: recolor_flat(px, flat))
    elif stem == "Soldier_Head":
        process(f, "skin+hair", recolor_head)
    else:
        print("SKIP (no mapping):", f)

print("done")
