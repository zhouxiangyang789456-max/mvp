using UnityEngine;
using Mvp.Battle.Map.Generation;

namespace Mvp.Battle.Map
{
    public static class TerrainGeometryHelper
    {
        public static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        public static void Prepare(GameObject instance, TerrainPrefabEntry entry, float surfaceY)
        {
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = 2; // Ignore Raycast; visuals never own input.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            if (entry.ScaleMode == TerrainScaleMode.FixedScale)
                instance.transform.localScale = Vector3.one * Mathf.Max(0.001f, entry.FixedScale);
            else if (entry.ScaleMode == TerrainScaleMode.FitFootprint &&
                     TryGetRendererBounds(instance, out var sizeBounds))
                instance.transform.localScale *= Mathf.Max(0.001f, entry.MaxFootprint) /
                    Mathf.Max(0.001f, Mathf.Max(sizeBounds.size.x, sizeBounds.size.z));
            if (entry.AlignBoundsBottom && TryGetRendererBounds(instance, out var bounds))
                instance.transform.position += Vector3.up * (surfaceY + entry.GroundOffset - bounds.min.y);
            else
                instance.transform.position += Vector3.up * entry.GroundOffset;
        }

        public static void PrepareHandAuthored(GameObject instance, float scale,
            HandTileCategory category)
        {
            bool tangible = IsTangible(category);
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = tangible ? 0 : 2;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = tangible;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = tangible;
            }
            instance.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);

            // Imported terrain prefabs are inconsistent: many have visuals but no
            // collider. Add one conservative static box so solid terrain also has a
            // physical body for projectiles and safety checks. Grid traversal remains
            // the authoritative rule for unit movement.
            if (tangible && instance.GetComponentInChildren<Collider>(true) == null &&
                TryGetRendererBounds(instance, out var bounds))
            {
                var box = instance.AddComponent<BoxCollider>();
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                var s = instance.transform.lossyScale;
                box.size = new Vector3(
                    bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)),
                    bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)),
                    bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
            }
        }

        static bool IsTangible(HandTileCategory category)
        {
            switch (category)
            {
                case HandTileCategory.Base:
                case HandTileCategory.Path:
                case HandTileCategory.Ramp:
                case HandTileCategory.Bridge:
                case HandTileCategory.Mountain:
                case HandTileCategory.Building:
                case HandTileCategory.Decoration:
                    return true;
                // Forest is deliberately pass-through: tree trunks must not stop units.
                default:
                    return false;
            }
        }
    }
}
