# -*- coding: utf-8 -*-
"""Probe combined arm-only low-ready combos + rifle muzzle drop/angle."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]
rif = bpy.data.objects["Infantry_Rifle"]

def eval_state():
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lh = L.pose_world("L_Hand"); rh = L.pose_world("R_Hand")
    # rifle muzzle = min world x vertex of evaluated (skinned) mesh
    deps = L.dg
    eval_rif = rif.evaluated_get(deps)
    m = eval_rif.matrix_world
    best = None
    for v in eval_rif.data.vertices:
        w = m @ v.co
        if best is None or w.x < best.x:
            best = w
    return lh, rh, best

def probe(tag, pose):
    L.set_pose(pose, {})
    lh, rh, muz = eval_state()
    print(f"{tag}: L_Hand z={lh.z:.3f} R_Hand z={rh.z:.3f} | "
          f"muzzle({muz.x:.3f},{muz.y:.3f},{muz.z:.3f}) drop={0.0 - (0.0 if muz.z>1.0 else 1.0-muz.z):.3f}")

# reference rest
L.set_pose({}, {})
lh, rh, muz = eval_state()
print(f"rest:  L_Hand z={lh.z:.3f} R_Hand z={rh.z:.3f} | muzzle({muz.x:.3f},{muz.y:.3f},{muz.z:.3f})")

hunch = {"Spine01": (0.08, 0, 0), "Spine02": (0.04, 0, 0), "Head": (0, 0, -0.15)}
cands = [
    ("E", dict(hunch, **{"L_Upperarm": (0.02, 0, -0.25), "R_Upperarm": (0.02, 0, 0.25),
                         "L_Forearm": (0, 0, -0.35), "R_Forearm": (0, 0, 0.45)})),
    ("F", dict(hunch, **{"L_Upperarm": (0.02, 0, -0.30), "R_Upperarm": (0.02, 0, 0.30),
                         "L_Forearm": (0, 0, -0.50), "R_Forearm": (0, 0, 0.60)})),
    ("G", dict(hunch, **{"L_Upperarm": (0.02, 0.20, -0.25), "R_Upperarm": (0.02, 0, 0.30),
                         "L_Forearm": (0, 0, -0.45), "R_Forearm": (0, 0, 0.55)})),
    ("H", {"Spine01": (0.12, 0, 0), "Spine02": (0.06, 0, 0), "Head": (0, 0, -0.12),
           "L_Upperarm": (0.02, 0.15, -0.30), "R_Upperarm": (0.02, 0, 0.30),
           "L_Forearm": (0, 0, -0.50), "R_Forearm": (0, 0, 0.60)}),
]
for tag, pose in cands:
    probe(tag, pose)

L.set_pose({}, {})
print("\ndone")
