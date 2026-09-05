# -*- coding: utf-8 -*-
"""Robustly clear the scene, import 坦克.glb, report structure."""
import bpy

# --- robust clear: remove every object from bpy.data entirely ---
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
# purge now-orphaned data blocks
for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images,
              bpy.data.armatures, bpy.data.actions, bpy.data.cameras,
              bpy.data.lights, bpy.data.collections):
    for d in list(block):
        if d.users == 0:
            try:
                block.remove(d)
            except Exception as e:
                pass
print("after clear objects:", len(bpy.data.objects))

src = r"D:/prounity/mvp/单位/坦克.glb"
bpy.ops.import_scene.gltf(filepath=src)
print("imported:", src)

print("--- objects ---")
for o in bpy.data.objects:
    print("  %-30s type=%s" % (o.name, o.type))

print("--- armatures ---")
for a in bpy.data.armatures:
    print("  armature:", a.name, "bones:", len(a.bones))
    root = [b.name for b in a.bones if b.parent is None]
    print("    root bones:", root)

print("--- meshes ---")
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    bbox = [tuple(round(v, 3) for v in o.bound_box[i]) for i in (0, 6)]
    print("  mesh: %-25s verts=%-7d mats=%d  bbox_min=%s bbox_max=%s" % (
        o.name, len(o.data.vertices), len(o.material_slots),
        bbox[0], bbox[1]))

print("--- materials & textures ---")
seen_img = set()
for m in bpy.data.materials:
    imgs = []
    if m.node_tree is not None:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image is not None:
                img = n.image
                imgs.append((img.name, img.packed_file is not None,
                             img.filepath, img.size))
                seen_img.add(img.name)
    print("  mat: %-25s images=%s" % (m.name, imgs))

print("--- scene fps ---")
print("  render fps:", bpy.context.scene.render.fps,
      "frame_end:", bpy.context.scene.frame_end)
print("--- actions ---")
for a in bpy.data.actions:
    print("  action:", a.name, "frames:", a.frame_range)
print("total unique images:", len(seen_img))
