# -*- coding: utf-8 -*-
"""Scale soldier armature to target height (~1.7m), bake scale into bone rest,
center origin at feet-center, and remove GLB artifacts (ParentNode, Camera, Light)."""
import bpy
from mathutils import Vector

arm = bpy.data.objects["Armature"]

def model_bbox():
    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    pts = []
    for o in objs:
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    mins = [min(p[i] for p in pts) for i in range(3)]
    maxs = [max(p[i] for p in pts) for i in range(3)]
    return mins, maxs

mins0, maxs0 = model_bbox()
h0 = maxs0[2] - mins0[2]
target = 1.70
s = target / h0
print(f"height {h0:.4f} -> target {target}, factor {s:.4f}")

# 1) scale armature object
arm.scale *= s

# 2) apply scale to armature -> bakes into bone rest positions
bpy.context.view_layer.objects.active = arm
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# 3) remove GLB artifacts
for name in ["ParentNode", "Camera", "Light"]:
    o = bpy.data.objects.get(name)
    if o is not None:
        bpy.data.objects.remove(o, do_unlink=True)
        print("REMOVED", name)

# 4) parent armature directly to scene root
arm.parent = None
arm.location = (0.0, 0.0, 0.0)

# verify
mins1, maxs1 = model_bbox()
h1 = maxs1[2] - mins1[2]
print(f"after scale: height {h1:.4f}, zmin {mins1[2]:.4f}, zmax {maxs1[2]:.4f}")
print(f"x range {mins1[0]:.4f}..{maxs1[0]:.4f}, y range {mins1[1]:.4f}..{maxs1[1]:.4f}")
print("Armature scale:", [round(v,4) for v in arm.scale])
print("Armature loc:", [round(v,4) for v in arm.location])

# verify head bone world z (approximate soldier height via bones)
pb = arm.pose.bones
if "Head" in pb:
    hz = (arm.matrix_world @ pb["Head"].head)[2]
    print("Head bone world z:", round(hz, 4))
if "Root" in pb:
    rz = (arm.matrix_world @ pb["Root"].head)
    print("Root bone world:", [round(v,4) for v in rz])
