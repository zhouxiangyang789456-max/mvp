# -*- coding: utf-8 -*-
"""Export the tank (static meshes under ParentNode) to FBX.
Front = Blender -X -> Unity +Z (axis_forward='-X', axis_up='Z'),
matching the soldier export convention.
"""
import os
import re
import bpy

out_dir = r"D:/prounity/mvp/mvp/Tools/_export"
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "Tank.fbx")

# select ParentNode (empty) + all Tank_part* meshes
parent = bpy.data.objects.get("ParentNode")
bpy.ops.object.select_all(action="DESELECT")
if parent is not None:
    parent.select_set(True)
for o in bpy.data.objects:
    if o.type == "MESH" and o.name.startswith("Tank_part"):
        o.select_set(True)

scene = bpy.context.scene
view_layer = scene.view_layers[0]
view_layer.objects.active = parent if parent else None

# Re-assert relative texture paths (should already be set from prep).
def clean(name):
    return re.sub(r"[^A-Za-z0-9_.]", "_", name)

export_mesh_names = {o.name for o in bpy.data.objects
                     if o.type == "MESH" and o.name.startswith("Tank_part")}
seen = set()
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
            if img.name in seen:
                continue
            seen.add(img.name)
            ln = img.name.lower()
            if ln.endswith("basecolor"):
                kind = "BaseColor"
            elif ln.endswith("normal"):
                kind = "Normal"
            else:
                kind = "Tex"
            img.filepath = "Textures/" + clean(m.name) + "_" + kind + ".png"

print("exporting tank...")
with bpy.context.temp_override(scene=scene, view_layer=view_layer,
                               active_object=parent,
                               selected_objects=[o for o in bpy.data.objects if o.select_get()],
                               object=parent):
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        axis_forward="-X",
        axis_up="Z",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_mesh_modifiers=True,
    )
print("exported:", out_path, os.path.getsize(out_path), "bytes")
