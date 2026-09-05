# -*- coding: utf-8 -*-
"""Report material->texture mapping and scene fps (pre-export planning)."""
import bpy

scene = bpy.context.scene
print("fps:", scene.render.fps, "frame_start/end:",
      scene.frame_start, scene.frame_end)

# meshes we will export
export_meshes = [o for o in bpy.data.objects
                 if o.type == "MESH" and o.name != "Infantry_Armature"]
print("\nexport meshes and their materials:")
for o in bpy.data.objects:
    if o.type == "MESH" and o.name in ("Soldier_Head", "Soldier_Chest", "Infantry_Rifle"):
        for slot in o.material_slots:
            m = slot.material
            print(f"  {o.name} -> {m.name if m else None}")

print("\nmaterial -> image texture nodes:")
for m in bpy.data.materials:
    if m.node_tree is None:
        continue
    for n in m.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None:
            img = n.image
            # determine socket usage
            usage = "?"
            for out in n.outputs:
                if out.name == "Color" and out.links:
                    usage = "BaseColor"
                elif out.name == "Normal" and out.links:
                    usage = "Normal"
            print(f"  {m.name} | node {n.name} -> {img.name} "
                  f"({img.size[0]}x{img.size[1]}) packed={img.packed_file is not None} "
                  f"usage={usage} filepath={img.filepath!r}")
