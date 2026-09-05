# -*- coding: utf-8 -*-
"""Search for a 'take a knee' pose: L knee down, R foot planted forward."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]

def probe(tag, l_thigh, l_calf, r_thigh, r_calf, root_z=0.0):
    L.set_pose({
        "L_Thigh": (l_thigh, 0, 0), "L_Calf": (l_calf, 0, 0),
        "R_Thigh": (r_thigh, 0, 0), "R_Calf": (r_calf, 0, 0),
        "Spine01": (0.06, 0, 0), "Spine02": (0.03, 0, 0),
        "Head": (0, 0, -0.08),
    }, {"Root": (0, 0, root_z)})
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    lk = L.pose_world("L_Calf"); lf = L.pose_world("L_Foot")
    rk = L.pose_world("R_Calf"); rf = L.pose_world("R_Foot")
    wa = L.pose_world("Waist")
    print(f"{tag}: Waist z={wa.z:.3f} | L_knee z={lk.z:.3f} L_foot({lf.x:.3f},{lf.z:.3f}) | "
          f"R_knee z={rk.z:.3f} R_foot({rf.x:.3f},{rf.z:.3f})")

print("=== search: L knee down ===")
# vary L thigh (knee down) and L calf (foot placement)
for lt, lc in [(-0.8, -0.3), (-0.9, -0.4), (-1.0, -0.5), (-1.1, -0.6), (-1.2, -0.7), (-1.3, -0.8)]:
    probe(f"lt{lt:.1f}lc{lc:.1f}", lt, lc, -0.25, 0.05)

print("\n=== refine best ===")
for lt, lc in [(-1.0, -0.4), (-1.0, -0.5), (-1.1, -0.5), (-1.1, -0.6)]:
    probe(f"lt{lt:.1f}lc{lc:.1f}", lt, lc, -0.25, 0.05)

L.set_pose({}, {})
print("done")
