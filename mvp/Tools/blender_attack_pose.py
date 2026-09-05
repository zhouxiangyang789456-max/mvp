# -*- coding: utf-8 -*-
"""Test Attack aim pose: hand positions + head forward direction."""
import bpy
import blender_anim_lib as L
from mathutils import Vector

arm = bpy.data.objects["Armature"]
pb = arm.pose.bones

def bone_matrix(name):
    return arm.matrix_world @ pb[name].matrix

def head_fwd():
    m = bone_matrix("Head")
    # local +Z direction in world
    z = (m.to_3x3() @ Vector((0, 0, 1))).normalized()
    return z

def show_aim(pose):
    L.set_pose(pose, None)
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lh = L.pose_world("L_Hand"); rh = L.pose_world("R_Hand")
    hf = head_fwd()
    print(f"  L_Hand=({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand=({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")
    print(f"  Head fwd(z)=({hf.x:.3f},{hf.y:.3f},{hf.z:.3f})  pitch={hf.z:.3f}")

# rest reference
print("=== rest ===")
show_aim({})

# aim pose
aim = {
    "Spine01": (0.04, 0, 0),
    "Spine02": (0.02, 0, 0),
    "Head": (-0.10, 0, 0),   # try look down
    "L_Upperarm": (0.10, 0, 0.05),
    "L_Forearm": (0.10, 0, 0),
    "R_Upperarm": (-0.05, 0, 0.05),
    "R_Forearm": (0.10, 0, 0),
}
print("\n=== aim v1 ===")
show_aim(aim)

aim2 = dict(aim)
aim2["Head"] = (0.0, 0, -0.10)  # try other axis for look down
print("\n=== aim v2 (head -Z) ===")
show_aim(aim2)

aim3 = dict(aim)
aim3["Head"] = (0.0, -0.10, 0)  # try Y axis
print("\n=== aim v3 (head -Y) ===")
show_aim(aim3)

L.set_pose({}, {})
print("done")
