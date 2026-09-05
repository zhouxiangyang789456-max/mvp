# -*- coding: utf-8 -*-
"""Analyze head + rifle basecolor textures for region segmentation:
- head: detect large dark region (hair) vs skin.
- rifle: wood (handguard, warm) vs metal (gray) threshold.
"""
import os
from PIL import Image

d = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Textures"

def analyze(name, label):
    p = os.path.join(d, name)
    im = Image.open(p).convert("RGB")
    w, h = im.size
    px = list(im.getdata())
    n = len(px)
    print("=== %s (%s) %dx%d ===" % (label, name, w, h))

    # luminance buckets
    buckets = {}
    for c in px:
        lum = int(0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2])
        bucket = lum // 32 * 32  # 0,32,...,224
        buckets[bucket] = buckets.get(bucket, 0) + 1
    print("  luminance histogram (bucket->%):")
    for bucket in sorted(buckets):
        pct = buckets[bucket] * 100.0 / n
        print("    %3d-%3d: %5.1f%%" % (bucket, bucket + 31, pct))

    # For rifle: R vs B to separate wood(warm) from metal(cool)
    if "Rifle" in name:
        warm = 0
        cool = 0
        for c in px:
            if c[0] - c[2] >= 20:
                warm += 1
            else:
                cool += 1
        print("  rifle: warm(R-B>=20)=%5.1f%%  cool=%5.1f%%" % (warm * 100.0 / n, cool * 100.0 / n))


analyze("Soldier_Head_BaseColor.png", "head")
analyze("Infantry_Rifle_BaseColor.png", "rifle")
analyze("Soldier_LegL_BaseColor.png", "legL")
