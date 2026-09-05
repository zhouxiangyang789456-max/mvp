# -*- coding: utf-8 -*-
"""Diagnose current tank state: root/mesh transforms + world AABB."""
import bpy
from mathutils import Vector

parent = bpy.data.objects.get("ParentNode")
print("ParentNode:", parent)
if parent:
    print("  matrix_world:")
    for row in parent.matrix_world:
        print("   ", [round(v, 4) for v in row])
    print("  rotation_euler:", tuple(round(v, 3) for v in parent.rotation_euler))
    print("  location:", tuple(round(v, 3) for v in parent.location))

m = bpy.data.objects.get("Tank_part0")
print("Tank_part0:")
print("  parent:", m.parent.name if m and m.parent else None)
print("  matrix_world:")
for row in m.matrix_world:
    print("   ", [round(v, 4) for v in row])
print("  matrix_local:")
for row in m.matrix_local:
    print("   ", [round(v, 4) for v in row])

# world AABB over all meshes
meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Tank_part")]
mins = [1e9] * 3
maxs = [-1e9] * 3
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mins[i] = min(mins[i], w[i])
            maxs[i] = max(maxs[i], w[i])
print("meshes:", len(meshes))
print("AABB min:", tuple(round(v, 3) for v in mins))
print("AABB max:", tuple(round(v, 3) for v in maxs))
print("size:", tuple(round(maxs[i] - mins[i], 3) for i in range(3)))

# sample: lowest & highest world point of Tank_part0
if m:
    lows = [o for o in meshes]
    # find the mesh containing the global min z corner
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            if abs(w[2] - mins[2]) < 0.001:
                print("low corner of", o.name, "->", tuple(round(v, 3) for v in w))
                break
        else:
            continue
        break
