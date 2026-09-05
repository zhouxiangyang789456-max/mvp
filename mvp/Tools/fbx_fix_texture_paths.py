# -*- coding: utf-8 -*-
"""Rewrite absolute texture paths in the exported FBX to relative ones
that Unity can resolve (Textures/<name>.png next to the FBX)."""
import os
import re

src = r"D:/prounity/mvp/mvp/Tools/_export/Infantry.fbx"
prefixes = [
    b"D:\\prounity\\mvp\\mvp\\Assets\\Art\\Battle\\Units\\Infantry\\Textures\\",
    b"D:/prounity/mvp/mvp/Assets/Art/Battle/Units/Infantry/Textures/",
    b"\\Textures\\",
    b"/Textures/",
]

data = open(src, "rb").read()
count = 0
# First, normalize any backslash-full prefix to a forward-slash relative path.
for p in prefixes:
    n = data.count(p)
    if n:
        data = data.replace(p, b"Textures/")
        count += n
print("path replacements:", count)

# Any remaining absolute D:\ or D:/ occurrences referencing Textures?
remain = [m for m in re.finditer(rb"[A-Za-z]:\\\\[^\\\"]*Infantry\\\\Textures\\\\[A-Za-z0-9_.]+\.png", data)]
print("remaining absolute texture refs:", len(remain))
for m in remain:
    print("  ", m.group(0).decode("latin1"))

out = src
open(out, "wb").write(data)
print("rewritten:", out, os.path.getsize(out), "bytes")
