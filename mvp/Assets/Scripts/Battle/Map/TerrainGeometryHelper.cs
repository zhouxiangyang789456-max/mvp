using UnityEngine;

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

        public static void PrepareHandAuthored(GameObject instance, float scale)
        {
            foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = 2;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            instance.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        }
    }
}
