# -*- coding: utf-8 -*-
"""Inspect the exported FBX for clip names, meshes, textures (binary scan)."""
import re
import sys

path = sys.argv[1] if len(sys.argv) > 1 else r"D:/prounity/mvp/mvp/Tools/_export/Infantry.fbx"
data = open(path, "rb").read()
print("file size:", len(data))

for name in ["Idle", "Move", "Attack", "Occupy", "Infantry_Rifle",
             "Soldier_Chest", "Soldier_Head", "Armature", "L_Thigh",
             "R_Thigh", "L_Calf", "Root", "Hip", "Waist"]:
    print(f"{name}: {data.count(name.encode())}")

print("--- texture file refs found in FBX ---")
pats = re.findall(rb"[A-Za-z0-9_./\\\\:]+_BaseColor\.png|[A-Za-z0-9_./\\\\:]+_Normal\.png", data)
seen = set()
for p in pats:
    s = p.decode("latin1")
    if s not in seen:
        seen.add(s)
        print(" ", s)
print("unique texture refs:", len(seen))
