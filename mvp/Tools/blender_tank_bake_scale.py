# -*- coding: utf-8 -*-
"""Fix tank export: bake ParentNode rotation/location into mesh geometry so the
FBX root is identity (matches the soldier export), then scale 2x.

Steps:
1. For each Tank_part mesh: bake world transform into vertices, reset local to identity.
2. Scale all vertices 2x about the origin (tank bottom stays on z=0).
3. Drop the old rotated ParentNode; create a fresh identity ParentNode root.
4. Verify AABB (expect ~1.958 x 0.852 x 0.874) and save the .blend.
"""
import bpy
from mathutils import Matrix, Vector

meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Tank_part")]
print("meshes:", len(meshes))

# 1) Bake world transform into vertices, reset local to identity.
for o in meshes:
    o.data.transform(o.matrix_world)   # vertices -> world coordinates (Z-up, upright, on ground)
    o.matrix_world = Matrix.Identity(4)
    o.data.update()

# 2) Scale 2x about origin.
scale2 = Matrix.Scale(2.0, 4)
for o in meshes:
    o.data.transform(scale2)
    o.data.update()

# 3) Remove old ParentNode, create a fresh identity root.
old = bpy.data.objects.get("ParentNode")
if old is not None:
    bpy.data.objects.remove(old, do_unlink=True)

new_root = bpy.data.objects.new("ParentNode", None)
bpy.context.scene.collection.objects.link(new_root)
for o in meshes:
    o.parent = new_root

bpy.context.view_layer.update()

# 4) Verify AABB.
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

bpy.ops.wm.save_as_mainfile(filepath=r"D:/prounity/mvp/单位/坦克.blend")
print("saved 坦克.blend")
