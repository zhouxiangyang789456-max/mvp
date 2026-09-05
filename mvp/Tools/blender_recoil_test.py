# -*- coding: utf-8 -*-
"""Measure rifle muzzle displacement for different recoil approaches."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]
rif = bpy.data.objects["Infantry_Rifle"]

def rifle_muzzle():
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    eval_rif = rif.evaluated_get(L.dg)
    m = eval_rif.matrix_world
    best = None
    for v in eval_rif.data.vertices:
        w = m @ v.co
        if best is None or w.x < best.x:
            best = w
    return best

def measure(tag, pose, loc=None):
    L.set_pose(pose, loc)
    v = rifle_muzzle()
    print(f"{tag}: muzzle world x={v.x:.3f} z={v.z:.3f}")

aim = {
    "Spine01": (0.04, 0, 0), "Spine02": (0.02, 0, 0),
    "Head": (0, 0, -0.12),
    "L_Upperarm": (0.05, 0, 0.02), "R_Upperarm": (0.05, 0, 0.02),
    "L_Forearm": (0.05, 0, 0), "R_Forearm": (0.05, 0, 0),
}
print("=== aim (reference) ===")
measure("aim", aim)

recoil1 = {
    "Spine01": (-0.10, 0, 0), "Spine02": (-0.06, 0, 0),
    "Head": (0, 0, 0.08),
    "L_Clavicle": (0.15, 0, 0), "R_Clavicle": (0.15, 0, 0),
    "L_Upperarm": (0.15, 0, 0.02), "R_Upperarm": (0.15, 0, 0.02),
    "L_Forearm": (0.12, 0, 0), "R_Forearm": (0.12, 0, 0),
}
print("\n=== recoil1: spine+clavicle+arms back ===")
measure("r1", recoil1)

recoil2 = {
    "Spine01": (-0.06, 0, 0), "Spine02": (-0.04, 0, 0),
    "Head": (0, 0, 0.08),
    "L_Clavicle": (0.10, 0, 0), "R_Clavicle": (0.10, 0, 0),
    "L_Upperarm": (0.10, 0, 0.02), "R_Upperarm": (0.10, 0, 0.02),
    "L_Forearm": (0.08, 0, 0), "R_Forearm": (0.08, 0, 0),
}
print("\n=== recoil2: moderate spine+clavicle+arms ===")
measure("r2", recoil2)

# hand-location kick (on top of aim body)
recoil3_loc = {"L_Hand": (0.06, 0, 0.02), "R_Hand": (0.06, 0, 0.02)}
print("\n=== recoil3: aim body + hand loc kick +0.06 x ===")
measure("r3", aim, recoil3_loc)

recoil4_loc = {"L_Hand": (0.10, 0, 0.03), "R_Hand": (0.10, 0, 0.03)}
print("\n=== recoil4: aim body + hand loc kick +0.10 x ===")
measure("r4", aim, recoil4_loc)

L.set_pose({}, {})
print("done")
