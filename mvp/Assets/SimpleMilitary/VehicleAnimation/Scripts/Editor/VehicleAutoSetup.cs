#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace SimpleMilitary.VehicleAnimation.Editor
{
    /// <summary>
    /// 载具动画一键配置工具。
    ///
    /// 用法：在 Hierarchy 中选中载具根节点 → 菜单栏
    /// GameObject → Simple Military → 一键配置载具动画。
    ///
    /// 工具会自动扫描子节点名称，判断载具类型，挂上对应组件并填好引用，
    /// 省去手动拖拽多个轮子 / 炮塔 / 旋翼节点的操作。支持撤销（Ctrl+Z）。
    /// </summary>
    public static class VehicleAutoSetup
    {
        private const string MenuPath = "GameObject/Simple Military/一键配置载具动画";

        [MenuItem(MenuPath, false, 20)]
        private static void SetupFromMenu(MenuCommand command)
        {
            var go = command.context as GameObject;
            if (go == null)
            {
                UnityEngine.Debug.LogWarning("[载具动画配置] 请先在 Hierarchy 中选中载具根节点。");
                return;
            }
            Setup(go);
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate(MenuCommand command)
        {
            return command.context is GameObject;
        }

        /// <summary>
        /// 对指定对象执行自动配置。也可从其他编辑器脚本调用。
        /// </summary>
        public static void Setup(GameObject root)
        {
            var report = new StringBuilder();
            report.AppendLine($"载具「{root.name}」配置结果：");

            // 1. 运动组件（所有载具都需要）
            var motion = root.GetComponent<VehicleMotion>();
            if (motion == null)
            {
                motion = Undo.AddComponent<VehicleMotion>(root);
                report.AppendLine("  + VehicleMotion（速度源）");
            }
            else
            {
                report.AppendLine("  = VehicleMotion 已存在，跳过");
            }

            // 2. 收集并分类子节点
            var wheels = new List<Transform>();
            var turrets = new List<Transform>();
            var barrels = new List<Transform>();
            var racks = new List<Transform>();
            var mainRotors = new List<Transform>();
            var tailRotors = new List<Transform>();

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                string n = child.name.ToLowerInvariant();

                if (n.Contains("wheel"))
                    wheels.Add(child);
                else if (n.Contains("turret") || n.Contains("radar_turret"))
                    turrets.Add(child);
                else if (n.Contains("barrel") || n.Contains("cannon") || n.Contains("gun") && !n.Contains("mggun"))
                    barrels.Add(child);
                else if (n.Contains("rack") || n.Contains("misslerack") || n.Contains("missilerack"))
                    racks.Add(child);
                else if (n.Contains("rotor_main") || n.Contains("rotor") && !n.Contains("tail"))
                    mainRotors.Add(child);
                else if (n.Contains("rotor_tail"))
                    tailRotors.Add(child);
                else if (n.Contains("prop"))
                    mainRotors.Add(child);
            }

            // 3. 车轮
            if (wheels.Count > 0)
            {
                var comp = root.GetComponent<VehicleWheels>();
                bool isNew = comp == null;
                if (isNew)
                    comp = Undo.AddComponent<VehicleWheels>(root);
                else
                    Undo.RecordObject(comp, "更新车轮配置");

                comp.autoFindWheels = false;
                comp.wheels = new List<Transform>(wheels);
                comp.motion = motion;
                comp.AutoPickSteeringWheels();
                comp.enableSteering = comp.steeringWheels.Count > 0;
                EditorUtility.SetDirty(comp);
                report.AppendLine($"  {(isNew ? "+" : "=")} VehicleWheels：{wheels.Count} 个车轮，前轮 {comp.steeringWheels.Count} 个");
            }

            // 4. 炮塔 + 炮管
            if (turrets.Count > 0 || barrels.Count > 0)
            {
                var comp = root.GetComponent<VehicleTurretAim>();
                bool isNew = comp == null;
                if (isNew)
                    comp = Undo.AddComponent<VehicleTurretAim>(root);
                else
                    Undo.RecordObject(comp, "更新炮塔配置");

                if (turrets.Count > 0) comp.turret = turrets[0];
                if (barrels.Count > 0) comp.barrel = barrels[0];
                EditorUtility.SetDirty(comp);
                report.AppendLine($"  {(isNew ? "+" : "=")} VehicleTurretAim：炮塔「{comp.turret?.name ?? "无"}」，炮管「{comp.barrel?.name ?? "无"}」");
            }

            // 5. 导弹发射架
            if (racks.Count > 0)
            {
                var comp = root.GetComponent<MissileRackLauncher>();
                bool isNew = comp == null;
                if (isNew)
                    comp = Undo.AddComponent<MissileRackLauncher>(root);
                else
                    Undo.RecordObject(comp, "更新发射架配置");

                comp.rack = racks[0];
                comp.CollectMissiles();
                EditorUtility.SetDirty(comp);
                report.AppendLine($"  {(isNew ? "+" : "=")} MissileRackLauncher：发射架「{racks[0].name}」，导弹 {comp.MissilesLeft} 枚");
            }

            // 6. 旋翼
            if (mainRotors.Count > 0 || tailRotors.Count > 0)
            {
                var comp = root.GetComponent<HeliRotor>();
                bool isNew = comp == null;
                if (isNew)
                    comp = Undo.AddComponent<HeliRotor>(root);
                else
                    Undo.RecordObject(comp, "更新旋翼配置");

                if (mainRotors.Count > 0) comp.mainRotor = mainRotors[0];
                if (tailRotors.Count > 0) comp.tailRotor = tailRotors[0];
                EditorUtility.SetDirty(comp);
                report.AppendLine($"  {(isNew ? "+" : "=")} HeliRotor：主旋翼「{comp.mainRotor?.name ?? "无"}」，尾桨「{comp.tailRotor?.name ?? "无"}」");
            }

            // 7. 履带提示（无法自动配置，需人工处理）
            bool looksLikeTank = root.name.ToLowerInvariant().Contains("tank");
            if (looksLikeTank)
            {
                report.AppendLine("  ! 检测到坦克：底盘与履带共用材质，无法自动配置滚动。");
                report.AppendLine("    请先在建模软件中分离履带，再挂 TankTrackScroll（详见文档第 4.2 节）。");
            }

            // 8. 碰撞体提示
            var col = root.GetComponentInChildren<Collider>();
            if (col == null)
            {
                report.AppendLine("  ! 未检测到任何 Collider：本资源包不含碰撞体，请自行添加后再用于游戏。");
            }

            // 标记为已修改，确保 Prefab 改动能保存
            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);

            UnityEngine.Debug.Log(report.ToString());
        }
    }
}
#endif
