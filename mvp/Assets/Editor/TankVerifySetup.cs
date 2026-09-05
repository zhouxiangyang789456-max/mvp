using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>
    /// Batch-mode entry point used to import + verify the tank FBX:
    ///   1. Runs TankAssetSetup (textures, import config + prefab build).
    ///   2. Verifies mesh renderers, materials/_MainTex, and world bounds/orientation
    ///      (tank should be ~0.98 long, ~0.44 tall, sitting on the ground, front=+Z).
    /// Entry: Mvp/Verify Tank Setup  (or Unity -executeMethod).
    /// </summary>
    public static class TankVerifySetup
    {
        const string PrefabPath = "Assets/Resources/Battle/Units/Tank.prefab";

        [MenuItem("Mvp/Verify Tank Setup")]
        public static void RunMenu()
        {
            Run();
        }

        public static void Run()
        {
            Debug.Log("[TankVerifySetup] === start ===");

            // 1) Run the standard setup (textures, importer, prefab)
            TankAssetSetup.RunSetup();

            // 3) Instantiate prefab and inspect
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[TankVerifySetup] prefab not found at " + PrefabPath);
                return;
            }

            var root = new GameObject("_TankVerifyTemp");
            var inst = (GameObject)Object.Instantiate(prefab, root.transform);
            inst.name = "TankVerify";

            var rends = inst.GetComponentsInChildren<MeshRenderer>(true);
            Debug.Log("[TankVerifySetup] mesh renderers: " + rends.Length);
            int withTex = 0;
            foreach (var r in rends)
            {
                string tex = "none";
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_MainTex")
                    && r.sharedMaterial.GetTexture("_MainTex") != null)
                {
                    tex = r.sharedMaterial.GetTexture("_MainTex").name;
                    withTex++;
                }
                Debug.Log("[TankVerifySetup]   " + r.gameObject.name +
                          " mats=" + r.sharedMaterials.Length + " _MainTex=" + tex);
            }
            Debug.Log("[TankVerifySetup] renderers with _MainTex: " + withTex + "/" + rends.Length);

            // 4) Bounds + orientation
            var b = new Bounds(inst.transform.position, Vector3.zero);
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Debug.Log("[TankVerifySetup] world bounds center=(" +
                      b.center.x.ToString("F3") + "," + b.center.y.ToString("F3") + "," +
                      b.center.z.ToString("F3") + ") size=(" +
                      b.size.x.ToString("F3") + "," + b.size.y.ToString("F3") + "," +
                      b.size.z.ToString("F3") + ")");

            // The barrel (front) should be the part extending toward +Z.
            // Report the part with the largest +Z extent so we can confirm front=+Z.
            Transform frontPart = null;
            float maxZ = float.MinValue;
            foreach (Transform child in inst.transform)
            {
                foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.bounds.max.z > maxZ) { maxZ = r.bounds.max.z; frontPart = r.transform; }
                }
            }
            Debug.Log("[TankVerifySetup] most +Z part = " +
                      (frontPart != null ? frontPart.name : "(null)") +
                      " maxZ=" + maxZ.ToString("F3") +
                      " (expect a barrel-like part near +0.4..0.49 -> front=+Z)");

            Object.DestroyImmediate(root);
            Debug.Log("[TankVerifySetup] === done ===");
        }
    }
}
