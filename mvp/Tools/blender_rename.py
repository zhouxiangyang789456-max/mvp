# -*- coding: utf-8 -*-
"""Rename soldier parts and materials to clean English names. Also delete the hidden Icosphere."""
import bpy

NAME_MAP = {
    "tripo_part_0": "Soldier_TorsoL",
    "tripo_part_1": "Soldier_Chest",
    "tripo_part_2": "Soldier_Head",
    "tripo_part_3": "Soldier_LegL",
    "tripo_part_4": "Soldier_LegR",
    "tripo_part_5": "Soldier_ArmR",
    "tripo_part_6": "Infantry_Rifle",
    "tripo_part_7": "Soldier_Waist",
    "tripo_part_8": "Soldier_ArmL",
    "tripo_part_9": "Soldier_Helmet",
    "tripo_part_10": "Soldier_FootR",
    "tripo_part_11": "Soldier_HandL",
    "tripo_part_12": "Soldier_Pelvis",
    "tripo_part_13": "Soldier_HandR",
}

# Delete hidden Icosphere leftover
for obj in bpy.data.objects:
    if obj.name == "Icosphere":
        bpy.data.objects.remove(obj, do_unlink=True)
        print("DELETED Icosphere")

renamed = []
for old, new in NAME_MAP.items():
    obj = bpy.data.objects.get(old)
    if obj is None:
        print("MISSING OBJECT", old)
        continue
    # rename object and its mesh data
    obj.name = new
    if obj.data is not None and obj.data.name == old:
        obj.data.name = new + "_Mesh"
    # rename the single material on the object
    for slot in obj.material_slots:
        if slot.material and slot.material.name.startswith(old):
            slot.material.name = new
            renamed.append((old, new, slot.material.name))
        else:
            renamed.append((old, new, "material-not-prefixed"))

for r in renamed:
    print("RENAMED", r[0], "->", r[1], "| material:", r[2])
