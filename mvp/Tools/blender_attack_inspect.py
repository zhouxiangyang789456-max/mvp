# -*- coding: utf-8 -*-
"""Get rifle world bbox and compare to hands/body."""
import bpy
import blender_anim_lib as L
from mathutils import Vector

arm = bpy.data.objects["Armature"]
L.set_pose({}, {})
L.dg = bpy.context.evaluated_depsgraph_get()
L.dg.update()

def world_bbox(o):
    wm = o.matrix_world
    corners = [wm @ Vector(c) for c in o.bound_box]
    mins = [min(c[i] for c in corners) for i in range(3)]
    maxs = [max(c[i] for c in corners) for i in range(3)]
    return mins, maxs

rif = bpy.data.objects["Infantry_Rifle"]
mns, mxs = world_bbox(rif)
print("RIFLE world bbox:")
print(f"  x: {mns[0]:.3f} .. {mxs[0]:.3f}")
print(f"  y: {mns[1]:.3f} .. {mxs[1]:.3f}")
print(f"  z: {mns[2]:.3f} .. {mxs[2]:.3f}")
print(f"  center: ({(mns[0]+mxs[0])/2:.3f}, {(mns[1]+mxs[1])/2:.3f}, {(mns[2]+mxs[2])/2:.3f})")

# soldier body bbox
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.name != "Infantry_Rifle"]
b_mns = [9]*3; b_mxs = [-9]*3
for o in meshes:
    m, x = world_bbox(o)
    for i in range(3):
        b_mns[i] = min(b_mns[i], m[i]); b_mxs[i] = max(b_mxs[i], x[i])
print("\nBODY world bbox:")
print(f"  x: {b_mns[0]:.3f} .. {b_mxs[0]:.3f}")
print(f"  y: {b_mns[1]:.3f} .. {b_mxs[1]:.3f}")
print(f"  z: {b_mns[2]:.3f} .. {b_mxs[2]:.3f}")

# distance from rifle to hands (nearest rifle point to each hand)
lh = L.pose_world("L_Hand"); rh = L.pose_world("R_Hand")
print(f"\nL_Hand: ({lh.x:.3f},{lh.y:.3f},{lh.z:.3f})")
print(f"R_Hand: ({rh.x:.3f},{rh.y:.3f},{rh.z:.3f})")

# rifle length/direction
print("\nrifle len:", round(mxs[0]-mns[0], 3), "muzzle end min-x or max-x?")
print("  min-x end y-extent will show stock vs barrel")
