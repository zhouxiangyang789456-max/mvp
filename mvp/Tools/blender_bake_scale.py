# -*- coding: utf-8 -*-
"""Bake the armature object scale into BOTH the mesh bind vertices and the bone
rest pose, so armature/mesh scale == 1 and the model is exactly 1.70m tall.

Must be done in one pass because at rest the Armature-modifier deformation is the
identity, so the object scale must be baked into bind vertices AND bone rest.
"""
import bpy
from mathutils import Vector

arm = bpy.data.objects["Armature"]
objs = [o for o in bpy.data.objects if o.type == 'MESH']

# 1) compute bind-space height from raw local vertex coords (not world matrix)
pts = []
for o in objs:
    pts += [Vector(c) for c in o.bound_box]
zs = [p.z for p in pts]
bind_h = max(zs) - min(zs)
s = 1.70 / bind_h
print(f"bind height {bind_h:.4f} -> factor {s:.4f} (arm.scale.x was {arm.scale.x:.4f})")

# 2) scale mesh bind vertices in local space (around armature origin at 0)
for o in objs:
    mesh = o.data
    for v in mesh.vertices:
        v.co.x *= s
        v.co.y *= s
        v.co.z *= s
    mesh.update()
print("mesh bind vertices scaled")

# 3) bake scale into bone rest via edit mode (temp_override gives active object)
override = {
    "active_object": arm,
    "selected_objects": [arm],
    "object": arm,
    "scene": bpy.context.scene,
    "view_layer": bpy.context.view_layer,
}
with bpy.context.temp_override(**override):
    bpy.ops.object.mode_set(mode="EDIT")
for eb in arm.data.edit_bones:
    eb.head.x *= s
    eb.head.y *= s
    eb.head.z *= s
    eb.tail.x *= s
    eb.tail.y *= s
    eb.tail.z *= s
with bpy.context.temp_override(**override):
    bpy.ops.object.mode_set(mode="OBJECT")
print("bone rest scaled")

# 4) reset armature transform to identity
arm.scale = (1.0, 1.0, 1.0)
arm.rotation_euler = (0.0, 0.0, 0.0)
arm.location = (0.0, 0.0, 0.0)

# 5) verify
pts2 = []
for o in objs:
    pts2 += [o.matrix_world @ Vector(c) for c in o.bound_box]
mins = [min(p[i] for p in pts2) for i in range(3)]
maxs = [max(p[i] for p in pts2) for i in range(3)]
print("VERIFY height:", round(maxs[2] - mins[2], 4), "zmin:", round(mins[2], 4))
print("VERIFY x range:", round(mins[0], 4), "..", round(maxs[0], 4))
print("VERIFY arm scale:", [round(v, 4) for v in arm.scale])
pb = arm.pose.bones
if "Head" in pb:
    print("VERIFY Head bone world z:", round((arm.matrix_world @ pb["Head"].head)[2], 4))
if "Root" in pb:
    print("VERIFY Root bone world:", [round(v, 4) for v in (arm.matrix_world @ pb["Root"].head)])
if "L_Hand" in pb and "L_Upperarm" in pb:
    lu = arm.matrix_world @ pb["L_Upperarm"].head
    lh = arm.matrix_world @ pb["L_Hand"].head
    print("VERIFY arm length (L_Upperarm->L_Hand):", round((lh - lu).length, 4))
