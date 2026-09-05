# -*- coding: utf-8 -*-
"""Rotate the tank geometry 180° about the vertical (Blender Z) axis so the
barrel (currently -X) points +X. After export (Blender +X -> Unity +Z) the tank
faces forward in Unity. Stays upright; bottom stays on z=0."""
import bpy
from mathutils import Matrix, Vector

meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Tank_part")]
rot = Matrix.Rotation(3.14159265358979, 4, 'Z')
for o in meshes:
    o.data.transform(rot)
    o.data.update()
bpy.context.view_layer.update()

mn = [1e9] * 3
mx = [-1e9] * 3
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for i in range(3):
            mn[i] = min(mn[i], w[i])
            mx[i] = max(mx[i], w[i])
print("AABB min=", tuple(round(v, 3) for v in mn), "max=", tuple(round(v, 3) for v in mx))

o = bpy.data.objects.get("Tank_part6")
if o:
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    xs = [p[0] for p in pts]
    print("Tank_part6 x range:", round(min(xs), 3), "to", round(max(xs), 3),
          "(tip should now be +X)")

bpy.ops.wm.save_as_mainfile(filepath=r"D:/prounity/mvp/单位/坦克.blend")
print("saved 坦克.blend")
