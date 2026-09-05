# -*- coding: utf-8 -*-
"""Probe upperarm/forearm axes to find a low-ready rifle drop for Occupy."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]

def hands():
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lh = L.pose_world("L_Hand"); rh = L.pose_world("R_Hand")
    return lh, rh

base = {
    "Spine01": (0.05, 0, 0), "Spine02": (0.025, 0, 0), "Head": (0, 0, -0.15),
}

# rest hand positions for reference
L.set_pose({}, {})
L.dg = bpy.context.evaluated_depsgraph_get(); L.dg.update()
lr = L.pose_world("L_Hand"); rr = L.pose_world("R_Hand")
print(f"rest: L_Hand z={lr.z:.3f} R_Hand z={rr.z:.3f}")

# sweep each axis on each arm bone independently
print("\n=== L_Upperarm axis sweep (applied alone over base+arms at 0) ===")
for ax_i in range(3):
    ax = "xyz"[ax_i]
    for v in [-0.3, -0.2, -0.1, 0.1, 0.2, 0.3, 0.5, 0.8]:
        e = [0.0, 0.0, 0.0]; e[ax_i] = v
        pose = dict(base)
        pose["L_Upperarm"] = tuple(e)
        L.set_pose(pose, {})
        lh, rh = hands()
        print(f"  L_Upperarm {ax}={v:+.2f}: L_Hand({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")

print("\n=== R_Upperarm axis sweep ===")
for ax_i in range(3):
    ax = "xyz"[ax_i]
    for v in [-0.3, -0.2, -0.1, 0.1, 0.2, 0.3, 0.5, 0.8]:
        e = [0.0, 0.0, 0.0]; e[ax_i] = v
        pose = dict(base)
        pose["R_Upperarm"] = tuple(e)
        L.set_pose(pose, {})
        lh, rh = hands()
        print(f"  R_Upperarm {ax}={v:+.2f}: L_Hand({lh.x:.3f},{lh.y:.3f},{lh.z:.3f}) R_Hand({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")

print("\n=== L_Forearm axis sweep (arms at 0, forearms varied) ===")
for ax_i in range(3):
    ax = "xyz"[ax_i]
    for v in [-0.3, -0.1, 0.1, 0.3, 0.6, 0.9]:
        e = [0.0, 0.0, 0.0]; e[ax_i] = v
        pose = dict(base)
        pose["L_Forearm"] = tuple(e)
        L.set_pose(pose, {})
        lh, rh = hands()
        print(f"  L_Forearm {ax}={v:+.2f}: L_Hand({lh.x:.3f},{lh.y:.3f},{lh.z:.3f})")

print("\n=== R_Forearm axis sweep ===")
for ax_i in range(3):
    ax = "xyz"[ax_i]
    for v in [-0.3, -0.1, 0.1, 0.3, 0.6, 0.9]:
        e = [0.0, 0.0, 0.0]; e[ax_i] = v
        pose = dict(base)
        pose["R_Forearm"] = tuple(e)
        L.set_pose(pose, {})
        lh, rh = hands()
        print(f"  R_Forearm {ax}={v:+.2f}: R_Hand({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")

L.set_pose({}, {})
print("\ndone")
