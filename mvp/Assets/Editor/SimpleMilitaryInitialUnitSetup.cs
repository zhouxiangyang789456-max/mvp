#if UNITY_EDITOR
using System;
using Mvp.Battle.Units;
using Mvp.CommanderSelect;
using Mvp.Shared;
using SimpleMilitary.VehicleAnimation;
using SimpleMilitary.VehicleAnimation.Editor;
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>Builds the three initial runtime visuals from the imported package.</summary>
    public static class SimpleMilitaryInitialUnitSetup
    {
        const string AutoRunKey = "Mvp.SimpleMilitaryInitialUnitSetup.v3.tankForward";
        const string InfantrySource =
            "Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_SpecialForces01_Black.prefab";
        const string TankSource =
            "Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/SK_Veh_Tank_01.prefab";
        const string FieldGunSource =
            "Assets/SimpleMilitary/Optimized/Prefabs/Vehicles/SK_Veh_FieldGun_01.prefab";
        const string RuntimeFolder = "Assets/Resources/Battle/Units";
        const string InfantryTarget = RuntimeFolder + "/Infantry.prefab";
        const string TankTarget = RuntimeFolder + "/Tank.prefab";
        const string FieldGunTarget = RuntimeFolder + "/RocketArtillery.prefab";

        [InitializeOnLoadMethod]
        static void ScheduleInitialBuild()
        {
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            EditorApplication.delayCall += RunSetupAndVerify;
        }

        [MenuItem("Mvp/Simple Military/Build Initial Commander Units")]
        public static void RunSetupAndVerify()
        {
            EnsureFolder(RuntimeFolder);
            BuildInfantry();
            BuildVehicle(TankSource, TankTarget, "InitialTank_SK_Veh_Tank_01",
                0.14f, 0.38f, 180f);
            BuildVehicle(FieldGunSource, FieldGunTarget, "InitialRanged_SK_Veh_FieldGun_01",
                0.13f, 0.38f, 0f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Verify();
        }

        static void BuildInfantry()
        {
            var instance = InstantiateSource(InfantrySource);
            try
            {
                instance.name = "InitialInfantry_SpecialForces01";
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("SpecialForces01 has no Animator/controller.");
                animator.applyRootMotion = false;

                if (instance.GetComponent<UnitAnimationDriver>() == null)
                    instance.AddComponent<UnitAnimationDriver>();
                ConfigureProfile(instance, 0.25f, 0.50f, 0.05f, false);
                Save(instance, InfantryTarget);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static void BuildVehicle(string sourcePath, string targetPath, string runtimeName,
            float modelScale, float healthAnchorY, float containerYaw)
        {
            var instance = InstantiateSource(sourcePath);
            try
            {
                instance.name = runtimeName;
                VehicleAutoSetup.Setup(instance);
                if (instance.GetComponent<VehicleUnitAnimationDriver>() == null)
                    instance.AddComponent<VehicleUnitAnimationDriver>();
                ConfigureProfile(instance, modelScale, healthAnchorY, 0.03f, true);
                instance.GetComponent<UnitModelProfile>().ContainerEuler =
                    new Vector3(0f, containerYaw, 0f);
                Save(instance, targetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static GameObject InstantiateSource(string sourcePath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
                throw new InvalidOperationException("Missing source prefab: " + sourcePath);
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(source);
            return instance;
        }

        static void ConfigureProfile(GameObject root, float scale, float anchorY,
            float clearance, bool singleVisual)
        {
            var profile = root.GetComponent<UnitModelProfile>();
            if (profile == null) profile = root.AddComponent<UnitModelProfile>();
            profile.ModelScale = scale;
            profile.HealthAnchorY = anchorY;
            profile.GroundClearance = clearance;
            profile.SingleVisualPerSlot = singleVisual;
            profile.ContainerEuler = Vector3.zero;
            profile.InstanceEuler = Vector3.zero;
            profile.FacingYawOffset = 0f;
            EditorUtility.SetDirty(profile);
        }

        static void Save(GameObject instance, string targetPath)
        {
            bool success;
            PrefabUtility.SaveAsPrefabAsset(instance, targetPath, out success);
            if (!success) throw new InvalidOperationException("Failed to save " + targetPath);
            Debug.Log("[SimpleMilitaryInitialUnitSetup] Built " + targetPath);
        }

        static void Verify()
        {
            VerifyInfantry();
            VerifyVehicle(TankTarget, "Tank_01");
            VerifyVehicle(FieldGunTarget, "FieldGun_01");

            var commanders = CommanderCatalog.GetAll();
            Require(commanders.Count >= 3, "Three initial commanders are required.");
            Require(commanders[0].StartingUnits.Count == 1 &&
                commanders[0].StartingUnits[0].Count == 1, "Infantry commander must start with one slot.");
            Require(commanders[1].StartingUnits.Count == 1 &&
                commanders[1].StartingUnits[0].Count == 1, "Tank commander must start with one unit.");
            Require(commanders[2].StartingUnits.Count == 1 &&
                commanders[2].StartingUnits[0].Count == 1, "Ranged commander must start with one unit.");
            Debug.Log("[SimpleMilitaryInitialUnitSetup] VERIFIED: SpecialForces01, Tank 01 and Field Gun 01 are ready.");
        }

        static void VerifyInfantry()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfantryTarget);
            Require(prefab != null, "Runtime infantry prefab missing.");
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Require(animator != null && animator.runtimeAnimatorController != null,
                "Runtime infantry Animator/controller missing.");
            Require(prefab.GetComponent<UnitAnimationDriver>() != null,
                "Runtime infantry animation bridge missing.");
            Require(prefab.GetComponent<UnitModelProfile>() != null,
                "Runtime infantry model profile missing.");
        }

        static void VerifyVehicle(string path, string sourceToken)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, "Runtime vehicle prefab missing: " + path);
            Require(prefab.name.IndexOf(sourceToken, StringComparison.OrdinalIgnoreCase) >= 0,
                "Runtime vehicle does not identify selected source: " + sourceToken);
            Require(prefab.GetComponent<VehicleMotion>() != null,
                "Vehicle motion animation source missing: " + path);
            Require(prefab.GetComponent<VehicleTurretAim>() != null,
                "Vehicle turret/barrel animation missing: " + path);
            Require(prefab.GetComponent<VehicleUnitAnimationDriver>() != null,
                "Vehicle combat animation bridge missing: " + path);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
