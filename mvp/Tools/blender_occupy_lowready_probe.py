# -*- coding: utf-8 -*-
"""Probe spine lean + arm combos for a low-ready rifle drop (Occupy)."""
import bpy
import blender_anim_lib as L

def hands():
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lh = L.pose_world("L_Hand"); rh = L.pose_world("R_Hand")
    return lh, rh

L.set_pose({}, {})
L.dg = bpy.context.evaluated_depsgraph_get(); L.dg.update()
lr = L.pose_world("L_Hand"); rr = L.pose_world("R_Hand")
print(f"rest: L_Hand z={lr.z:.3f} R_Hand z={rr.z:.3f}")

# 1) Spine01 forward lean effect on hands
print("\n=== Spine01 X (lean) sweep, no arms ===")
for v in [0.0, 0.05, 0.10, 0.15, 0.20, 0.30, 0.40]:
    L.set_pose({"Spine01": (v, 0, 0)}, {})
    lh, rh = hands()
    print(f"  Spine01 x={v:+.2f}: L_Hand({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")

# 2) Spine02 X sweep
print("\n=== Spine02 X sweep ===")
for v in [0.0, 0.05, 0.10, 0.20]:
    L.set_pose({"Spine02": (v, 0, 0)}, {})
    lh, rh = hands()
    print(f"  Spine02 x={v:+.2f}: L_Hand({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")

# 3) full low-ready candidate combos
print("\n=== low-ready candidate combos ===")
cands = [
    ("A", {"Spine01": (0.15, 0, 0), "Spine02": (0.08, 0, 0), "Head": (0, 0, -0.10),
           "L_Upperarm": (0.02, 0, -0.20), "R_Upperarm": (0.02, 0, 0.20),
           "L_Forearm": (0, 0, -0.30), "R_Forearm": (0, 0, 0.30)}),
    ("B", {"Spine01": (0.20, 0, 0), "Spine02": (0.10, 0, 0), "Head": (0, 0, -0.08),
           "L_Upperarm": (0.02, 0, -0.25), "R_Upperarm": (0.02, 0, 0.25),
           "L_Forearm": (0, 0, -0.40), "R_Forearm": (0, 0, 0.40)}),
    ("C", {"Spine01": (0.12, 0, 0), "Spine02": (0.06, 0, 0), "Head": (0, 0, -0.12),
           "L_Upperarm": (0.02, 0, -0.15), "R_Upperarm": (0.02, 0, 0.15),
           "L_Forearm": (0, 0, -0.25), "R_Forearm": (0, 0, 0.25)}),
    ("D", {"Spine01": (0.25, 0, 0), "Spine02": (0.12, 0, 0), "Head": (0, 0, -0.06),
           "L_Upperarm": (0.02, 0, -0.30), "R_Upperarm": (0.02, 0, 0.30),
           "L_Forearm": (0, 0, -0.50), "R_Forearm": (0, 0, 0.50)}),
]
for tag, pose in cands:
    L.set_pose(pose, {})
    lh, rh = hands()
    wa = L.pose_world("Waist")
    he = L.pose_world("Head")
    print(f"  {tag}: L_Hand({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand({rh.x:.3f},{rh.y:.3f},{rh.z:.3f}) "
          f"Waist z={wa.z:.3f} Head({he.x:.3f},{he.z:.3f})")

L.set_pose({}, {})
print("\ndone")
