# -*- coding: utf-8 -*-
"""Runs inside Blender via blender.execute_script. Inspects imported soldier parts."""
import bpy
from mathutils import Vector

print("=== MESH PARTS ===")
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    bbox = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    dims = [round(max(p[i] for p in bbox) - min(p[i] for p in bbox), 4) for i in range(3)]
    center = [round((max(p[i] for p in bbox) + min(p[i] for p in bbox)) / 2, 4) for i in range(3)]
    mats = [ms.material.name if ms.material else "None" for ms in obj.material_slots]
    verts = len(obj.data.vertices)
    parent = obj.parent.name if obj.parent else "None"
    loc = [round(v, 4) for v in obj.location]
    vis = obj.visible_get()
    print(f"OBJ {obj.name}: verts={verts} dims(xyz)={dims} center={center} loc={loc} parent={parent} mats={mats} visible={vis}")

print("=== ALL OBJECTS ===")
for obj in bpy.data.objects:
    print(f"ALL {obj.name}: type={obj.type} loc={[round(v,3) for v in obj.location]} parent={obj.parent.name if obj.parent else 'None'}")
