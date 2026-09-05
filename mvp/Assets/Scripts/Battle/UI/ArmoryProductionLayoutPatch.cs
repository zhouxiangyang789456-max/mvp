using UnityEngine;

namespace Mvp.Battle.UI
{
    /// <summary>Applies the fixed left-column anchors after the runtime V2 hierarchy is built.</summary>
    public sealed class ArmoryProductionLayoutPatch : MonoBehaviour
    {
        int _patchedPanelId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindObjectOfType<ArmoryProductionLayoutPatch>() != null) return;
            new GameObject("ArmoryProductionLayoutPatch").AddComponent<ArmoryProductionLayoutPatch>();
        }

        void LateUpdate()
        {
            var panels = Resources.FindObjectsOfTypeAll<ArmoryProductionPanelV2>();
            if (panels == null || panels.Length == 0) return;
            var panel = panels[0];
            if (panel == null || panel.GetInstanceID() == _patchedPanelId) return;
            var left = panel.transform.Find("Frame/UnitListPanel") as RectTransform;
            if (left == null) return;

            left.anchorMin = Vector2.zero;
            left.anchorMax = Vector2.zero;
            left.pivot = new Vector2(0.5f, 0.5f);
            left.anchoredPosition = new Vector2(286f, 365f);
            left.sizeDelta = new Vector2(548f, 690f);
            _patchedPanelId = panel.GetInstanceID();
        }
    }
}
