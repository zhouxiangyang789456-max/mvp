using System;
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>
    /// One-shot setup for the tank FBX produced by the Blender pipeline.
    /// The tank is a STATIC model (46 static Tank_part meshes, no armature), so:
    ///   1. Configures the ModelImporter (animationType=None, importAnimation=false).
    ///   2. Builds Tank.prefab at Assets/Resources/Battle/Units (model only; no
    ///      Animator / no UnitAnimationDriver, since the tank has no animations).
    /// Keeps the original camo colors: materials import via ImportStandard and the
    /// runtime TintModel multiplies team color on top.
    /// Run manually via 菜单 "Mvp/Setup Tank Asset", or automatically on editor
    /// startup once the FBX is imported.
    /// </summary>
    public static class TankAssetSetup
    {
        const string FbxPath = "Assets/Art/Battle/Units/Tank/Tank.fbx";
        const string PrefabPath = "Assets/Resources/Battle/Units/Tank.prefab";
        const string PrefabFolder = "Assets/Resources/Battle/Units";

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            // Runs once Unity opens / reloads and assets have had a chance to import.
            EditorApplication.delayCall += () => DelayedSetup(0);
        }

        static void DelayedSetup(int attempt)
        {
            if (attempt > 30) return; // give up after ~30 frames

            string fbxAbs = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), FbxPath);
            if (!System.IO.File.Exists(fbxAbs)) return; // FBX not in project yet

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                EditorApplication.delayCall += () => DelayedSetup(attempt + 1);
                return;
            }

            // Rebuild the prefab whenever the FBX is newer than it (i.e. it was
            // re-exported from Blender). Idempotent once the prefab is up to date.
            string prefabAbs = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), PrefabPath);
            bool needsBuild = !System.IO.File.Exists(prefabAbs);
            if (!needsBuild)
                needsBuild = System.IO.File.GetLastWriteTimeUtc(fbxAbs) >
                             System.IO.File.GetLastWriteTimeUtc(prefabAbs);
            if (needsBuild) RunSetup();
        }

        [MenuItem("Mvp/Setup Tank Asset")]
        public static void RunSetup()
        {
            try
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(), FbxPath)))
                {
                    Debug.LogError("[TankAssetSetup] FBX not found: " + FbxPath);
                    return;
                }

                ConfigureTextures();
                ConfigureImporter();
                BuildPrefab(PrefabPath);

                Debug.Log("[TankAssetSetup] Done. Prefab=" + PrefabPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[TankAssetSetup] Failed: " + e);
            }
        }

        // ---------------------------------------------------------------- textures

        static void ConfigureTextures()
        {
            const string texFolder = "Assets/Art/Battle/Units/Tank/Textures";
            string projectRoot = System.IO.Directory.GetCurrentDirectory();
            string texDirAbs = System.IO.Path.Combine(projectRoot,
                texFolder.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.Directory.Exists(texDirAbs)) return;

            foreach (var file in System.IO.Directory.GetFiles(texDirAbs, "*_Normal.png"))
            {
                string rel = System.IO.Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (ti == null) continue;
                bool changed = ti.textureType != TextureImporterType.NormalMap;
                if (changed) ti.textureType = TextureImporterType.NormalMap;
                if (ti.isReadable != true) { ti.isReadable = true; changed = true; }
                if (changed) { ti.SaveAndReimport(); Debug.Log("[TankAssetSetup] normal map: " + rel); }
            }
            foreach (var file in System.IO.Directory.GetFiles(texDirAbs, "*_BaseColor.png"))
            {
                string rel = System.IO.Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (ti != null && ti.isReadable != true)
                {
                    ti.isReadable = true;
                    ti.SaveAndReimport();
                    Debug.Log("[TankAssetSetup] basecolor readable: " + rel);
                }
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
            if (importer.animationType != ModelImporterAnimationType.None)
                { importer.animationType = ModelImporterAnimationType.None; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (!importer.useFileScale) { importer.useFileScale = true; changed = true; }
            if (Math.Abs(importer.globalScale - 1f) > 0.0001f) { importer.globalScale = 1f; changed = true; }
            if (importer.importBlendShapes) { importer.importBlendShapes = false; changed = true; }
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            // Import materials + textures so the tank keeps its camo; the runtime
            // TintModel still multiplies team color on top.
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                { importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; changed = true; }

            if (changed) importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- prefab

        static void BuildPrefab(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                Debug.LogError("[TankAssetSetup] Cannot load model from " + FbxPath);
                return;
            }

            var tempRoot = new GameObject("_TankSetupTemp");
            var instance = (GameObject)UnityEngine.Object.Instantiate(model, tempRoot.transform);
            instance.name = "Tank";

            // Static tank: remove any Animator the import may have created.
            var animator = instance.GetComponent<Animator>();
            if (animator != null) UnityEngine.Object.DestroyImmediate(animator);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(tempRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
    }
}
