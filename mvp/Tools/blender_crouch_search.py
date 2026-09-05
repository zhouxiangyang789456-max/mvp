# -*- coding: utf-8 -*-
"""Search for a moderate crouch with minimal foot sink."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]

def probe(tag, lt, lc, rt, rc, root_z, spine=0.06):
    L.set_pose({
        "L_Thigh": (lt, 0, 0), "L_Calf": (lc, 0, 0),
        "R_Thigh": (rt, 0, 0), "R_Calf": (rc, 0, 0),
        "Spine01": (spine, 0, 0), "Spine02": (spine*0.5, 0, 0),
        "Head": (0, 0, -0.08),
    }, {"Root": (0, 0, root_z)})
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lf = L.pose_world("L_Foot"); rf = L.pose_world("R_Foot")
    wa = L.pose_world("Waist")
    flag = "  <-- PEN" if (lf.z < 0.01 or rf.z < 0.01) else ""
    print(f"{tag}: Waist z={wa.z:.3f} | L_foot z={lf.z:.3f} R_foot z={rf.z:.3f}{flag}")

print("=== crouch search (root drop + leg bend) ===")
for rz in [-0.10, -0.15, -0.20]:
    for lt, lc in [(-0.2, 0.3), (-0.3, 0.5), (-0.4, 0.6), (-0.5, 0.8)]:
        # mirror for R using its own quirk (R thigh back sinks, forward lifts)
        probe(f"rz{rz:.2f} lt{lt:.1f}lc{lc:.1f}", lt, lc, lt, lc, rz)

L.set_pose({}, {})
print("done")
