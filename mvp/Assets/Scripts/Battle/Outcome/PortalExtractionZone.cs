using Mvp.Battle.Map;
using UnityEngine;

namespace Mvp.Battle.Outcome
{
    /// <summary>Visual facade and NxM logical footprint for the extraction portal.</summary>
    public sealed class PortalExtractionZone : MonoBehaviour
    {
        public Vector2Int Anchor { get; private set; }
        public int ZoneWidth { get; private set; }
        public int ZoneHeight { get; private set; }
        GameObject _packageVisual;
        bool _open;

        public static PortalExtractionZone Create(Transform parent,
            BattleGridController grid, Vector2Int anchor, int width = 2, int height = 2)
        {
            int w = Mathf.Max(1, width);
            int h = Mathf.Max(1, height);
            var go = new GameObject("ExtractionPortal");
            go.transform.SetParent(parent, false);
            var center = grid.GridToWorld(anchor) + new Vector3(w * 0.5f, 0f, h * 0.5f);
            center.y = TerrainCatalog.GetElevation(grid.GetTerrain(anchor));
            go.transform.position = center;
            var zone = go.AddComponent<PortalExtractionZone>();
            zone.Anchor = anchor;
            zone.ZoneWidth = w;
            zone.ZoneHeight = h;
            zone.BuildVisuals();
            return zone;
        }

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= Anchor.x && cell.x < Anchor.x + ZoneWidth &&
                   cell.y >= Anchor.y && cell.y < Anchor.y + ZoneHeight;
        }

        void BuildVisuals()
        {
            var packagePrefab = Resources.Load<GameObject>(
                "Battle/Objectives/ExtractionPortalVisual");
            if (packagePrefab != null)
            {
                _packageVisual = Instantiate(packagePrefab, transform, false);
                _packageVisual.name = "PortalEffectURP_Visual";
                _packageVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                _packageVisual.transform.localRotation = Quaternion.identity;
                _packageVisual.transform.localScale = Vector3.one;

                // The extraction rule uses the logical grid footprint. Package physics and
                // cameras must not interfere with battle input, while its original renderers,
                // particles, animators and effect behaviours remain intact.
                foreach (var packageCamera in _packageVisual.GetComponentsInChildren<Camera>(true))
                    packageCamera.gameObject.SetActive(false);
                foreach (var packageCollider in _packageVisual.GetComponentsInChildren<Collider>(true))
                    packageCollider.enabled = false;
                foreach (var body in _packageVisual.GetComponentsInChildren<Rigidbody>(true))
                    body.isKinematic = true;

                HideRoundPortalMeshes(_packageVisual.transform);
            }
            else
                Debug.LogError("[Extraction] Missing original portal visual prefab in Resources.");
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_packageVisual != null) _packageVisual.SetActive(open);
        }

        public void SetCompleted(bool victory)
        {
            _open = false;
            if (_packageVisual != null) _packageVisual.SetActive(victory);
        }

        public void PlayEntryPulse(Vector3 worldPosition)
        {
            // The package owns its visual animation; logical entry remains grid-driven.
        }

        static void HideRoundPortalMeshes(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string objectName = renderer.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("portal_mesh") || objectName.Contains("portal border") ||
                    objectName.Contains("portal_border"))
                    renderer.enabled = false;
            }
        }

    }
}
