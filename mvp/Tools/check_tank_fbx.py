# -*- coding: utf-8 -*-
"""Byte-level sanity check for the tank FBX."""
import os

p = r"D:/prounity/mvp/mvp/Tools/_export/Tank.fbx"
data = open(p, "rb").read()

print("header:", data[:27])
rel_backslash = data.count(b"Textures\\")
rel_forward = data.count(b"Textures/")
abs_export = data.count(b"_export")
print("relative 'Textures\\\\' refs:", rel_backslash)
print("relative 'Textures/' refs:", rel_forward)
print("absolute '_export' refs:", abs_export)
print("Tank_part occurrences:", data.count(b"Tank_part"))
print("ParentNode occurrences:", data.count(b"ParentNode"))
print("Armature occurrences:", data.count(b"Armature"))
print("size:", os.path.getsize(p))
