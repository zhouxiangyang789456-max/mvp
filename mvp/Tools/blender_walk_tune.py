# -*- coding: utf-8 -*-
"""Tune R forward swing with calf to place foot low & forward."""
import bpy
import blender_anim_lib as L

def foot_pos(pose):
    L.set_pose(pose, None)
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    rf = L.pose_world("R_Foot")
    return rf

print("=== R forward (thigh neg) x calf (find low-z forward) ===")
for t in [-0.10, -0.15, -0.20, -0.25]:
    for c in [0.0, 0.2, 0.4, 0.5, 0.6]:
        rf = foot_pos({"R_Thigh": (t, 0, 0), "R_Calf": (c, 0, 0)})
        flag = "  <-- PEN" if rf.z < 0.010 else ""
        print(f"  R_Thigh={t:+.2f} R_Calf={c:+.2f} -> R_Foot x={rf.x:+.3f} z={rf.z:+.3f}{flag}")
    print()

print("=== R back (thigh pos) x calf (find ground-level back) ===")
for t in [0.08, 0.10, 0.12, 0.15]:
    for c in [0.2, 0.3, 0.4, 0.5]:
        rf = foot_pos({"R_Thigh": (t, 0, 0), "R_Calf": (c, 0, 0)})
        flag = "  <-- PEN" if rf.z < 0.010 else ""
        print(f"  R_Thigh={t:+.2f} R_Calf={c:+.2f} -> R_Foot x={rf.x:+.3f} z={rf.z:+.3f}{flag}")
    print()

L.set_pose({}, {})
print("done")
