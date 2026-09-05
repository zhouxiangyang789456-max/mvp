# -*- coding: utf-8 -*-
"""Launch Blender with MCP_ENABLE_SCRIPT_EXECUTE=true so the addon's
blender.execute_script tool is enabled. Detached from this console."""
import os
import subprocess

env = dict(os.environ)
env["MCP_ENABLE_SCRIPT_EXECUTE"] = "true"

# Kill any running Blender first (optional safety)
subprocess.run(["taskkill", "/IM", "blender.exe", "/F"], capture_output=True)

proc = subprocess.Popen(
    [r"D:\blender.exe"],
    cwd="D:/",
    env=env,
    creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP,
)
print("Blender launched PID", proc.pid, "with MCP_ENABLE_SCRIPT_EXECUTE=true")
