# -*- coding: utf-8 -*-
"""Fix the tank root transform: rotation_mode=XYZ then rotate + lift."""
import bpy
import math
from mathutils import Vector

parent = bpy.data.objects.get("ParentNode")
if parent is None:
    raise RuntimeError("ParentNode not found")

parent.rotation_mode = "XYZ"
parent.rotation_euler = (math.pi / 2, 0, 0)   # +Y -> +Z up, -X front unchanged
parent.location = (0, 0, 0.219)               # sit lowest point on z=0
bpy.context.view_layer.update()

print("ParentNode rot:", tuple(round(math.degrees(a),1) for a in parent.rotation_euler))
print("ParentNode matrix_world:")
for row in parent.matrix_world:
    print("   ", [round(v,4) for v in row])

# verify AABB
mins = [1e9] * 3
maxs = [-1e9] * 3
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mins[i] = min(mins[i], w[i])
            maxs[i] = max(maxs[i], w[i])
print("AABB min:", tuple(round(v,3) for v in mins))
print("AABB max:", tuple(round(v,3) for v in maxs))
print("size:", tuple(round(maxs[i]-mins[i],3) for i in range(3)))

bpy.ops.wm.save_as_mainfile(filepath=r"D:/prounity/mvp/单位/坦克.blend")
print("saved 坦克.blend")
