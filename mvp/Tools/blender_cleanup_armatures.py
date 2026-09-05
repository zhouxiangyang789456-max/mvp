# -*- coding: utf-8 -*-
import bpy

for o in list(bpy.data.objects):
    if o.type == "ARMATURE" and o.name != "Armature":
        bpy.data.objects.remove(o, do_unlink=True)
print("armatures remaining:", [o.name for o in bpy.data.objects if o.type == "ARMATURE"])
print("actions remaining:", [a.name for a in bpy.data.actions])
