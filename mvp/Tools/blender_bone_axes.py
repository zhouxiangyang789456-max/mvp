# -*- coding: utf-8 -*-
"""Map skeleton world positions and diagnose pose-bone local axes so we can
author animations correctly (which axis = forward/back swing, lateral, twist).
"""
import bpy
from mathutils import Vector

arm = bpy.data.objects["Armature"]
pb = arm.pose.bones
dg = bpy.context.evaluated_depsgraph_get()

def clear_pose():
    for p in pb:
        p.rotation_mode = 'QUATERNION'
        p.rotation_quaternion = (1, 0, 0, 0)
        p.location = (0, 0, 0)

def bone_world(name):
    m = arm.matrix_world @ pb[name].matrix
    return m.translation

clear_pose()
dg.update()

print("=== SKELETON WORLD POSITIONS (rest) ===")
for bn in ["Root", "Hip", "Pelvis", "Waist", "Spine01", "Spine02", "NeckTwist01",
           "NeckTwist02", "Head",
           "L_Clavicle", "L_Upperarm", "L_Forearm", "L_Hand",
           "R_Clavicle", "R_Upperarm", "R_Forearm", "R_Hand",
           "L_Thigh", "L_Calf", "L_Foot", "L_ToeBase",
           "R_Thigh", "R_Calf", "R_Foot", "R_ToeBase"]:
    p = bone_world(bn)
    print(f"{bn}: ({round(p.x,3)}, {round(p.y,3)}, {round(p.z,3)})")

print()
print("=== FACING DETERMINATION ===")
# neck->head offset gives head up direction (z); face side: helmet/head mesh center
h = bone_world("Head")
neck = bone_world("NeckTwist02")
print("Neck->Head direction:", [round(h[i]-neck[i],3) for i in range(3)])
# chest normal: from Pelvis to Spine01 (up), plus a spine X/Y orientation check
pelvis = bone_world("Pelvis")
sp1 = bone_world("Spine01")
print("Pelvis->Spine01 dir:", [round(sp1[i]-pelvis[i],3) for i in range(3)])

print()
print("=== AXIS TESTS (tip displacement when parent rotated +0.5 rad) ===")
def try_axis(bone, axis, sign, tip):
    b = pb[bone]
    b.rotation_mode = 'XYZ'
    e = [0.0, 0.0, 0.0]
    e[axis] = sign * 0.5
    b.rotation_euler = e
    dg.update()
    t = bone_world(tip)
    b.rotation_euler = (0, 0, 0)
    dg.update()
    return t

tests = [
    ("L_Thigh", "L_Foot"), ("R_Thigh", "R_Foot"),
    ("L_Upperarm", "L_Hand"), ("R_Upperarm", "R_Hand"),
    ("L_Calf", "L_Foot"), ("R_Calf", "R_Foot"),
    ("L_Forearm", "L_Hand"), ("R_Forearm", "R_Hand"),
    ("Spine01", "Head"), ("Waist", "Head"), ("Head", "Head"),
    ("L_Clavicle", "L_Hand"), ("R_Clavicle", "R_Hand"),
]
for bone, tip in tests:
    base = bone_world(tip)
    rows = []
    for axis, an in [(0, "X"), (1, "Y"), (2, "Z")]:
        t = try_axis(bone, axis, 1.0, tip)
        d = [round(t[i] - base[i], 3) for i in range(3)]
        rows.append(f"    +{an}: dxyz={d}")
    print(f"{bone} -> {tip}: base=({round(base.x,3)},{round(base.y,3)},{round(base.z,3)})")
    for r in rows:
        print(r)
