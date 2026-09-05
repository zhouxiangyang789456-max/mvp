using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Mvp.Battle.Units;

namespace Mvp.EditorTools
{
    /// <summary>
    /// One-shot setup for the infantry FBX produced by the Blender pipeline:
    ///   1. Configures the ModelImporter (Generic rig, animation on).
    ///   2. Marks the 4 clips (Idle/Move/Attack/Occupy) as looping.
    ///   3. Builds Infantry.controller (4 states + int "State" param + transitions).
    ///   4. Builds Infantry.prefab at Assets/Resources/Battle/Units (model + Animator +
    ///      UnitAnimationDriver + a togglable Flag_Root).
    /// Run manually via 菜单 "Mvp/Setup Infantry Asset", or automatically on editor
    /// startup once the FBX is imported.
    /// </summary>
    public static class InfantryAssetSetup
    {
        const string FbxPath = "Assets/Art/Battle/Units/Infantry/Infantry.fbx";
        // Controller lives under a Resources folder so UnitAnimationDriver can
        // load it at runtime as a fallback (the serialized prefab reference can
        // drop when the controller's GUID churns across rebuilds).
        const string OldControllerPath = "Assets/Art/Battle/Units/Infantry/Infantry.controller";
        const string ControllerPath = "Assets/Resources/Battle/Units/InfantryAnimator.controller";
        const string PrefabPath = "Assets/Resources/Battle/Units/Infantry.prefab";
        const string SimpleMilitaryPrefabPath =
            "Assets/SimpleMilitary/Old/Prefabs/Characters/SimpleMilitary_SpecialForces01_Black.prefab";
        const string PrefabFolder = "Assets/Resources/Battle/Units";

        // Clip name (state name) -> integer used by the "State" param / transitions.
        static readonly string[] ClipNames = { "Idle", "Move", "Attack", "Occupy" };

        static bool _autoSetupAttempted;

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            // Runs once Unity opens and assets have had a chance to import.
            EditorApplication.delayCall += () => DelayedSetup(0);
        }

        static void DelayedSetup(int attempt)
        {
            if (_autoSetupAttempted) return;
            if (attempt > 30) return; // give up after ~30 frames

            // The selected Simple Military infantry is now the canonical runtime
            // visual. Do not let this legacy FBX bootstrap overwrite it on reload.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SimpleMilitaryPrefabPath) != null)
            {
                _autoSetupAttempted = true;
                return;
            }

            if (!System.IO.File.Exists(System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), FbxPath)))
                return; // FBX not in project yet

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                EditorApplication.delayCall += () => DelayedSetup(attempt + 1);
                return;
            }

            _autoSetupAttempted = true;
            RunSetup();
        }

        [MenuItem("Mvp/Setup Infantry Asset")]
        public static void RunSetup()
        {
            try
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(), FbxPath)))
                {
                    Debug.LogError("[InfantryAssetSetup] FBX not found: " + FbxPath);
                    return;
                }

                MigrateLegacyController();

                ConfigureImporter();

                var clips = FindClips(FbxPath);
                SetClipsLoop(clips);

                var controller = BuildController(ControllerPath, clips);
                if (controller == null)
                {
                    Debug.LogError("[InfantryAssetSetup] Controller build failed.");
                    return;
                }

                BuildPrefab(PrefabPath, clips, controller);

                Debug.Log(
                    "[InfantryAssetSetup] Done. Controller=" + ControllerPath +
                    " Prefab=" + PrefabPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[InfantryAssetSetup] Failed: " + e);
            }
        }

        /// <summary>
        /// Removes the controller that used to live next to the FBX
        /// (Assets/Art/...). It now lives in Resources so it can be loaded at
        /// runtime; leaving the old asset around only confuses the pipeline.
        /// </summary>
        static void MigrateLegacyController()
        {
            string legacyMeta = OldControllerPath + ".meta";
            if (System.IO.File.Exists(System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), OldControllerPath)))
            {
                AssetDatabase.DeleteAsset(OldControllerPath);
                Debug.Log("[InfantryAssetSetup] Removed legacy controller " + OldControllerPath);
            }
            else if (System.IO.File.Exists(System.IO.Path.Combine(
                         System.IO.Directory.GetCurrentDirectory(), legacyMeta)))
            {
                // Orphaned .meta with no asset — clean it up too.
                System.IO.File.Delete(legacyMeta);
            }
        }

        // ---------------------------------------------------------------- importer

        static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            }
            if (importer == null) return;

            // Only SaveAndReimport when a setting actually changes, so the
            // auto-setup is idempotent and doesn't trigger a reimport loop.
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
                { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
            if (importer.animationCompression != ModelImporterAnimationCompression.Off)
                { importer.animationCompression = ModelImporterAnimationCompression.Off; changed = true; }
            if (!importer.useFileScale) { importer.useFileScale = true; changed = true; }
            if (Math.Abs(importer.globalScale - 1f) > 0.0001f) { importer.globalScale = 1f; changed = true; }
            if (importer.importBlendShapes) { importer.importBlendShapes = false; changed = true; }
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            // Import materials + textures so the soldier keeps its camo;
            // the runtime TintModel still multiplies team color on top.
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                { importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; changed = true; }

            if (changed) importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- clips

        static Dictionary<string, AnimationClip> FindClips(string fbxPath)
        {
            var result = new Dictionary<string, AnimationClip>();
            var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var o in all)
            {
                var clip = o as AnimationClip;
                if (clip == null) continue;
                foreach (var name in ClipNames)
                {
                    if (clip.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                        && !result.ContainsKey(name))
                    {
                        result[name] = clip;
                    }
                }
            }

            foreach (var name in ClipNames)
            {
                if (!result.ContainsKey(name))
                    Debug.LogWarning("[InfantryAssetSetup] Clip not found for: " + name);
                else
                    Debug.Log("[InfantryAssetSetup] Clip '" + result[name].name +
                        "' frames " + result[name].frameRate + " length " +
                        result[name].length.ToString("F2") + "s");
            }
            return result;
        }

        static void SetClipsLoop(Dictionary<string, AnimationClip> clips)
        {
            bool changed = false;
            foreach (var kv in clips)
            {
                if (kv.Value == null) continue;
                var settings = AnimationUtility.GetAnimationClipSettings(kv.Value);
                if (!settings.loopTime)
                {
                    settings.loopTime = true;
                    settings.loopBlend = true;
                    AnimationUtility.SetAnimationClipSettings(kv.Value, settings);
                    changed = true;
                }
            }
            if (changed) AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- controller

        static AnimatorController BuildController(
            string path, Dictionary<string, AnimationClip> clips)
        {
            // Reuse an existing controller with the full state set so the asset
            // GUID stays stable across rebuilds. Deleting + recreating churns the
            // GUID, which can leave prefab Animator references dangling at runtime.
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null && HasAllStates(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null) return null;
            controller.AddParameter("State", AnimatorControllerParameterType.Int);

            var sm = controller.layers[0].stateMachine;

            var states = new Dictionary<string, AnimatorState>();
            foreach (var name in ClipNames)
            {
                AnimationClip clip;
                if (!clips.TryGetValue(name, out clip) || clip == null) continue;

                var state = sm.AddState(name);
                state.motion = clip;
                states[name] = state;
            }

            if (!states.ContainsKey("Idle"))
            {
                Debug.LogError("[InfantryAssetSetup] No Idle clip; controller incomplete.");
                return controller;
            }
            sm.defaultState = states["Idle"];

            // Transitions: every state -> every other state when State == <int>.
            var targets = new[] { "Idle", "Move", "Attack", "Occupy" };
            foreach (var fromName in targets)
            {
                AnimatorState from;
                if (!states.TryGetValue(fromName, out from)) continue;
                foreach (var toName in targets)
                {
                    if (fromName == toName) continue;
                    AnimatorState to;
                    if (!states.TryGetValue(toName, out to)) continue;
                    var tr = from.AddTransition(to);
                    tr.hasExitTime = false;
                    tr.duration = 0.10f;
                    tr.AddCondition(AnimatorConditionMode.Equals, (float)IndexOf(toName), "State");
                }
            }

            return controller;
        }

        static int IndexOf(string name)
        {
            for (int i = 0; i < ClipNames.Length; i++)
                if (ClipNames[i] == name) return i;
            return 0;
        }

        static bool HasAllStates(AnimatorController controller)
        {
            if (controller == null) return false;
            if (controller.parameters == null ||
                !System.Array.Exists(controller.parameters,
                    p => p.name == "State" && p.type == AnimatorControllerParameterType.Int))
                return false;
            var sm = controller.layers.Length > 0
                ? controller.layers[0].stateMachine
                : null;
            if (sm == null) return false;
            var present = new System.Collections.Generic.HashSet<string>();
            foreach (var child in sm.states) present.Add(child.state.name);
            foreach (var name in ClipNames)
                if (!present.Contains(name)) return false;
            return true;
        }

        // ---------------------------------------------------------------- prefab

        static void BuildPrefab(string path, Dictionary<string, AnimationClip> clips,
            AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                Debug.LogError("[InfantryAssetSetup] Cannot load model from " + FbxPath);
                return;
            }

            var tempRoot = new GameObject("_InfantrySetupTemp");
            var instance = (GameObject)UnityEngine.Object.Instantiate(model, tempRoot.transform);
            instance.name = "Infantry";

            // Animator
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            if (animator.avatar == null)
            {
                var avatar = LoadAvatarFromFbx();
                if (avatar != null) animator.avatar = avatar;
            }

            // Reparent flag meshes under a togglable Flag_Root.
            var flagPole = FindInChildren(instance.transform, "Flag_Pole");
            var flagCloth = FindInChildren(instance.transform, "Flag_Cloth");
            if (flagPole != null || flagCloth != null)
            {
                var flagRoot = new GameObject("Flag_Root");
                flagRoot.transform.SetParent(instance.transform, false);
                if (flagPole != null) flagPole.transform.SetParent(flagRoot.transform, true);
                if (flagCloth != null) flagCloth.transform.SetParent(flagRoot.transform, true);
            }

            // Animation driver
            if (instance.GetComponentInChildren<UnitAnimationDriver>() == null)
                instance.AddComponent<UnitAnimationDriver>();

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(tempRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static Avatar LoadAvatarFromFbx()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            foreach (var o in all)
            {
                var avatar = o as Avatar;
                if (avatar != null) return avatar;
            }
            return null;
        }

        static void EnsureFolder(string folder)
        {
            string parent = "Assets";
            string cur = "Assets";
            foreach (var part in folder.Substring("Assets/".Length).Split('/'))
            {
                string next = cur + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, part);
                parent = next;
                cur = next;
            }
        }

        static Transform FindInChildren(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindInChildren(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
