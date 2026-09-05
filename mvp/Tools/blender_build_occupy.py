# -*- coding: utf-8 -*-
"""Build Occupy (standing low-ready hold loop) on the soldier armature."""
import bpy
import blender_anim_lib as L

OCCUPY_BONES = ["Spine01", "Spine02", "Head", "L_Upperarm", "R_Upperarm",
                "L_Forearm", "R_Forearm"]

# Low-ready hold: rifle dropped ~9cm via forearm Z bends + upperarm Z +
# slight spine hunch. L_Forearm -Z and R_Forearm +Z are the axis quirks that
# bring each hand down on this rig.
hold = {
    "Spine01": (0.08, 0, 0),
    "Spine02": (0.04, 0, 0),
    "Head": (0, 0, -0.15),
    "L_Upperarm": (0.02, 0, -0.30),
    "R_Upperarm": (0.02, 0, 0.30),
    "L_Forearm": (0, 0, -0.50),
    "R_Forearm": (0, 0, 0.60),
}
breathe = {
    "Spine01": (0.06, 0, 0),
    "Spine02": (0.03, 0, 0),
    "Head": (0, 0, -0.13),
    "L_Upperarm": (0.02, 0, -0.25),
    "R_Upperarm": (0.02, 0, 0.25),
    "L_Forearm": (0, 0, -0.40),
    "R_Forearm": (0, 0, 0.50),
}

if "Occupy" in bpy.data.actions:
    bpy.data.actions.remove(bpy.data.actions["Occupy"])

keys = [
    (1,  hold),
    (15, breathe),
    (30, hold),
]
L.build_action("Occupy", [(f, p, {}) for f, p in keys], OCCUPY_BONES)
print("built Occupy")

# verify hand/head positions at hold pose
arm = bpy.data.objects["Armature"]
arm.animation_data.action = bpy.data.actions["Occupy"]
L.set_pose(hold, {})
L.dg = bpy.context.evaluated_depsgraph_get()
L.dg.update()
lh = L.pose_world("L_Hand"); rh = L.pose_world("R_Hand")
he = L.pose_world("Head")
print(f"hold: L_Hand=({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand=({rh.x:.3f},{rh.y:.3f},{rh.z:.3f}) Head=({he.x:.3f},{he.y:.3f},{he.z:.3f})")
# compare to rest hands
L.set_pose({}, {})
L.dg.update()
lh0 = L.pose_world("L_Hand"); rh0 = L.pose_world("R_Hand")
print(f"rest: L_Hand=({lh0.x:.3f},{lh0.y:.3f},{lh0.z:.3f}) R_Hand=({rh0.x:.3f},{rh0.y:.3f},{rh0.z:.3f})")
print(f"hand drop: L {lh0.z-lh.z:.3f} R {rh0.z-rh.z:.3f}")

arm.animation_data.action = None
bpy.context.scene.frame_set(1)
L.set_pose({}, {})
print("done")
