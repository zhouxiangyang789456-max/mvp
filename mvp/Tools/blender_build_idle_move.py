# -*- coding: utf-8 -*-
"""Author Idle and Move actions on the soldier armature."""
import bpy
import blender_anim_lib as L

L.clear_animation()

# ---------------- IDLE (1..90) ----------------
IDLE_BONES = ["Spine01", "Spine02", "Head", "Root"]
idle_keys = [
    (1,   {"Spine01": (0, 0, 0),      "Spine02": (0, 0, 0),      "Head": (0, 0, 0), "Root": (0, 0, 0)}),
    (30,  {"Spine01": (-0.025, 0, 0), "Spine02": (-0.018, 0, 0), "Head": (0, 0.03, 0), "Root": (0, 0, 0.008)}),
    (60,  {"Spine01": (0.012, 0, 0),  "Spine02": (0.008, 0, 0),  "Head": (0, -0.03, 0), "Root": (0, 0, -0.008)}),
    (90,  {"Spine01": (0, 0, 0),      "Spine02": (0, 0, 0),      "Head": (0, 0, 0), "Root": (0, 0, 0)}),
]
L.build_action("Idle", [(f, p, {}) for f, p in idle_keys], IDLE_BONES)
print("built Idle")

# ---------------- MOVE (1..30, tactical walk cycle) ----------------
# Bent-knee low stance: keeps feet near ground given the rig's mirrored
# thigh rolls (R_Thigh+ drives the foot down, R forward lifts high).
MOVE_BONES = ["L_Thigh", "R_Thigh", "L_Calf", "R_Calf", "Spine01", "Spine02"]

contactA = {
    "L_Thigh": (-0.10, 0, 0), "L_Calf": (0.15, 0, 0),
    "R_Thigh": (0.10, 0, 0),  "R_Calf": (0.55, 0, 0),
    "Spine01": (0.03, 0, 0),  "Spine02": (0.015, 0, 0),
}
pass_pose = {
    "L_Thigh": (0, 0, 0), "L_Calf": (0.18, 0, 0),
    "R_Thigh": (0, 0, 0), "R_Calf": (0.18, 0, 0),
    "Spine01": (0.02, 0, 0), "Spine02": (0.01, 0, 0),
}
contactB = {
    "R_Thigh": (-0.06, 0, 0), "R_Calf": (0.15, 0, 0),
    "L_Thigh": (0.10, 0, 0),  "L_Calf": (0.45, 0, 0),
    "Spine01": (0.03, 0, 0),  "Spine02": (0.015, 0, 0),
}

move_keys = [
    (1,  contactA, {"Root": (0, 0, 0)}),
    (8,  pass_pose, {"Root": (0, 0, 0.015)}),
    (15, contactB, {"Root": (0, 0, 0)}),
    (22, pass_pose, {"Root": (0, 0, 0.015)}),
    (30, contactA, {"Root": (0, 0, 0)}),
]
L.build_action("Move", move_keys, MOVE_BONES, loc_bones=["Root"])
print("built Move")

# reset to rest pose
L.set_pose({}, {})
arm = bpy.data.objects["Armature"]
arm.animation_data.action = None

# verify Move foot lift across the cycle
arm.animation_data.action = bpy.data.actions["Move"]
# bones must be in XYZ mode for euler fcurves to drive playback
L.set_pose({bn: (0, 0, 0) for bn in MOVE_BONES}, {"Root": (0, 0, 0)})
mins = {"L": 9, "R": 9}
for f in range(1, 31):
    bpy.context.scene.frame_set(f)
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lf = L.pose_world("L_Foot"); rf = L.pose_world("R_Foot")
    mins["L"] = min(mins["L"], lf.z); mins["R"] = min(mins["R"], rf.z)
    print(f"frame {f}: L_Foot z={round(lf.z,3)} R_Foot z={round(rf.z,3)}")
print("min foot z across cycle: L", round(mins["L"],3), "R", round(mins["R"],3))
arm.animation_data.action = None
bpy.context.scene.frame_set(1)
L.set_pose({}, {})
