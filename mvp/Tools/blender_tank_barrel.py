# -*- coding: utf-8 -*-
"""Determine which Blender axis the tank barrel (front) points toward."""
import bpy
from mathutils import Vector

meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Tank_part")]

# Report extents along each axis for a few candidate long/thin parts.
print("--- parts sorted by |world max x| and |world min x| ---")
with_x = []
for o in meshes:
    xs = [ (o.matrix_world @ Vector(c))[0] for c in o.bound_box ]
    with_x.append((o.name, min(xs), max(xs)))
by_min = sorted(with_x, key=lambda r: r[1])
by_max = sorted(with_x, key=lambda r: -r[2])
print("most -x (tip toward -X):", [(n, round(a,3), round(b,3)) for n,a,b in by_min[:5]])
print("most +x (tip toward +X):", [(n, round(a,3), round(b,3)) for n,a,b in by_max[:5]])

# Check the part we earlier identified as the barrel (Tank_part6).
o = bpy.data.objects.get("Tank_part6")
if o:
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = [min(p[i] for p in pts) for i in range(3)]
    mx = [max(p[i] for p in pts) for i in range(3)]
    sz = [mx[i]-mn[i] for i in range(3)]
    print("Tank_part6 center=", tuple(round((mn[i]+mx[i])/2,3) for i in range(3)),
          "size=", tuple(round(s,3) for s in sz))

# Whole-tank AABB
mn = [1e9]*3; mx=[-1e9]*3
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mn[i]=min(mn[i],w[i]); mx[i]=max(mx[i],w[i])
print("AABB min=", tuple(round(v,3) for v in mn), "max=", tuple(round(v,3) for v in mx))
