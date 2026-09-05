# -*- coding: utf-8 -*-
"""Check rifle vertex groups + all bone names."""
import bpy
import blender_anim_lib as L

arm = bpy.data.objects["Armature"]
print("=== ALL BONE NAMES ===")
for b in arm.data.bones:
    print(" ", b.name)

rif = bpy.data.objects["Infantry_Rifle"]
print("\n=== RIFLE VERTEX GROUPS ===")
for vg in rif.vertex_groups:
    idx = vg.index
    total = sum(1 for v in rif.data.vertices if vg.index in [g.group for g in v.groups])
    print(f"  {vg.name}: {total} verts")

# distribution of weights
print("\n=== RIFLE bone weight distribution (top groups by weight) ===")
import collections
cnt = collections.Counter()
for v in rif.data.vertices:
    for g in v.groups:
        cnt[rif.vertex_groups[g.group].name] += 1
for name, c in cnt.most_common(20):
    print(f"  {name}: {c}")
