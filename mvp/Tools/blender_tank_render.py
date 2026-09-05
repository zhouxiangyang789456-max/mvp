# -*- coding: utf-8 -*-
"""Render top/side views of the tank to PNGs (WORKBENCH flat material)."""
import bpy
import math
import os

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.light = "FLAT"
scene.render.image_settings.file_format = "PNG"
scene.render.resolution_x = 900
scene.render.resolution_y = 900

cam = bpy.data.objects.get("ViewCam")
if cam is None:
    cam = bpy.data.objects.new("ViewCam", bpy.data.cameras.new("ViewCam"))
    scene.collection.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.5
scene.camera = cam

out_dir = r"D:/prounity/mvp/mvp/Tools/_export"
os.makedirs(out_dir, exist_ok=True)

# (name, location, rotation_euler) — camera looks along its -Z at the origin.
views = {
    "top":    ((0, 6, 0), (math.pi / 2, 0, 0)),
    "frontX": ((6, 0, 0), (0, math.pi / 2, 0)),  # from +X looking -X
    "sideZ":  ((0, 0, 6), (0, 0, 0)),            # from +Z looking -Z
}

for name, (loc, rot) in views.items():
    cam.location = loc
    cam.rotation_euler = rot
    bpy.context.view_layer.update()
    path = os.path.join(out_dir, "tank_view_" + name + ".png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("rendered", path)

print("done")
