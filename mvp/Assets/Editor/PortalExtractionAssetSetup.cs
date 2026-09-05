#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    /// <summary>Copies the package's original portal effect into a Resources prefab.</summary>
    public static class PortalExtractionAssetSetup
    {
        const string Source =
            "Assets/Knife/Portal URP/Demo with VFX/Prefabs/PortalTunnel.prefab";
        const string Folder = "Assets/Resources/Battle/Objectives";
        const string Target = Folder + "/ExtractionPortalVisual.prefab";
        const string SessionKey = "Mvp.PortalExtractionAssetSetup.v4";

        [InitializeOnLoadMethod]
        static void Schedule()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += Build;
        }

        [MenuItem("Mvp/Portal/Build Extraction Portal Visual")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) return;
            EnsureFolder(Folder);

            var output = new GameObject("ExtractionPortalVisual");
            try
            {
                var visual = PrefabUtility.InstantiatePrefab(source, output.transform) as GameObject;
                if (visual == null) return;
                visual.name = source.name;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                bool success;
                PrefabUtility.SaveAsPrefabAsset(output, Target, out success);
                if (success)
                    Debug.Log("[PortalSetup] Built visual-only prefab: " + Target);
                else
                    Debug.LogError("[PortalSetup] Failed to save " + Target);
            }
            finally
            {
                Object.DestroyImmediate(output);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
