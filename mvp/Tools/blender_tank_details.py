# -*- coding: utf-8 -*-
"""Tank details: world AABB, orientation, ParentNode transform, materials/textures."""
import bpy
import math

# --- world AABB across all mesh objects ---
mesh_objs = [o for o in bpy.data.objects if o.type == "MESH"]
mins = [1e9, 1e9, 1e9]
maxs = [-1e9, -1e9, -1e9]
for o in mesh_objs:
    m = o.matrix_world
    for corner in o.bound_box:
        w = m @ __import__("mathutils").Vector(corner)
        for i in range(3):
            mins[i] = min(mins[i], w[i])
            maxs[i] = max(maxs[i], w[i])
print("world AABB min:", tuple(round(v, 3) for v in mins))
print("world AABB max:", tuple(round(v, 3) for v in maxs))
size = [maxs[i] - mins[i] for i in range(3)]
print("world size (x,y,z):", tuple(round(v, 3) for v in size))
print("lowest y (ground offset needed):", round(mins[1], 3))

# --- ParentNode transform ---
for o in bpy.data.objects:
    if o.type == "EMPTY":
        e = o
        print("EMPTY:", e.name, "loc=", tuple(round(v,3) for v in e.location),
              "rot_euler_deg=", tuple(round(math.degrees(a),1) for a in e.rotation_euler))
        for c in e.children:
            print("   child:", c.name)

# --- per-material image slots (packed? path? size?) ---
print("--- materials ---")
seen = set()
for m in bpy.data.materials:
    imgs = []
    if m.node_tree is not None:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image is not None:
                img = n.image
                imgs.append((n.label or n.name, img.name,
                             img.packed_file is not None,
                             img.filepath, img.size,
                             img.file_format))
                seen.add(img.name)
    print("mat '%s':" % m.name)
    for im in imgs:
        print("    %-16s img=%-30s packed=%s path='%s' size=%s fmt=%s" % im)
print("total unique images:", len(seen))

# --- which part is likely the barrel/turret: the parts farthest along x and z ---
# print each mesh world center
print("--- mesh world centers (x,y,z) ---")
for o in mesh_objs:
    c = o.matrix_world @ __import__("mathutils").Vector((0, 0, 0))
    print("  %-14s center=(%s,%s,%s)" % (o.name,
          round(c[0], 3), round(c[1], 3), round(c[2], 3)))
