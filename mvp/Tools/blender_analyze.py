# -*- coding: utf-8 -*-
"""Analyze soldier model: classify each mesh part by dominant vertex-group bones,
compute key bone world positions, and check pose."""
import bpy
from mathutils import Vector

arm = bpy.data.objects["Armature"]
pose = arm.pose
scene = bpy.context.scene

print("=== KEY BONE WORLD POSITIONS ===")
def bone_world(bone_name):
    if bone_name not in pose.bones:
        return None
    return arm.matrix_world @ pose.bones[bone_name].head

for bn in ["Root", "Hip", "Waist", "Spine01", "Spine02", "NeckTwist01", "Head",
           "L_Clavicle", "L_Upperarm", "L_Forearm", "L_Hand",
           "R_Clavicle", "R_Upperarm", "R_Forearm", "R_Hand",
           "L_Thigh", "L_Calf", "L_Foot", "R_Thigh", "R_Calf", "R_Foot"]:
    p = bone_world(bn)
    if p:
        print(f"{bn}: {tuple(round(v,3) for v in p)}")

print()
print("=== MESH PART -> DOMINANT BONES (top weights by avg over vertices) ===")
for obj in bpy.data.objects:
    if obj.type != 'MESH' or obj.name.startswith('Icosphere'):
        continue
    mesh = obj.data
    groups = obj.vertex_groups
    # accumulate weight per group over all vertices
    wsum = {}
    for v in mesh.vertices:
        for g in v.groups:
            wsum.setdefault(g.group, 0.0)
            wsum[g.group] += g.weight
    # also count influences
    total_w = sum(wsum.values())
    ranked = sorted(wsum.items(), key=lambda kv: -kv[1])[:5]
    names = []
    for gi, w in ranked:
        gname = groups[gi].name if gi < len(groups) else f"?{gi}"
        names.append(f"{gname}({w/total_w*100:.0f}%)")
    print(f"OBJ {obj.name}: verts={len(mesh.vertices)} -> {', '.join(names)}")
    # check if object has any bone constraint or armature modifier
    mods = [m.type for m in obj.modifiers]
    print(f"    modifiers={mods}")

print()
print("=== POSE / BONE ROTATION (pose space) ===")
for bn in ["Root", "Hip", "L_Thigh", "R_Thigh", "L_Calf", "R_Calf",
           "L_Upperarm", "R_Upperarm", "L_Forearm", "R_Forearm", "Head"]:
    if bn in pose.bones:
        b = pose.bones[bn]
        r = b.rotation_quaternion if b.rotation_mode == 'QUATERNION' else b.rotation_euler
        vals = [float(x) for x in r]
        print(f"{bn}: rotation_mode={b.rotation_mode} rot={[round(v,3) for v in vals]}")

print()
print("=== SCENE ROOT/EMPTY transforms ===")
for obj in bpy.data.objects:
    if obj.type == 'EMPTY' or obj.name == 'Armature':
        print(f"{obj.name}: loc={[round(v,3) for v in obj.location]} rot={[round(v,3) for v in obj.rotation_euler]} scale={[round(v,3) for v in obj.scale]}")
