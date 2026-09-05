# -*- coding: utf-8 -*-
"""Build Attack (rifle burst loop) on the soldier armature."""
import bpy
import blender_anim_lib as L

# bones we key for attack
ATTACK_BONES = ["Spine01", "Spine02", "Head", "L_Clavicle", "R_Clavicle",
                "L_Upperarm", "R_Upperarm", "L_Forearm", "R_Forearm"]

aim = {
    "Spine01": (0.04, 0, 0),
    "Spine02": (0.02, 0, 0),
    "Head": (0, 0, -0.12),
    "L_Upperarm": (0.05, 0, 0.02),
    "R_Upperarm": (0.05, 0, 0.02),
    "L_Forearm": (0.05, 0, 0),
    "R_Forearm": (0.05, 0, 0),
}
recoil = {
    "Spine01": (-0.10, 0, 0),
    "Spine02": (-0.06, 0, 0),
    "Head": (0, 0, 0.08),
    "L_Clavicle": (0.15, 0, 0),
    "R_Clavicle": (0.15, 0, 0),
    "L_Upperarm": (0.15, 0, 0.02),
    "R_Upperarm": (0.15, 0, 0.02),
    "L_Forearm": (0.12, 0, 0),
    "R_Forearm": (0.12, 0, 0),
}

# clear only the Attack action (keep Idle/Move)
if "Attack" in bpy.data.actions:
    bpy.data.actions.remove(bpy.data.actions["Attack"])

keys = [
    (1,  aim),
    (3,  recoil),
    (5,  aim),
    (8,  recoil),
    (10, aim),
    (30, aim),
]
L.build_action("Attack", [(f, p, {}) for f, p in keys], ATTACK_BONES)
print("built Attack")

# ---- verify recoil: sample rifle muzzle vertex world x at aim vs recoil ----
arm = bpy.data.objects["Armature"]
rif = bpy.data.objects["Infantry_Rifle"]
# muzzle = vertex with min world x in rifle mesh
verts = list(rif.data.vertices)
muzzle_v = min(verts, key=lambda v: v.co.x)
print("muzzle local x:", round(muzzle_v.co.x, 3))

def rifle_muzzle_x():
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    # need evaluated copy to get deformed (skinned) position
    deps = L.dg
    eval_rif = rif.evaluated_get(deps)
    m = eval_rif.matrix_world
    # find min-x corner of evaluated mesh
    best = None
    for v in eval_rif.data.vertices:
        w = m @ v.co
        if best is None or w.x < best.x:
            best = w
    return best.x

# set pose to aim, then evaluate at frames
arm.animation_data.action = bpy.data.actions["Attack"]
L.set_pose(aim, {})
mins_x = 9
for f in [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25, 30]:
    bpy.context.scene.frame_set(f)
    L.dg = bpy.context.evaluated_depsgraph_get()
    L.dg.update()
    mx = rifle_muzzle_x()
    mins_x = min(mins_x, mx)
    print(f"frame {f}: rifle muzzle world x={mx:.3f}")
print("min muzzle x (forward extent):", round(mins_x, 3))

arm.animation_data.action = None
bpy.context.scene.frame_set(1)
L.set_pose({}, {})
print("done")
