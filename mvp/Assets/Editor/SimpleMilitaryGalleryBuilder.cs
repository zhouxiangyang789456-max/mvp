using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mvp.Validation;
using SimpleMilitary.VehicleAnimation;
using SimpleMilitary.VehicleAnimation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimpleMilitaryGalleryBuilder
{
    private const string Root = "Assets/SimpleMilitaryValidation";
    private const string ScenePath = Root + "/SimpleMilitaryGallery.unity";
    private const string ReportPath = Root + "/SimpleMilitaryVerificationReport.md";

    private static readonly string[] CharacterNames =
    {
        "BombDisposal", "EasternSoldier", "FemaleMedic", "FemaleSoldier", "GasMaskSoldier",
        "General", "GermanSoldier", "JungleCommando", "Medic", "Mercenary", "Pilot", "Soldier01",
        "SpecialForces01", "SpecialForces02", "SpecialForces03", "SpecialForces04", "Terrorist01",
        "Terrorist02", "Terrorist03", "TrainingSoldier"
    };

    private static readonly string[] VehicleNames =
    {
        "SK_Veh_Apc_01", "SK_Veh_Apc_02", "SK_Veh_Armor_Car_01", "SK_Veh_Attack_Heli_01",
        "SK_Veh_Drone_01", "SK_Veh_FieldGun_01", "SK_Veh_Radar_Unit_01", "SK_Veh_ScudTruck_01",
        "SK_Veh_Small_Heli_01", "SK_Veh_Tank_01", "SK_Veh_tank_02", "SK_Veh_Troop_Car_01",
        "SK_Veh_Truck_Fuel_01", "SK_Veh_Truck_Medic_01", "SK_Veh_Truck_Troop_01",
        "SK_Veh_Truck_Troop_02"
    };

    [InitializeOnLoadMethod]
    private static void ScheduleFirstBuild()
    {
        string sceneFile = Path.Combine(Directory.GetCurrentDirectory(), ScenePath);
        string reportFile = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
        if (File.Exists(sceneFile) && File.Exists(reportFile))
            return;

        const string sessionKey = "SimpleMilitaryGalleryBuilder.Attempted";
        if (SessionState.GetBool(sessionKey, false))
            return;

        SessionState.SetBool(sessionKey, true);
        EditorApplication.delayCall += () =>
        {
            try
            {
                BuildAndVerify();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    public static void BuildAndVerify()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder(Root);
        BuildScene();
        WriteReport();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[SimpleMilitaryGallery] PASS: gallery and verification report created.");
    }

    private static void BuildScene()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        CreateEnvironment();

        GameObject characterRoot = new GameObject("Characters_20_Black");
        for (int i = 0; i < CharacterNames.Length; i++)
        {
            int column = i % 5;
            int row = i / 5;
            Vector3 position = new Vector3((column - 2) * 3.2f, 0f, row * 3.5f);
            string assetPath = $"Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_{CharacterNames[i]}_Black.prefab";
            GameObject unit = InstantiateRequired(assetPath, position, characterRoot.transform);
            unit.name = $"Character_{i + 1:00}_{CharacterNames[i]}";
            unit.AddComponent<CharacterAnimationPreview>();
            CreateLabel(unit.transform, CharacterNames[i] + "\n人物 " + (i + 1), 2.4f);
        }

        GameObject vehicleRoot = new GameObject("Vehicles_16_Optimized");
        for (int i = 0; i < VehicleNames.Length; i++)
        {
            int column = i % 4;
            int row = i / 4;
            Vector3 position = new Vector3((column - 1.5f) * 7f, 0f, 17f + row * 7f);
            string assetPath = $"Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/{VehicleNames[i]}.prefab";
            GameObject unit = InstantiateRequired(assetPath, position, vehicleRoot.transform);
            unit.name = $"Vehicle_{i + 1:00}_{VehicleNames[i]}";
            VehicleAutoSetup.Setup(unit);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = unit.name + "_AimTarget";
            target.transform.SetParent(vehicleRoot.transform);
            target.transform.position = position + new Vector3(0f, 1.5f, 4f);
            target.transform.localScale = Vector3.one * 0.22f;

            VehicleGalleryPreview preview = unit.AddComponent<VehicleGalleryPreview>();
            preview.aimTarget = target.transform;
            CreateLabel(unit.transform, VehicleNames[i] + "\n载具 " + (i + 1), 3.1f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Unable to save gallery: " + ScenePath);
        AssetDatabase.SaveAssets();
        EditorSceneManager.CloseScene(scene, true);
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);
    }

    private static void WriteReport()
    {
        var report = new StringBuilder();
        var failures = new List<string>();
        report.AppendLine("# Simple Military 单位验证报告");
        report.AppendLine();
        report.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();
        report.AppendLine("## 人物单位（Black 配色代表）");
        report.AppendLine();
        report.AppendLine("| 人物 | Animator | Avatar | 动画片段 | 渲染器 | 材质 | 结论 |");
        report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");

        string[] expectedClips = { "Idle", "Walk", "Run", "Character_Auto_SingleShot", "Death_01" };
        foreach (string name in CharacterNames)
        {
            string path = $"Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_{name}_Black.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add(name + ": prefab missing");
                report.AppendLine($"| {name} | 0 | 0 | 0 | 0 | 0 | 缺失 Prefab |");
                continue;
            }

            Animator animator = prefab.GetComponent<Animator>();
            int renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            int missingMaterials = CountMissingMaterials(prefab);
            string[] clips = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.Select(c => c.name).Distinct().ToArray()
                : Array.Empty<string>();
            string[] absent = expectedClips.Where(c => !clips.Contains(c)).ToArray();
            bool ok = animator != null && animator.avatar != null && animator.runtimeAnimatorController != null &&
                      renderers > 0 && missingMaterials == 0 && absent.Length == 0;
            if (!ok)
                failures.Add(name + ": character validation failed");
            report.AppendLine($"| {name} | {(animator != null ? 1 : 0)} | {(animator != null && animator.avatar != null ? 1 : 0)} | {clips.Length} | {renderers} | {(missingMaterials == 0 ? "完整" : "缺 " + missingMaterials)} | {(ok ? "通过" : "需检查")} |");
        }

        report.AppendLine();
        report.AppendLine("## 优化版载具");
        report.AppendLine();
        report.AppendLine("| 载具 | 网格渲染器 | 材质 | 自动配置 | 结论/限制 |");
        report.AppendLine("| --- | ---: | ---: | --- | --- |");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        foreach (string name in VehicleNames)
        {
            GameObject instance = FindSceneObjectContaining(scene, name);
            if (instance == null)
            {
                failures.Add(name + ": scene instance missing");
                report.AppendLine($"| {name} | 0 | 缺失 | 无 | 场景实例缺失 |");
                continue;
            }

            int renderers = instance.GetComponentsInChildren<Renderer>(true).Length;
            int missingMaterials = CountMissingMaterials(instance);
            string configured = DescribeVehicleComponents(instance);
            bool usesStaticTracks = name.IndexOf("Tank", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    name.Equals("SK_Veh_Apc_02", StringComparison.OrdinalIgnoreCase);
            string limitation = usesStaticTracks ? "通过；履带滚动待分离网格" : "通过";
            bool ok = renderers > 0 && missingMaterials == 0 && configured != "仅速度源";
            if (!ok)
            {
                failures.Add(name + ": vehicle validation failed");
                limitation = "需检查";
            }
            report.AppendLine($"| {name} | {renderers} | {(missingMaterials == 0 ? "完整" : "缺 " + missingMaterials)} | {configured} | {limitation} |");
        }

        int missingScripts = CountMissingScripts(scene);
        report.AppendLine();
        report.AppendLine("## 汇总");
        report.AppendLine();
        report.AppendLine($"- 人物：{CharacterNames.Length} 种（各用 Black 配色代表三套配色验证）");
        report.AppendLine($"- 载具：{VehicleNames.Length} 个优化版 Prefab");
        report.AppendLine($"- 场景缺失脚本：{missingScripts}");
        report.AppendLine("- 人物预览动作：Idle → Walk → Run → 自动步枪单发 → Death_01");
        report.AppendLine("- 已知限制：坦克履带与车身共用网格/材质，本阶段不修改模型，因此不启用履带 UV 滚动。");
        report.AppendLine("- 本场景未加入 Build Settings，也未连接现有战斗规则。");

        if (missingScripts > 0)
            failures.Add("Gallery contains " + missingScripts + " missing scripts");

        EditorSceneManager.CloseScene(scene, true);

        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ReportPath), report.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);

        if (failures.Count > 0)
            throw new InvalidOperationException("Gallery validation failed:\n- " + string.Join("\n- ", failures));
    }

    private static string DescribeVehicleComponents(GameObject unit)
    {
        var parts = new List<string>();
        VehicleWheels wheels = unit.GetComponent<VehicleWheels>();
        if (wheels != null && wheels.wheels.Count > 0) parts.Add("车轮×" + wheels.wheels.Count);
        VehicleTurretAim aim = unit.GetComponent<VehicleTurretAim>();
        if (aim != null && (aim.turret != null || aim.barrel != null)) parts.Add("炮塔/炮管");
        HeliRotor rotor = unit.GetComponent<HeliRotor>();
        if (rotor != null && rotor.mainRotor != null) parts.Add("旋翼");
        MissileRackLauncher rack = unit.GetComponent<MissileRackLauncher>();
        if (rack != null && rack.rack != null) parts.Add("导弹架");
        return parts.Count > 0 ? string.Join("、", parts) : "仅速度源";
    }

    private static int CountMissingMaterials(GameObject root)
    {
        int count = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            count += renderer.sharedMaterials.Count(material => material == null || material.shader == null);
        return count;
    }

    private static int CountMissingScripts(Scene scene)
    {
        int total = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        return total;
    }

    private static GameObject FindSceneObjectContaining(Scene scene, string fragment)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Contains(fragment)) return child.gameObject;
        return null;
    }

    private static GameObject InstantiateRequired(string path, Vector3 position, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent);
        instance.transform.position = position;
        return instance;
    }

    private static void CreateLabel(Transform parent, string content, float height)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent);
        label.transform.localPosition = new Vector3(0f, height, 0f);
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = content;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.12f;
        text.fontSize = 42;
        text.color = Color.white;
        label.AddComponent<BillboardLabel>();
    }

    private static void CreateEnvironment()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 18f, -24f);
        cameraObject.transform.LookAt(new Vector3(0f, 2f, 18f));
        camera.farClipPlane = 150f;
        cameraObject.AddComponent<GalleryCameraController>();

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GalleryGround";
        ground.transform.position = new Vector3(0f, -0.03f, 18f);
        ground.transform.localScale = new Vector3(4f, 1f, 6f);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
