using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>
    /// Batch-mode entry point used to import + verify the infantry FBX:
    ///   1. Marks *_Normal.png textures as NormalMap.
    ///   2. Runs InfantryAssetSetup (import config, clips loop, controller, prefab).
    ///   3. Verifies clips, materials, bones/orientation, and model height.
    /// Entry: Mvp/Verify Infantry Setup  (or Unity -executeMethod).
    /// </summary>
    public static class InfantryVerifySetup
    {
        const string FbxPath = "Assets/Art/Battle/Units/Infantry/Infantry.fbx";
        const string PrefabPath = "Assets/Resources/Battle/Units/Infantry.prefab";
        const string ControllerPath = "Assets/Resources/Battle/Units/InfantryAnimator.controller";
        const string TexFolder = "Assets/Art/Battle/Units/Infantry/Textures";

        [MenuItem("Mvp/Verify Infantry Setup")]
        public static void RunMenu()
        {
            Run();
        }

        public static void Run()
        {
            Debug.Log("[InfantryVerifySetup] === start ===");

            // 1) Normal maps
            string projectRoot = Directory.GetCurrentDirectory();
            string texDirAbs = Path.Combine(projectRoot, TexFolder.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(texDirAbs))
            {
                foreach (var file in Directory.GetFiles(texDirAbs, "*_Normal.png"))
                {
                    string rel = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                    if (ti != null)
                    {
                        bool changed = ti.textureType != TextureImporterType.NormalMap;
                        if (changed) ti.textureType = TextureImporterType.NormalMap;
                        // Keep texture readable so Renderer.material.color tinting works on the base map.
                        if (ti.isReadable != true) { ti.isReadable = true; changed = true; }
                        if (changed) { ti.SaveAndReimport(); Debug.Log("[InfantryVerifySetup] normal map: " + rel); }
                    }
                }
                // Also keep base-color maps readable (tint path).
                foreach (var file in Directory.GetFiles(texDirAbs, "*_BaseColor.png"))
                {
                    string rel = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    var ti = AssetImporter.GetAtPath(rel) as TextureImporter;
                    if (ti != null && ti.isReadable != true)
                    {
                        ti.isReadable = true;
                        ti.SaveAndReimport();
                        Debug.Log("[InfantryVerifySetup] basecolor readable: " + rel);
                    }
                }
            }
            AssetDatabase.Refresh();

            // 2) Run the standard setup
            InfantryAssetSetup.RunSetup();

            // 3) Verify clips on the controller
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                ControllerPath);
            if (controller != null)
            {
                foreach (var state in controller.layers[0].stateMachine.states)
                {
                    var motion = state.state.motion as AnimationClip;
                    Debug.Log("[InfantryVerifySetup] state " + state.state.name +
                              " -> clip " + (motion != null ? motion.name : "(null)") +
                              " len " + (motion != null ? motion.length.ToString("F2") : "-") + "s");
                }
            }
            else
            {
                Debug.LogError("[InfantryVerifySetup] controller not found");
            }

            // 4) Instantiate prefab and inspect
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[InfantryVerifySetup] prefab not found at " + PrefabPath);
                return;
            }

            var root = new GameObject("_VerifyTemp");
            var inst = (GameObject)Object.Instantiate(prefab, root.transform);
            inst.name = "InfantryVerify";

            var rends = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Debug.Log("[InfantryVerifySetup] skinned renderers: " + rends.Length);
            foreach (var r in rends)
            {
                string tex = "none";
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_MainTex")
                    && r.sharedMaterial.GetTexture("_MainTex") != null)
                    tex = r.sharedMaterial.GetTexture("_MainTex").name;
                Debug.Log("[InfantryVerifySetup]   " + r.gameObject.name +
                          " mats=" + r.sharedMaterials.Length + " _MainTex=" + tex);
            }

            // Orientation: Head bone offset relative to Root in the instantiated model.
            Transform headBone = null, rootBone = null;
            foreach (var r in rends)
            {
                if (r.bones == null) continue;
                foreach (var b in r.bones)
                {
                    if (b == null) continue;
                    if (b.name == "Head") headBone = b;
                    if (b.name == "Root") rootBone = b;
                }
            }
            if (headBone != null && rootBone != null)
            {
                Vector3 off = headBone.position - rootBone.position;
                Debug.Log("[InfantryVerifySetup] Head offset from Root: (" +
                          off.x.ToString("F3") + "," + off.y.ToString("F3") + "," +
                          off.z.ToString("F3") + ")  -> forward axis is " +
                          (Mathf.Abs(off.z) > Mathf.Abs(off.x) && off.z > 0 ? "+Z (correct)" : "OTHER"));
            }
            else
            {
                Debug.LogError("[InfantryVerifySetup] Head/Root bones not found");
            }

            // Model height
            if (rends.Length > 0)
            {
                var b = new Bounds(inst.transform.position, Vector3.zero);
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Debug.Log("[InfantryVerifySetup] model world bounds height = " +
                          b.size.y.ToString("F3"));
            }

            Object.DestroyImmediate(root);
            Debug.Log("[InfantryVerifySetup] === done ===");
        }
    }
}
