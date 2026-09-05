# -*- coding: utf-8 -*-
"""Determine the soldier's facing direction using rifle muzzle + head nose geometry."""
import bpy
from mathutils import Vector

# --- RIFLE: which X-end is the thin barrel (muzzle) vs tall stock ---
rifle = bpy.data.objects["Infantry_Rifle"]
verts = [Vector(v.co) for v in rifle.data.vertices]
xs = [v.x for v in verts]
minx, maxx = min(xs), max(xs)
L = maxx - minx
bin_l = minx + 0.12 * L
bin_r = maxx - 0.12 * L
def z_extent(pred):
    zs = [v.z for v in verts if pred(v)]
    if not zs:
        return None
    return max(zs) - min(zs)
zl = z_extent(lambda v: v.x < bin_l)
zr = z_extent(lambda v: v.x > bin_r)
print(f"rifle x-range {minx:.3f}..{maxx:.3f}")
print(f"min-x end z-extent: {zl}, max-x end z-extent: {zr}")
# also y-extent at each end
def y_extent(pred):
    ys = [v.y for v in verts if pred(v)]
    return (max(ys) - min(ys)) if ys else None
yl = y_extent(lambda v: v.x < bin_l)
yr = y_extent(lambda v: v.x > bin_r)
print(f"min-x end y-extent: {yl}, max-x end y-extent: {yr}")
# muzzle = the end with smaller z-extent (thin barrel)
if zl is not None and zr is not None:
    if zl < zr:
        print("MUZZLE at min-x (barrel points -X)")
    else:
        print("MUZZLE at max-x (barrel points +X)")

# --- HEAD: find nose-tip (most protrusive vertex in mid z-band) ---
head = bpy.data.objects["Soldier_Head"]
hverts = [Vector(v.co) for v in head.data.vertices]
hz = [v.z for v in hverts]
hminz, hmaxz = min(hz), max(hz)
mid_lo = hminz + 0.25 * (hmaxz - hminz)
mid_hi = hminz + 0.75 * (hmaxz - hminz)
# center of head in horizontal plane (all vertices)
cx = sum(v.x for v in hverts) / len(hverts)
cy = sum(v.y for v in hverts) / len(hverts)
print(f"\nhead center xz: ({cx:.3f}, {cy:.3f})")
# find most protrusive vertex in mid band (max horizontal distance from center)
best = None
best_d = 0
for v in hverts:
    if mid_lo <= v.z <= mid_hi:
        d = (v.x - cx) ** 2 + (v.y - cy) ** 2
        if d > best_d:
            best_d = d
            best = v
if best:
    print(f"nose-tip candidate at ({best.x:.3f}, {best.y:.3f}, {best.z:.3f})")
    print(f"  -> protrudes toward X={'+' if best.x >= cx else '-'}x, Y={'+' if best.y >= cy else '-'}y")
    print(f"  direction vector from center: dx={best.x-cx:.3f}, dy={best.y-cy:.3f}")
