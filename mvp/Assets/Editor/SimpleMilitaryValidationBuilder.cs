using Mvp.Validation;
using SimpleMilitary.VehicleAnimation;
using SimpleMilitary.VehicleAnimation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class SimpleMilitaryValidationBuilder
{
    private const string SceneDirectory = "Assets/SimpleMilitaryValidation";
    private const string ScenePath = SceneDirectory + "/SimpleMilitaryValidation.unity";

    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder(SceneDirectory);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateEnvironment();

        GameObject character = InstantiateRequired(
            "Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_General_Black.prefab",
            new Vector3(0f, 0f, 0f));
        character.name = "Character_General_Idle";

        GameObject tank = InstantiateRequired(
            "Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/SK_Veh_Tank_01.prefab",
            new Vector3(-4.5f, 0f, 3f));
        tank.name = "Vehicle_Tank_TurretValidation";
        VehicleAutoSetup.Setup(tank);

        GameObject truck = InstantiateRequired(
            "Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/SK_Veh_Truck_Troop_01.prefab",
            new Vector3(4.5f, 0f, 2f));
        truck.name = "Vehicle_Truck_WheelValidation";
        VehicleAutoSetup.Setup(truck);

        GameObject helicopter = InstantiateRequired(
            "Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/SK_Veh_Attack_Heli_01.prefab",
            new Vector3(0f, 3.2f, 7f));
        helicopter.name = "Vehicle_Helicopter_RotorValidation";
        VehicleAutoSetup.Setup(helicopter);

        GameObject drone = InstantiateRequired(
            "Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/SK_Veh_Drone_01.prefab",
            new Vector3(4.5f, 2.2f, 7f));
        drone.name = "Vehicle_Drone_RotorValidation";
        VehicleAutoSetup.Setup(drone);

        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "TurretAimTarget";
        target.transform.position = new Vector3(-4.5f, 1.5f, 8f);
        target.transform.localScale = Vector3.one * 0.35f;

        VehicleTurretAim aim = tank.GetComponent<VehicleTurretAim>();
        if (aim != null)
            aim.target = target.transform;

        GameObject controllerObject = new GameObject("SimpleMilitaryValidationController");
        SimpleMilitaryValidationController controller =
            controllerObject.AddComponent<SimpleMilitaryValidationController>();
        controller.movingVehicle = truck.transform;
        controller.turretTarget = target.transform;
        controller.characterAnimator = character.GetComponent<Animator>();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new System.InvalidOperationException("Failed to save validation scene: " + ScenePath);

        AssetDatabase.SaveAssets();
        Debug.Log("[SimpleMilitaryValidation] Scene created successfully: " + ScenePath);
    }

    public static void Verify()
    {
        var failures = new List<string>();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                if (missing > 0)
                    failures.Add(child.name + " has " + missing + " missing script(s)");
            }
        }

        GameObject character = GameObject.Find("Character_General_Idle");
        Animator animator = character != null ? character.GetComponent<Animator>() : null;
        if (animator == null)
            failures.Add("Character Animator is missing");
        else
        {
            if (animator.runtimeAnimatorController == null)
                failures.Add("Character Animator Controller is missing");
            if (animator.avatar == null)
                failures.Add("Character Avatar is missing");
        }

        GameObject truck = GameObject.Find("Vehicle_Truck_WheelValidation");
        VehicleWheels wheels = truck != null ? truck.GetComponent<VehicleWheels>() : null;
        if (wheels == null || wheels.wheels.Count == 0)
            failures.Add("Truck wheel animation was not configured");

        GameObject tank = GameObject.Find("Vehicle_Tank_TurretValidation");
        VehicleTurretAim aim = tank != null ? tank.GetComponent<VehicleTurretAim>() : null;
        if (aim == null || aim.turret == null || aim.target == null)
            failures.Add("Tank turret animation was not configured");

        ValidateRotor("Vehicle_Helicopter_RotorValidation", failures);
        ValidateRotor("Vehicle_Drone_RotorValidation", failures);
        ValidateFixedCharacterPrefab(
            "Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_FemaleMedic_Black.prefab",
            failures);
        ValidateFixedCharacterPrefab(
            "Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_FemaleSoldier_Black.prefab",
            failures);

        if (failures.Count > 0)
            throw new System.InvalidOperationException(
                "Simple Military validation failed:\n- " + string.Join("\n- ", failures));

        Debug.Log("[SimpleMilitaryValidation] PASS: scene, scripts, character rig, fixed prefabs and vehicle components verified.");
    }

    private static void ValidateRotor(string objectName, List<string> failures)
    {
        GameObject vehicle = GameObject.Find(objectName);
        HeliRotor rotor = vehicle != null ? vehicle.GetComponent<HeliRotor>() : null;
        if (rotor == null || rotor.mainRotor == null)
            failures.Add(objectName + " rotor animation was not configured");
    }

    private static void ValidateFixedCharacterPrefab(string path, List<string> failures)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            failures.Add("Fixed prefab is missing: " + path);
            return;
        }

        int animatorCount = prefab.GetComponentsInChildren<Animator>(true).Length;
        int rendererCount = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
        if (animatorCount != 1 || rendererCount != 20)
            failures.Add(path + " expected 1 Animator/20 renderers, got " + animatorCount + "/" + rendererCount);
    }

    private static GameObject InstantiateRequired(string assetPath, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            throw new System.InvalidOperationException("Required prefab was not imported: " + assetPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        return instance;
    }

    private static void CreateEnvironment()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 7f, -14f);
        cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 3.5f));
        camera.clearFlags = CameraClearFlags.Skybox;

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "ValidationGround";
        ground.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
