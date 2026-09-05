# -*- coding: utf-8 -*-
"""Fix: the baked geometry is already correct (Z-up, 2x, bottom on z=0), but the
mesh world matrices retained a stale inverse-parent rotation. Reset them to
identity now that they are parented to the identity ParentNode root."""
import bpy
from mathutils import Matrix, Vector

meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Tank_part")]
for o in meshes:
    o.matrix_world = Matrix.Identity(4)
bpy.context.view_layer.update()

mins = [1e9] * 3
maxs = [-1e9] * 3
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mins[i] = min(mins[i], w[i])
            maxs[i] = max(maxs[i], w[i])
print("AABB min:", tuple(round(v, 3) for v in mins))
print("AABB max:", tuple(round(v, 3) for v in maxs))
print("size:", tuple(round(maxs[i] - mins[i], 3) for i in range(3)))

# sample: confirm lowest corner sits on z=0
low = None
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        if abs(w[2] - mins[2]) < 0.001:
            low = (o.name, tuple(round(v, 3) for v in w))
            break
    if low:
        break
print("low corner:", low)

bpy.ops.wm.save_as_mainfile(filepath=r"D:/prounity/mvp/单位/坦克.blend")
print("saved 坦克.blend")
