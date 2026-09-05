using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>
    /// Batch-mode entry point used to import + verify the three new static units:
    ///   1. Runs NewUnitAssetSetup (textures, import config + prefab build).
    ///   2. For each prefab verifies mesh renderers, materials/_MainTex, and
    ///      world bounds (sizes / ground alignment / front=+Z).
    /// Entry: Mvp/Verify New Unit Setup.
    /// </summary>
    public static class NewUnitVerifySetup
    {
        static readonly string[] Units = { "Scout", "ScoutCar", "RocketArtillery" };
        const string PrefabFolder = "Assets/Resources/Battle/Units";

        [MenuItem("Mvp/Verify New Unit Setup")]
        public static void RunMenu()
        {
            Run();
        }

        public static void Run()
        {
            Debug.Log("[NewUnitVerifySetup] === start ===");

            // 1) Run the standard setup (textures, importer, prefab)
            NewUnitAssetSetup.RunSetup();

            foreach (var name in Units)
            {
                VerifyOne(name);
            }

            Debug.Log("[NewUnitVerifySetup] === done ===");
        }

        static void VerifyOne(string name)
        {
            string path = PrefabFolder + "/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[NewUnitVerifySetup] prefab not found at " + path);
                return;
            }

            var root = new GameObject("_" + name + "VerifyTemp");
            var inst = (GameObject)Object.Instantiate(prefab, root.transform);
            inst.name = name + "Verify";

            var rends = inst.GetComponentsInChildren<MeshRenderer>(true);
            int withTex = 0;
            foreach (var r in rends)
            {
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_MainTex")
                    && r.sharedMaterial.GetTexture("_MainTex") != null)
                    withTex++;
            }
            Debug.Log("[NewUnitVerifySetup] " + name +
                      " renderers=" + rends.Length + " with _MainTex=" + withTex +
                      "/" + rends.Length);

            // Bounds + orientation
            var b = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Debug.Log("[NewUnitVerifySetup] " + name + " bounds center=(" +
                      b.center.x.ToString("F3") + "," + b.center.y.ToString("F3") + "," +
                      b.center.z.ToString("F3") + ") size=(" +
                      b.size.x.ToString("F3") + "," + b.size.y.ToString("F3") + "," +
                      b.size.z.ToString("F3") + ")  (expect y-min ~ 0 -> on ground)");

            // Confirm which part extends most toward +Z (front).
            Transform frontPart = null;
            float maxZ = float.MinValue;
            foreach (Transform child in inst.transform)
            {
                foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.bounds.max.z > maxZ) { maxZ = r.bounds.max.z; frontPart = r.transform; }
                }
            }
            Debug.Log("[NewUnitVerifySetup] " + name + " most +Z part = " +
                      (frontPart != null ? frontPart.name : "(null)") +
                      " maxZ=" + maxZ.ToString("F3") + " (front=+Z)");

            Object.DestroyImmediate(root);
        }
    }
}
