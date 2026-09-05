#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
验证生成的 .unitypackage 结构是否正确
用法：python 验证包.py
"""

import os
import re
import shutil
import tarfile
import tempfile

PACKAGE = "SimpleMilitary-VehicleAnimation-v1.0.unitypackage"
HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    pkg = os.path.join(HERE, PACKAGE)
    if not os.path.exists(pkg):
        print("找不到包：" + PACKAGE)
        return

    tmp = tempfile.mkdtemp(prefix="upkg_verify_")
    try:
        with tarfile.open(pkg, "r:gz") as t:
            t.extractall(tmp)

        entries = {}
        for name in sorted(os.listdir(tmp)):
            d = os.path.join(tmp, name)
            if not os.path.isdir(d) or len(name) != 32:
                continue

            info = {"guid": name}

            pn = os.path.join(d, "pathname")
            if os.path.exists(pn):
                info["path"] = open(pn, "rb").read().decode("utf-8")

            meta = os.path.join(d, "asset.meta")
            if os.path.exists(meta):
                text = open(meta, "r", encoding="utf-8", errors="replace").read()
                m = re.search(r"guid:\s*([0-9a-f]{32})", text)
                info["meta_guid"] = m.group(1) if m else None
                info["is_folder"] = "folderAsset: yes" in text
                if "MonoImporter" in text:
                    info["importer"] = "MonoImporter"
                elif "TextScriptImporter" in text:
                    info["importer"] = "TextScriptImporter"
                elif "DefaultImporter" in text:
                    info["importer"] = "DefaultImporter"
                else:
                    info["importer"] = "未知"

            info["has_asset"] = os.path.exists(os.path.join(d, "asset"))
            entries[name] = info

        ok = True
        print("条目总数：%d\n" % len(entries))
        print("%-42s %-9s %-7s %-16s %s" % ("包内路径", "GUID", "实体", "Importer", "类型"))
        print("-" * 100)

        short = "Assets/SimpleMilitary/VehicleAnimation"
        for info in sorted(entries.values(), key=lambda x: x.get("path", "")):
            path = info.get("path", "?").replace(short, "~")
            guid_ok = info.get("meta_guid") == info["guid"]
            if not guid_ok:
                ok = False
            is_folder = info.get("is_folder", False)
            kind = "文件夹" if is_folder else "文件"

            # 文件必须有实体，文件夹必须没有实体
            if is_folder == info.get("has_asset", False):
                ok = False
                asset_mark = "X 异常"
            else:
                asset_mark = "无(正确)" if is_folder else "有"

            # pathname 不应有尾部脏字符
            if info.get("path", "").endswith(("00", "\n")):
                ok = False
                path += "  [!尾部脏字符]"

            print("%-42s %-9s %-7s %-16s %s"
                  % (path, "一致" if guid_ok else "X 不一致",
                     asset_mark, info.get("importer", "?"), kind))

        print()
        print("校验结果：" + ("全部通过，包结构正确" if ok else "存在问题，请检查上方标记"))

    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
