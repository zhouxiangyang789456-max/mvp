# -*- coding: utf-8 -*-
"""Leg chain geometry + candidate kneel pose test for Occupy."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]

# 1) rest leg chain positions
L.set_pose({}, {})
L.dg = bpy.context.evaluated_depsgraph_get()
L.dg.update()
print("=== rest leg chain ===")
for bn in ["Root", "Hip", "Pelvis", "Waist", "L_Thigh", "L_Calf", "L_Foot", "L_ToeBase",
           "R_Thigh", "R_Calf", "R_Foot", "R_ToeBase"]:
    p = L.pose_world(bn)
    print(f"  {bn}: ({p.x:.3f}, {p.y:.3f}, {p.z:.3f})")

# 2) candidate kneel poses
def test(tag, pose, loc=None):
    L.set_pose(pose, loc)
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lf = L.pose_world("L_Foot"); rf = L.pose_world("R_Foot")
    wa = L.pose_world("Waist")
    lk = L.pose_world("L_Calf"); rk = L.pose_world("R_Calf")
    print(f"{tag}: Waist z={wa.z:.3f} | L_Foot ({lf.x:.3f},{lf.z:.3f}) R_Foot ({rf.x:.3f},{rf.z:.3f}) | "
          f"L_Calf z={lk.z:.3f} R_Calf z={rk.z:.3f}")

print("\n=== kneel candidates ===")
# both knees bent, hips drop (squat). Root loc drops to lower hips.
test("squat1", {
    "L_Thigh": (-0.4, 0, 0), "L_Calf": (0.6, 0, 0),
    "R_Thigh": (-0.4, 0, 0), "R_Calf": (0.6, 0, 0),
    "Spine01": (0.05, 0, 0), "Spine02": (0.03, 0, 0),
}, {"Root": (0, 0, -0.25)})

test("kneel_L_down", {
    # L knee down: L thigh forward, L calf bent back (shin on ground)
    "L_Thigh": (-0.9, 0, 0), "L_Calf": (0.9, 0, 0),
    # R foot planted forward
    "R_Thigh": (-0.5, 0, 0), "R_Calf": (0.2, 0, 0),
    "Spine01": (0.08, 0, 0), "Spine02": (0.04, 0, 0),
}, {"Root": (0, 0, -0.10)})

test("kneel_R_down", {
    "R_Thigh": (-0.9, 0, 0), "R_Calf": (0.9, 0, 0),
    "L_Thigh": (-0.5, 0, 0), "L_Calf": (0.2, 0, 0),
    "Spine01": (0.08, 0, 0), "Spine02": (0.04, 0, 0),
}, {"Root": (0, 0, -0.10)})

L.set_pose({}, {})
print("done")
