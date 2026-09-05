# -*- coding: utf-8 -*-
"""Sweep thigh rotation -> foot world position, isolated per leg."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]

def foot_pos(pose):
    L.set_pose(pose, None)
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lf = L.pose_world("L_Foot"); rf = L.pose_world("R_Foot")
    return lf, rf

# baseline foot positions (all identity)
lb, rb = foot_pos({})
print(f"REST: L_Foot x={lb.x:.3f} z={lb.z:.3f} | R_Foot x={rb.x:.3f} z={rb.z:.3f}")
print(f"      L_Foot y={lb.y:.3f} | R_Foot y={rb.y:.3f}")

# L leg: vary L_Thigh, R kept identity. calf at 0.0 then 0.4
print("\n=== L_Thigh sweep (L_Calf=0) ===")
for v in [-0.4, -0.3, -0.2, -0.1, 0.0, 0.1, 0.2, 0.3, 0.4]:
    lf, _ = foot_pos({"L_Thigh": (v, 0, 0)})
    print(f"  L_Thigh={v:+.2f}: L_Foot x={lf.x:+.3f} z={lf.z:+.3f} (dx={lf.x-lb.x:+.3f} dz={lf.z-lb.z:+.3f})")

print("\n=== R_Thigh sweep (R_Calf=0) ===")
for v in [-0.4, -0.3, -0.2, -0.1, 0.0, 0.1, 0.2, 0.3, 0.4]:
    _, rf = foot_pos({"R_Thigh": (v, 0, 0)})
    print(f"  R_Thigh={v:+.2f}: R_Foot x={rf.x:+.3f} z={rf.z:+.3f} (dx={rf.x-rb.x:+.3f} dz={rf.z-rb.z:+.3f})")

# calf effects: fix thigh at 0, vary calf
print("\n=== L_Calf sweep (L_Thigh=0) ===")
for v in [0.0, 0.2, 0.4, 0.6]:
    lf, _ = foot_pos({"L_Calf": (v, 0, 0)})
    print(f"  L_Calf={v:+.2f}: L_Foot x={lf.x:+.3f} z={lf.z:+.3f} (dx={lf.x-lb.x:+.3f} dz={lf.z-lb.z:+.3f})")

print("\n=== R_Calf sweep (R_Thigh=0) ===")
for v in [0.0, 0.2, 0.4, 0.6]:
    _, rf = foot_pos({"R_Calf": (v, 0, 0)})
    print(f"  R_Calf={v:+.2f}: R_Foot x={rf.x:+.3f} z={rf.z:+.3f} (dx={rf.x-rb.x:+.3f} dz={rf.z-rb.z:+.3f})")

L.set_pose({}, {})
print("done")
