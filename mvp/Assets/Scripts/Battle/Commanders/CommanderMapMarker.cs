using UnityEngine;

namespace Mvp.Battle.Commanders
{
    public sealed class CommanderMapMarker : MonoBehaviour
    {
        const float HitRadiusPixels = 38f;
        CommanderGroupRuntime _group;
        SpriteRenderer _portrait;
        SpriteRenderer _ring;

        public static CommanderMapMarker Create(CommanderGroupRuntime group, Transform parent)
        {
            var go = new GameObject("CommanderMarker_" + group.CommanderId);
            go.transform.SetParent(parent, false);
            var marker = go.AddComponent<CommanderMapMarker>();
            marker._group = group;

            marker._ring = CreateRenderer(go.transform, "SelectionRing", CreateRingSprite());
            marker._ring.color = new Color(1f, 0.78f, 0.18f, 0.95f);
            marker._ring.sortingOrder = 119;
            marker._ring.transform.localScale = Vector3.one * 1.18f;
            marker._ring.enabled = false;

            var sprite = Resources.Load<Sprite>(group.Definition.MapPortraitAssetId);
            marker._portrait = CreateRenderer(go.transform, "Portrait", sprite);
            marker._portrait.sortingOrder = 120;
            if (sprite == null)
                Debug.LogWarning("[CommanderMarker] Missing sprite: " + group.Definition.MapPortraitAssetId);
            return marker;
        }

        static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        void LateUpdate()
        {
            if (_group == null) return;
            Vector3 center = _group.CurrentWorldCenter;
            // This billboard is the commander unit: it stands just above the group
            // center, remains separate from troop occupancy, and owns group selection.
            transform.position = center + Vector3.up * 1.15f;
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            transform.localScale = Vector3.one * 0.50f;
        }

        public bool HitTest(Vector2 screenPosition)
        {
            var cam = Camera.main;
            if (cam == null || !gameObject.activeInHierarchy) return false;
            Vector3 point = cam.WorldToScreenPoint(transform.position);
            return point.z > 0f && Vector2.Distance(screenPosition, point) <= HitRadiusPixels;
        }

        public void SetSelected(bool selected)
        {
            if (_ring != null) _ring.enabled = selected;
            if (_portrait != null) _portrait.color = Color.white;
        }

        public void SetDefeated()
        {
            if (_ring != null) _ring.enabled = false;
            if (_portrait != null) _portrait.color = new Color(0.38f, 0.38f, 0.38f, 0.75f);
        }

        static Sprite CreateRingSprite()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CommanderSelectionRing";
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                byte a = d >= 42f && d <= 47f ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 210, 70, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }
    }
}
