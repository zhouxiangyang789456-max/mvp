# -*- coding: utf-8 -*-
"""Export the soldier (Armature + meshes + 4 actions) to FBX.
Front = Blender -X -> Unity +Z (axis_forward='-X', axis_up='Z').
"""
import os
import bpy

out_dir = r"D:/prounity/mvp/mvp/Tools/_export"
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "Infantry.fbx")

# select Armature + soldier meshes
arm = bpy.data.objects["Armature"]
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
for o in bpy.data.objects:
    if o.type == "MESH" and (o.name.startswith("Soldier_")
                             or o.name == "Infantry_Rifle"):
        o.select_set(True)

scene = bpy.context.scene
view_layer = scene.view_layers[0]
view_layer.objects.active = arm

# Set image filepaths to RELATIVE "Textures/<CleanName>.png" so the FBX writes
# relative paths natively (no post-export byte patching that can corrupt it).
def clean(name):
    import re
    return re.sub(r"[^A-Za-z0-9_.]", "_", name)


export_mesh_names = {o.name for o in bpy.data.objects
                     if o.type == "MESH" and (o.name.startswith("Soldier_")
                                              or o.name == "Infantry_Rifle")}
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

print("exporting...")
with bpy.context.temp_override(scene=scene, view_layer=view_layer,
                               active_object=arm,
                               selected_objects=[o for o in bpy.data.objects if o.select_get()],
                               object=arm):
    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-X",
        axis_up="Z",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=1.0,
        use_mesh_modifiers=True,
    )
print("exported:", out_path, os.path.getsize(out_path), "bytes")
