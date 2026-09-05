# -*- coding: utf-8 -*-
"""Unpack + rename soldier textures into the Unity Infantry folder as PNG."""
import os
import re

tex_dir = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Textures"
os.makedirs(tex_dir, exist_ok=True)

export_mesh_names = {o.name for o in bpy.data.objects
                     if o.type == "MESH" and (o.name.startswith("Soldier_")
                                              or o.name == "Infantry_Rifle")}


def clean(name):
    return re.sub(r"[^A-Za-z0-9_.]", "_", name)


processed = set()
for o in bpy.data.objects:
    if o.name not in export_mesh_names:
        continue
    for slot in o.material_slots:
        m = slot.material
        if not m or m.node_tree is None:
            continue
        for n in m.node_tree.nodes:
            if n.type != "TEX_IMAGE" or n.image is None:
                continue
            img = n.image
            if img.name in processed:
                continue
            processed.add(img.name)
            ln = img.name.lower()
            if ln.endswith("basecolor"):
                kind = "BaseColor"
            elif ln.endswith("normal"):
                kind = "Normal"
            else:
                kind = "Tex"
            fname = f"{clean(m.name)}_{kind}.png"
            abs_path = os.path.join(tex_dir, fname)
            img.filepath_raw = abs_path
            img.save_render(abs_path)
            img.filepath = abs_path
            print(f"saved {img.name} ({img.size[0]}x{img.size[1]}) -> {fname}")

print("total textures:", len(processed))
