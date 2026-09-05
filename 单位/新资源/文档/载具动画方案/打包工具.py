#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity 资源包打包工具
把 Scripts/ 与文档打包成可直接导入 Unity 的 .unitypackage

用法：
    python 打包工具.py

输出：
    SimpleMilitary-VehicleAnimation-v1.0.unitypackage

说明：
    .unitypackage 本质是 tar.gz，每个资产一个以 GUID 命名的目录，
    内含 pathname（包内路径）、asset（文件内容）、asset.meta（Unity 元数据）。
    文件夹条目只有 pathname 与 asset.meta，没有 asset。
"""

import io
import os
import tarfile
import time
import uuid

# ---------------- 配置区 ----------------

PACKAGE_NAME = "SimpleMilitary-VehicleAnimation-v1.0.unitypackage"

# Unity 包内的根路径
ASSET_ROOT = "Assets/SimpleMilitary/VehicleAnimation"

# 需要打包的文件：(本地相对路径, 包内相对路径)
FILES = [
    ("Scripts/VehicleMotion.cs",        "Scripts/VehicleMotion.cs"),
    ("Scripts/VehicleWheels.cs",        "Scripts/VehicleWheels.cs"),
    ("Scripts/TankTrackScroll.cs",      "Scripts/TankTrackScroll.cs"),
    ("Scripts/VehicleTurretAim.cs",     "Scripts/VehicleTurretAim.cs"),
    ("Scripts/MissileRackLauncher.cs",  "Scripts/MissileRackLauncher.cs"),
    ("Scripts/HeliRotor.cs",            "Scripts/HeliRotor.cs"),
    ("Scripts/Editor/VehicleAutoSetup.cs", "Scripts/Editor/VehicleAutoSetup.cs"),
    ("Simple Military 载具动画添加方法.md",  "Docs/使用文档.md"),
]

# 需要建立的文件夹（含根路径，自动去重并排序）
FOLDERS = [
    "",
    "Scripts",
    "Scripts/Editor",
    "Docs",
]

# ---------------- 元数据模板 ----------------

META_FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

META_MONO = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

META_TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def make_guid():
    """生成 Unity 风格的 32 位小写十六进制 GUID"""
    return uuid.uuid4().hex


def pick_meta_template(ext):
    ext = ext.lower()
    if ext == ".cs":
        return META_MONO
    if ext in (".md", ".txt", ".json", ".xml"):
        return META_TEXT
    return META_FOLDER


def add_entry(tar, guid, unity_path, asset_bytes, meta_text):
    """向 tar 中写入一个资产条目"""
    # 1. GUID 目录条目
    dir_info = tarfile.TarInfo(guid)
    dir_info.type = tarfile.DIRTYPE
    dir_info.mode = 0o755
    dir_info.mtime = int(time.time())
    tar.addfile(dir_info)

    # 2. pathname —— 纯路径，不带换行与 Unity 的 "00" 尾巴
    pn_bytes = unity_path.encode("utf-8")
    pn_info = tarfile.TarInfo(guid + "/pathname")
    pn_info.size = len(pn_bytes)
    pn_info.mode = 0o644
    pn_info.mtime = int(time.time())
    tar.addfile(pn_info, io.BytesIO(pn_bytes))

    # 3. asset.meta
    meta_bytes = meta_text.encode("utf-8")
    meta_info = tarfile.TarInfo(guid + "/asset.meta")
    meta_info.size = len(meta_bytes)
    meta_info.mode = 0o644
    meta_info.mtime = int(time.time())
    tar.addfile(meta_info, io.BytesIO(meta_bytes))

    # 4. asset（文件夹没有实体文件）
    if asset_bytes is not None:
        asset_info = tarfile.TarInfo(guid + "/asset")
        asset_info.size = len(asset_bytes)
        asset_info.mode = 0o644
        asset_info.mtime = int(time.time())
        tar.addfile(asset_info, io.BytesIO(asset_bytes))


def main():
    base_dir = os.path.dirname(os.path.abspath(__file__))
    out_path = os.path.join(base_dir, PACKAGE_NAME)

    # 先检查源文件是否齐全
    missing = []
    for local_rel, _ in FILES:
        if not os.path.exists(os.path.join(base_dir, local_rel)):
            missing.append(local_rel)
    if missing:
        print("打包中止，以下文件缺失：")
        for m in missing:
            print("  - " + m)
        return

    with tarfile.open(out_path, "w:gz") as tar:
        # 1. 文件夹条目（从短到长，保证父目录先写入）
        for folder in sorted(set(FOLDERS), key=lambda s: (s.count("/"), len(s))):
            unity_path = ASSET_ROOT if not folder else ASSET_ROOT + "/" + folder
            guid = make_guid()
            meta = META_FOLDER.format(guid=guid)
            add_entry(tar, guid, unity_path, None, meta)
            print("  目录  " + unity_path)

        # 2. 文件条目
        for local_rel, pack_rel in FILES:
            local_path = os.path.join(base_dir, local_rel)
            unity_path = ASSET_ROOT + "/" + pack_rel

            with open(local_path, "rb") as f:
                data = f.read()

            ext = os.path.splitext(local_rel)[1]
            guid = make_guid()
            meta = pick_meta_template(ext).format(guid=guid)

            add_entry(tar, guid, unity_path, data, meta)
            print("  文件  " + unity_path)

    size_kb = os.path.getsize(out_path) / 1024
    print()
    print("打包完成：" + PACKAGE_NAME)
    print("大小：%.1f KB" % size_kb)
    print("包内路径：" + ASSET_ROOT)


if __name__ == "__main__":
    main()
