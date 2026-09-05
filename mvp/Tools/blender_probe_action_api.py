# -*- coding: utf-8 -*-
"""Confirm working path to set interpolation on action fcurves in Blender 5.2."""
import bpy
import sys
import io

log_path = r"D:/prounity/mvp/mvp/Tools/probe_action_out.txt"
buf = io.StringIO()
_real = sys.stdout
sys.stdout = buf

def log(*a):
    buf.write(" ".join(str(x) for x in a) + "\n")

try:
    log("=== ActionChannelbag.bl_rna.properties ===")
    for p in bpy.types.ActionChannelbag.bl_rna.properties:
        log(f"  {p.identifier} type={p.type}")
    log()

    arm = bpy.data.objects.get("Armature")
    act = bpy.data.actions.new("Probe_Act6")
    arm.animation_data_create()
    arm.animation_data.action = act
    pb = arm.pose.bones["Head"]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (0.1, 0, 0)
    pb.keyframe_insert(data_path="rotation_euler", frame=1)
    pb.rotation_euler = (0.2, 0, 0)
    pb.keyframe_insert(data_path="rotation_euler", frame=10)

    slots = list(act.slots)
    log("act.slots count:", len(slots))
    for ly in act.layers:
        for s in ly.strips:
            cb = s.channelbag(slots[0])
            log("channelbag() type:", type(cb))
            for attr in ["name", "fcurves", "slot"]:
                try:
                    val = getattr(cb, attr)
                    if hasattr(val, "__len__"):
                        log(f"  cb.{attr} len={len(val)}")
                    else:
                        log(f"  cb.{attr} = {val!r}")
                except Exception as e:
                    log(f"  cb.{attr} ERR {e!r}")
            try:
                fcs = cb.fcurves
                log("  fcurves count:", len(fcs))
                for fc in fcs:
                    log(f"    fc data_path={fc.data_path} array_index={fc.array_index} nkeys={len(fc.keyframe_points)}")
                    for kp in fc.keyframe_points:
                        log(f"      kp frame={kp.co[0]} value={kp.co[1]} interp={kp.interpolation}")
                    # SET LINEAR
                    for kp in fc.keyframe_points:
                        kp.interpolation = "LINEAR"
                    log("    -> set LINEAR ok")
            except Exception as e:
                log("  cb.fcurves ERR:", repr(e))
    # verify after set
    slots = list(act.slots)
    log("act.slots count:", len(slots))
    for ly in act.layers:
        for s in ly.strips:
            cb = s.channelbag(slots[0])
            for fc in cb.fcurves:
                log("VERIFY fc", fc.data_path, fc.keyframe_points[0].interpolation)

    arm.animation_data.action = None
    bpy.data.actions.remove(act)
    log("=== done ===")
except Exception as e:
    import traceback
    log("EXCEPTION:", repr(e))
    log(traceback.format_exc())

sys.stdout = _real
with open(log_path, "w", encoding="utf-8") as f:
    f.write(buf.getvalue())
print("wrote", log_path)
