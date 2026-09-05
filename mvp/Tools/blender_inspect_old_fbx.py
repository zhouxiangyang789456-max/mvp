# -*- coding: utf-8 -*-
"""Import the OLD Infantry.fbx, measure scale/orientation, then clean up."""
import bpy
import mathutils

path = r"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Infantry.fbx"
before = set(bpy.data.objects)
scene = bpy.context.scene
view_layer = scene.view_layers[0]
active = view_layer.objects.active
with bpy.context.temp_override(scene=scene, view_layer=view_layer,
                               active_object=active):
    bpy.ops.import_scene.fbx(filepath=path)
new = [o for o in bpy.data.objects if o.name not in before]
print("imported objects:")
for o in new:
    print(f"  {o.name} ({o.type}) parent={o.parent.name if o.parent else None}")

arm = None
for o in new:
    if o.type == "ARMATURE":
        arm = o
        break
if arm:
    print("armature:", arm.name)
    print("root rotation:", tuple(round(v, 3) for v in arm.rotation_euler))
    lo = mathutils.Vector((1e9, 1e9, 1e9))
    hi = mathutils.Vector((-1e9, -1e9, -1e9))
    for o in new:
        if o.type == "MESH":
            m = o.matrix_world
            for v in o.data.vertices:
                w = m @ v.co
                for i in range(3):
                    lo[i] = min(lo[i], w[i])
                    hi[i] = max(hi[i], w[i])
    print(f"meshes bbox: min({lo.x:.3f},{lo.y:.3f},{lo.z:.3f}) "
          f"max({hi.x:.3f},{hi.y:.3f},{hi.z:.3f})")
    print(f"height: {hi.z-lo.z:.3f}  depth(x): {hi.x-lo.x:.3f}  width(y): {hi.y-lo.y:.3f}")
    if "Head" in arm.pose.bones:
        root = arm.pose.bones.get("Root") or arm.pose.bones[0]
        he = arm.matrix_world @ arm.pose.bones["Head"].matrix
        rt = arm.matrix_world @ root.matrix
        print(f"root({rt.x:.3f},{rt.y:.3f},{rt.z:.3f}) "
              f"head({he.x:.3f},{he.y:.3f},{he.z:.3f}) "
              f"offset({he.x-rt.x:.3f},{he.y-rt.y:.3f},{he.z-rt.z:.3f})")
    print("armature children:", [c.name for c in arm.children])

for o in list(new):
    bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes):
    if m.users == 0:
        bpy.data.meshes.remove(m)
print("done")
