# -*- coding: utf-8 -*-
"""Prepare the tank for FBX export:
1. Rename meshes tripo_part_N -> Tank_partN, materials -> Tank_partN.
2. Rotate +90 about X (Y-up -> Z-up, front stays -X) and lift onto the ground.
3. Unpack all basecolor/normal textures to Assets/Art/Battle/Units/Tank/Textures.
4. Set image filepaths to relative Textures/<Clean>_<Kind>.png.
"""
import bpy
import math
import os
import re

# ---------------------------------------------------------------- 1. rename
def clean(name):
    return re.sub(r"[^A-Za-z0-9_.]", "_", name)

for o in list(bpy.data.objects):
    if o.type != "MESH":
        continue
    m = re.match(r"tripo_part_(\d+)", o.name)
    if not m:
        continue
    newname = "Tank_part" + m.group(1)
    o.name = newname
    if o.material_slots and o.material_slots[0].material is not None:
        o.material_slots[0].material.name = newname

print("meshes renamed:", [o.name for o in bpy.data.objects if o.type == "MESH"][:3], "...")
print("material count:", len(bpy.data.materials))

# ---------------------------------------------------------------- 2. rotate + lift
parent = bpy.data.objects.get("ParentNode")
if parent is None:
    # fall back: create a root empty and parent all meshes under it
    parent = bpy.data.objects.new("Tank_Root", None)
    bpy.context.scene.collection.objects.link(parent)
    for o in list(bpy.data.objects):
        if o.type == "MESH":
            o.parent = parent
print("root node:", parent.name)
parent.rotation_euler = (math.pi / 2, 0, 0)   # +Y -> +Z (up), -X front unchanged
parent.location = (0, 0, 0.219)               # sit lowest point on z=0
bpy.context.view_layer.update()

# verify AABB
mins = [1e9] * 3
maxs = [-1e9] * 3
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    for c in o.bound_box:
        w = o.matrix_world @ __import__("mathutils").Vector(c)
        for i in range(3):
            mins[i] = min(mins[i], w[i])
            maxs[i] = max(maxs[i], w[i])
print("AABB min:", tuple(round(v, 3) for v in mins))
print("AABB max:", tuple(round(v, 3) for v in maxs))

# ---------------------------------------------------------------- 3. unpack textures
tex_dir = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Tank/Textures"
os.makedirs(tex_dir, exist_ok=True)
seen = {}
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    for slot in o.material_slots:
        m = slot.material
        if not m or m.node_tree is None:
            continue
        for n in m.node_tree.nodes:
            if n.type != "TEX_IMAGE" or n.image is None:
                continue
            img = n.image
            key = img.name
            if key in seen:
                continue
            ln = img.name.lower()
            if ln.endswith("basecolor"):
                kind = "BaseColor"
            elif ln.endswith("normal"):
                kind = "Normal"
            else:
                kind = "Tex"
            matname = clean(m.name)
            rel = "Textures/" + matname + "_" + kind + ".png"
            abs_path = os.path.join(tex_dir, matname + "_" + kind + ".png")
            # save the packed pixels to disk
            img.save_render(abs_path)
            img.filepath = rel
            seen[key] = rel
            print("unpacked", abs_path, "->", rel)

total = sum(os.path.getsize(os.path.join(tex_dir, f))
            for f in os.listdir(tex_dir) if f.endswith(".png"))
print("textures unpacked:", len(seen), "total bytes:", total)

# ---------------------------------------------------------------- save source
out_blend = r"D:/prounity/mvp/单位/坦克.blend"
bpy.ops.wm.save_as_mainfile(filepath=out_blend)
print("saved blend:", out_blend)
