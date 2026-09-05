# -*- coding: utf-8 -*-
"""Shared animation-authoring helpers for the soldier rig.
Coordinate conventions: front = -X, up = +Z, character-left = +Y, right = -Y.
Pose rotations are LOCAL Euler (XYZ) relative to the rest pose.
"""
import bpy
from mathutils import Vector

arm = bpy.data.objects["Armature"]
pb = arm.pose.bones


def clear_animation():
    arm.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)


def new_action(name, frame_end):
    if name in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[name])
    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act
    act.frame_range = (1, frame_end)
    return act


def set_pose(pose, loc_pose=None):
    """pose: {bone: (rx, ry, rz)}. loc_pose: {bone: (x, y, z)} for location."""
    for b in pb:
        bn = b.name
        if bn in pose:
            b.rotation_mode = 'XYZ'
            b.rotation_euler = pose[bn]
        else:
            b.rotation_mode = 'QUATERNION'
            b.rotation_quaternion = (1, 0, 0, 0)
        if loc_pose and bn in loc_pose:
            b.location = loc_pose[bn]
        else:
            b.location = (0.0, 0.0, 0.0)


def keyframe_pose(frame, bones, loc_bones=()):
    for bn in bones:
        if bn in pb:
            pb[bn].keyframe_insert(data_path="rotation_euler", frame=frame)
    for bn in loc_bones:
        if bn in pb:
            pb[bn].keyframe_insert(data_path="location", frame=frame)


def linearize(act):
    """Blender 5.2 layered-action API: fcurves live in slot channelbags."""
    slots = list(act.slots)
    for ly in act.layers:
        for s in ly.strips:
            for slot in slots:
                try:
                    cb = s.channelbag(slot)
                except Exception:
                    continue
                for fc in cb.fcurves:
                    for kp in fc.keyframe_points:
                        kp.interpolation = 'LINEAR'
    act.update_tag()


def build_action(name, keys, bones, loc_bones=()):
    """keys: list of (frame, pose, loc_pose). bones: all rotation bones to key."""
    act = new_action(name, keys[-1][0])
    for frame, pose, locp in keys:
        set_pose(pose, locp)
        keyframe_pose(frame, bones, loc_bones)
    linearize(act)
    return act


def pose_world(bone_name):
    b = pb[bone_name]
    m = arm.matrix_world @ b.matrix
    return m.translation


def report_pose():
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    for bn in ["Root", "Hip", "L_Thigh", "R_Thigh", "L_Foot", "R_Foot",
               "L_Hand", "R_Hand", "Head", "L_Knee", "R_Knee"]:
        if bn in pb:
            p = pose_world(bn)
            print(f"  {bn}: ({round(p.x,3)}, {round(p.y,3)}, {round(p.z,3)})")
