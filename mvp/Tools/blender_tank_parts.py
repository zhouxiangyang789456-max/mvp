# -*- coding: utf-8 -*-
"""Analyze tank parts: world center, bbox size, aspect, identify barrel/turret."""
import bpy
from mathutils import Vector

objs = [o for o in bpy.data.objects if o.type == "MESH"]
rows = []
for o in objs:
    m = o.matrix_world
    c = m @ Vector((0, 0, 0))
    bbox = [m @ Vector(corner) for corner in o.bound_box]
    mn = [min(b[i] for b in bbox) for i in range(3)]
    mx = [max(b[i] for b in bbox) for i in range(3)]
    size = [mx[i] - mn[i] for i in range(3)]
    aspect = max(size) / (min(size) + 1e-6)
    rows.append((o.name, c, size, aspect, mn, mx))

print("--- parts with aspect >= 2 (long & thin: barrel/gun/antenna/treads?) ---")
for name, c, size, aspect, mn, mx in rows:
    if aspect >= 2.0:
        long = max(range(3), key=lambda i: size[i])
        axis = "XYZ"[long]
        print("  %-14s center=(%+.3f,%+.3f,%+.3f) size=(%+.3f,%+.3f,%+.3f) aspect=%5.1f long=%s" % (
            name, c[0], c[1], c[2], size[0], size[1], size[2], aspect, axis))

print("--- top 6 parts by center y (turret/cupola?) ---")
top = sorted(rows, key=lambda r: -r[1][1])[:6]
for name, c, size, aspect, mn, mx in top:
    print("  %-14s center=(%+.3f,%+.3f,%+.3f) size=(%+.3f,%+.3f,%+.3f)" % (
        name, c[0], c[1], c[2], size[0], size[1], size[2]))

print("--- parts extending to extremes along x (barrel tip?) ---")
xs = sorted(rows, key=lambda r: r[5][0])
print("  most -x:", [(r[0], round(r[5][0], 3), round(r[4][0], 3)) for r in xs[:3]])
xs2 = sorted(rows, key=lambda r: -r[5][0])
print("  most +x:", [(r[0], round(r[4][0], 3), round(r[5][0], 3)) for r in xs2[:3]])

print("--- parts extending to extremes along z ---")
zs = sorted(rows, key=lambda r: r[5][2])
print("  most -z:", [(r[0], round(r[5][2], 3), round(r[4][2], 3)) for r in zs[:3]])
zs2 = sorted(rows, key=lambda r: -r[5][2])
print("  most +z:", [(r[0], round(r[4][2], 3), round(r[5][2], 3)) for r in zs2[:3]])
