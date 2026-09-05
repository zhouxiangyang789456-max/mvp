# -*- coding: utf-8 -*-
"""Check soldier fit: overall bbox height, armature scale, root bone world pos,
per-part bbox overlap (interpenetration) for the 4 animations prep."""
import bpy
from mathutils import Vector

arm = bpy.data.objects.get("Armature")
print("=== SCALE / ORIGIN CHECK ===")
print("Armature loc:", [round(v, 4) for v in arm.location])
print("Armature scale:", [round(v, 4) for v in arm.scale])

# root bone world position
root = arm.pose.bones.get("Root")
if root:
    rw = arm.matrix_world @ root.head
    print("Root bone world head:", [round(v, 4) for v in rw])

# Overall model bbox (all mesh objects, world space)
objs = [o for o in bpy.data.objects if o.type == 'MESH']
allpts = []
for o in objs:
    allpts += [o.matrix_world @ Vector(c) for c in o.bound_box]
mins = [min(p[i] for p in allpts) for i in range(3)]
maxs = [max(p[i] for p in allpts) for i in range(3)]
print("Model bbox min:", [round(v, 4) for v in mins])
print("Model bbox max:", [round(v, 4) for v in maxs])
print("Model height (z):", round(maxs[2] - mins[2], 4))
print("Model width (x):", round(maxs[0] - mins[0], 4))
print("Model depth (y):", round(maxs[1] - mins[1], 4))

print()
print("=== PER-PART CENTERS (world) ===")
for o in objs:
    bbox = [o.matrix_world @ Vector(c) for c in o.bound_box]
    c = [round((max(p[i] for p in bbox) + min(p[i] for p in bbox)) / 2, 4) for i in range(3)]
    dims = [round(max(p[i] for p in bbox) - min(p[i] for p in bbox), 4) for i in range(3)]
    print(f"{o.name}: center={c} dims={dims}")

print()
print("=== INTERPENETRATION (overlapping bboxes) ===")
def overlap(a, b):
    # a, b are (min, max) tuples of 3-tuples
    for i in range(3):
        if a[1][i] <= b[0][i] or b[1][i] <= a[0][i]:
            return False
    return True

# Pairwise check; list intentional-part overlaps to report
pairs = []
for i in range(len(objs)):
    for j in range(i + 1, len(objs)):
        oa, ob = objs[i], objs[j]
        amin = [min((oa.matrix_world @ Vector(c))[k] for c in oa.bound_box) for k in range(3)]
        amax = [max((oa.matrix_world @ Vector(c))[k] for c in oa.bound_box) for k in range(3)]
        bmin = [min((ob.matrix_world @ Vector(c))[k] for c in ob.bound_box) for k in range(3)]
        bmax = [max((ob.matrix_world @ Vector(c))[k] for c in ob.bound_box) for k in range(3)]
        if overlap((amin, amax), (bmin, bmax)):
            pairs.append((oa.name, ob.name))

for a, b in pairs:
    print(f"  OVERLAP: {a} <-> {b}")
if not pairs:
    print("  no overlapping bbox pairs")
