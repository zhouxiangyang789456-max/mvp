using System;
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>
    /// One-shot setup for the three new STATIC units (Scout / ScoutCar /
    /// RocketArtillery) produced by the Blender pipeline. Each is a static model
    /// (no armature, no animation), so all follow the same recipe as
    /// TankAssetSetup:
    ///   1. Configure the ModelImporter (animationType=None, importAnimation=false,
    ///      ImportStandard materials, scale=1, readable).
    ///   2. Configure textures (normals -&gt; NormalMap, basecolor readable).
    ///   3. Build &lt;Name&gt;.prefab at Assets/Resources/Battle/Units (model only;
    ///      no Animator / no UnitAnimationDriver).
    /// Run manually via 菜单 "Mvp/Setup New Unit Assets", or automatically on
    /// editor startup once the FBX files are imported.
    /// </summary>
    public static class NewUnitAssetSetup
    {
        const string PrefabFolder = "Assets/Resources/Battle/Units";

        class UnitConfig
        {
            public string Name;
            public string FbxPath;
            public string PrefabPath;
            public string TexFolder;
        }

        static readonly UnitConfig[] Units =
        {
            new UnitConfig
            {
                Name = "Scout",
                FbxPath = "Assets/Art/Battle/Units/Scout/Scout.fbx",
                PrefabPath = "Assets/Resources/Battle/Units/Scout.prefab",
                TexFolder = "Assets/Art/Battle/Units/Scout/Textures",
            },
            new UnitConfig
            {
                Name = "ScoutCar",
                FbxPath = "Assets/Art/Battle/Units/ScoutCar/ScoutCar.fbx",
                PrefabPath = "Assets/Resources/Battle/Units/ScoutCar.prefab",
                TexFolder = "Assets/Art/Battle/Units/ScoutCar/Textures",
            },
            new UnitConfig
            {
                Name = "RocketArtillery",
                FbxPath = "Assets/Art/Battle/Units/RocketArtillery/RocketArtillery.fbx",
                PrefabPath = "Assets/Resources/Battle/Units/RocketArtillery.prefab",
                TexFolder = "Assets/Art/Battle/Units/RocketArtillery/Textures",
            },
        };

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            // Runs once Unity opens / reloads and assets have had a chance to import.
            EditorApplication.delayCall += () => DelayedSetup(0);
        }

        static void DelayedSetup(int attempt)
        {
            if (attempt > 60) return; // give up after ~60 frames

            bool allReady = true;
            bool anyNeedsBuild = false;
            foreach (var u in Units)
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(), u.FbxPath)))
                {
                    allReady = false;
                    continue;
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(u.FbxPath) == null)
                {
                    allReady = false;
                    continue;
                }
                if (NeedsBuild(u)) anyNeedsBuild = true;
            }

            if (!allReady)
            {
                EditorApplication.delayCall += () => DelayedSetup(attempt + 1);
                return;
            }
            if (anyNeedsBuild) RunSetup();
        }

        static bool NeedsBuild(UnitConfig u)
        {
            string fbxAbs = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), u.FbxPath);
            string prefabAbs = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), u.PrefabPath);
            if (!System.IO.File.Exists(prefabAbs)) return true;
            return System.IO.File.GetLastWriteTimeUtc(fbxAbs) >
                   System.IO.File.GetLastWriteTimeUtc(prefabAbs);
        }

        [MenuItem("Mvp/Setup New Unit Assets")]
        public static void RunSetup()
        {
            try
            {
                foreach (var u in Units)
                {
                    if (!System.IO.File.Exists(System.IO.Path.Combine(
                            System.IO.Directory.GetCurrentDirectory(), u.FbxPath)))
                    {
                        Debug.LogError("[NewUnitAssetSetup] FBX not found: " + u.FbxPath);
                        continue;
                    }
                    ConfigureTextures(u);
                    ConfigureImporter(u);
                    BuildPrefab(u);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[NewUnitAssetSetup] Done. Prefabs: Scout / ScoutCar / RocketArtillery -> " + PrefabFolder);
            }
            catch (Exception e)
            {
                Debug.LogError("[NewUnitAssetSetup] Failed: " + e);
            }
        }

        // ---------------------------------------------------------------- textures

        static void ConfigureTextures(UnitConfig u)
        {
            string projectRoot = System.IO.Directory.GetCurrentDirectory();
            string texDirAbs = System.IO.Path.Combine(projectRoot,
                u.TexFolder.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.Directory.Exists(texDirAbs)) return;

            foreach (var file in System.IO.Directory.GetFiles(texDirAbs, "*_Normal.png"))
            {
                string rel = System.IO.Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (ti == null) continue;
                bool changed = ti.textureType != TextureImporterType.NormalMap;
                if (changed) ti.textureType = TextureImporterType.NormalMap;
                if (ti.isReadable != true) { ti.isReadable = true; changed = true; }
                if (changed) { ti.SaveAndReimport(); Debug.Log("[NewUnitAssetSetup] normal map: " + rel); }
            }
            foreach (var file in System.IO.Directory.GetFiles(texDirAbs, "*_BaseColor.png"))
            {
                string rel = System.IO.Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                if (ti != null && ti.isReadable != true)
                {
                    ti.isReadable = true;
                    ti.SaveAndReimport();
                    Debug.Log("[NewUnitAssetSetup] basecolor readable: " + rel);
                }
            }
        }

        // ---------------------------------------------------------------- importer

        static void ConfigureImporter(UnitConfig u)
        {
            var importer = AssetImporter.GetAtPath(u.FbxPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(u.FbxPath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(u.FbxPath) as ModelImporter;
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
            // Import materials + textures so the unit keeps its baked look; the
            // runtime TintModel still multiplies team color on top.
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                { importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; changed = true; }

            if (changed) importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- prefab

        static void BuildPrefab(UnitConfig u)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(u.FbxPath);
            if (model == null)
            {
                Debug.LogError("[NewUnitAssetSetup] Cannot load model from " + u.FbxPath);
                return;
            }

            var tempRoot = new GameObject("_" + u.Name + "SetupTemp");
            var instance = (GameObject)UnityEngine.Object.Instantiate(model, tempRoot.transform);
            instance.name = u.Name;

            // Static unit: remove any Animator the import may have created.
            var animator = instance.GetComponent<Animator>();
            if (animator != null) UnityEngine.Object.DestroyImmediate(animator);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(instance, u.PrefabPath);
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
