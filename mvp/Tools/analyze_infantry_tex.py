# -*- coding: utf-8 -*-
"""Analyze infantry basecolor textures: avg color + luminance percentiles."""
import os
from PIL import Image

d = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Textures"
for f in sorted(os.listdir(d)):
    if not f.endswith("_BaseColor.png"):
        continue
    p = os.path.join(d, f)
    im = Image.open(p).convert("RGB")
    w, h = im.size
    px = list(im.getdata())
    n = len(px)
    r = sum(c[0] for c in px) // n
    g = sum(c[1] for c in px) // n
    b = sum(c[2] for c in px) // n
    lum = sorted(0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2] for c in px)
    p10 = int(lum[int(n * 0.10)])
    p50 = int(lum[int(n * 0.50)])
    p90 = int(lum[int(n * 0.90)])
    print("%-42s %dx%d avg=(%d,%d,%d) lum p10/p50/p90=%d/%d/%d"
          % (f, w, h, r, g, b, p10, p50, p90))
