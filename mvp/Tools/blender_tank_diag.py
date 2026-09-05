# -*- coding: utf-8 -*-
"""Diagnose ParentNode rotation vs child world matrices."""
import bpy
import math
from mathutils import Vector

parent = bpy.data.objects.get("ParentNode")
print("ParentNode:", parent)
if parent:
    print("  rotation_euler:", tuple(round(math.degrees(a),1) for a in parent.rotation_euler))
    print("  location:", tuple(round(v,3) for v in parent.location))
    print("  matrix_world:")
    for row in parent.matrix_world:
        print("   ", [round(v,4) for v in row])

m = bpy.data.objects.get("Tank_part0")
if m:
    print("Tank_part0 parent:", m.parent.name if m.parent else None)
    print("  local loc:", tuple(round(v,3) for v in m.location))
    print("  local rot:", tuple(round(math.degrees(a),1) for a in m.rotation_euler))
    print("  matrix_world:")
    for row in m.matrix_world:
        print("   ", [round(v,4) for v in row])

# Force a depsgraph update via the scene's view layer, then re-check.
bpy.context.view_layer.update()
m2 = bpy.data.objects.get("Tank_part0")
print("Tank_part0 matrix_world AFTER update:")
for row in m2.matrix_world:
    print("   ", [round(v,4) for v in row])
