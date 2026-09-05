using System.Collections.Generic;
using UnityEngine;
using Mvp.Shared;

namespace Mvp.Battle.Commanders
{
    public sealed class CommanderMapMarker : MonoBehaviour
    {
        const float HitRadiusPixels = 38f;
        const float ScreenPadding = 44f;
        const float RefreshInterval = 0.08f;
        const float FollowSharpness = 12f;

        static readonly Vector2[] CandidateOffsets =
        {
            Vector2.zero,
            new Vector2(72f, 0f),
            new Vector2(-72f, 0f),
            new Vector2(92f, -54f),
            new Vector2(-92f, -54f)
        };
        static readonly List<CommanderMapMarker> ActiveMarkers =
            new List<CommanderMapMarker>();

        CommanderGroupRuntime _group;
        SpriteRenderer _portrait;
        SpriteRenderer _ring;
        LineRenderer _leaderLine;
        Vector2 _targetScreen;
        Vector2 _displayScreen;
        float _screenDepth;
        float _nextRefresh;
        bool _hasScreenPosition;
        bool _selected;
        bool _defeated;

        public static CommanderMapMarker Create(CommanderGroupRuntime group, Transform parent)
        {
            var go = new GameObject("CommanderMarker_" + group.CommanderId);
            go.transform.SetParent(parent, false);
            var marker = go.AddComponent<CommanderMapMarker>();
            marker._group = group;

            marker._ring = CreateRenderer(go.transform, "SelectionRing", CreateRingSprite());
            marker._ring.color = new Color(0.949f, 0.788f, 0.298f, 0.95f); // #F2C94C
            marker._ring.sortingOrder = 119;
            marker._ring.transform.localScale = Vector3.one * 1.18f;
            marker._ring.enabled = false;

            var sprite = Resources.Load<Sprite>(group.Definition.MapPortraitAssetId);
            marker._portrait = CreateRenderer(go.transform, "Portrait", sprite);
            marker._portrait.sortingOrder = 120;
            if (sprite == null)
                Debug.LogWarning("[CommanderMarker] Missing sprite: " +
                    group.Definition.MapPortraitAssetId);

            marker.CreateLeaderLine();
            ActiveMarkers.Add(marker);
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

        void CreateLeaderLine()
        {
            _leaderLine = gameObject.AddComponent<LineRenderer>();
            _leaderLine.useWorldSpace = true;
            _leaderLine.positionCount = 2;
            _leaderLine.startWidth = 0.025f;
            _leaderLine.endWidth = 0.025f;
            _leaderLine.material = new Material(Shader.Find("Sprites/Default"));
            _leaderLine.startColor = new Color(0.949f, 0.788f, 0.298f, 0.55f); // #F2C94C
            _leaderLine.endColor = new Color(0.949f, 0.788f, 0.298f, 0.18f);
            _leaderLine.sortingOrder = 118;
        }

        void OnDestroy()
        {
            ActiveMarkers.Remove(this);
            if (_leaderLine != null && _leaderLine.material != null)
                Destroy(_leaderLine.material);
        }

        void LateUpdate()
        {
            if (_group == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (!_hasScreenPosition || Time.unscaledTime >= _nextRefresh)
            {
                RefreshTarget(cam);
                _nextRefresh = Time.unscaledTime + RefreshInterval;
            }
            if (!_hasScreenPosition) return;

            float follow = 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime);
            _displayScreen = Vector2.Lerp(_displayScreen, _targetScreen, follow);
            transform.position = cam.ScreenToWorldPoint(new Vector3(
                _displayScreen.x, _displayScreen.y, _screenDepth));
            transform.rotation = cam.transform.rotation;

            bool hovered = Vector2.Distance(Input.mousePosition, _displayScreen) <=
                HitRadiusPixels;
            bool moving = _group.State == CommanderGroupState.Moving ||
                _group.State == CommanderGroupState.Regrouping ||
                _group.State == CommanderGroupState.Capturing;
            float scale = hovered ? 0.50f :
                _selected ? (moving ? 0.45f : 0.50f) : 0.38f;
            transform.localScale = Vector3.one * scale;

            if (!_defeated && _portrait != null)
            {
                float alpha = hovered || _selected ? 1f : moving ? 0.72f : 0.86f;
                _portrait.color = new Color(1f, 1f, 1f, alpha);
            }
            UpdateLeaderLine();
        }

        void RefreshTarget(Camera cam)
        {
            Vector3 centerWorld = _group.CurrentWorldCenter;
            Vector3 centerScreen = cam.WorldToScreenPoint(centerWorld);
            if (centerScreen.z <= 0f) return;

            float minX = centerScreen.x;
            float maxX = centerScreen.x;
            float maxY = centerScreen.y;
            bool found = false;
            for (int i = 0; i < _group.Members.Count; i++)
            {
                var member = _group.Members[i];
                if (member == null || member.Data == null ||
                    member.Data.State == UnitState.Dead) continue;
                Vector3 point = cam.WorldToScreenPoint(
                    member.transform.position + Vector3.up * 1.05f);
                if (point.z <= 0f) continue;
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
                found = true;
            }

            float radius = _selected ? 34f : 28f;
            Vector2 basePoint = new Vector2(
                found ? (minX + maxX) * 0.5f : centerScreen.x,
                maxY + radius + 18f);
            float stagger = (_group.RosterIndex % 3) * 10f;
            basePoint.x += _group.RosterIndex % 2 == 0 ? stagger : -stagger;

            Vector2 best = ClampToScreen(basePoint);
            int bestPenalty = int.MaxValue;
            for (int i = 0; i < CandidateOffsets.Length; i++)
            {
                Vector2 candidate = ClampToScreen(basePoint + CandidateOffsets[i]);
                int penalty = OverlapPenalty(cam, candidate);
                if (penalty >= bestPenalty) continue;
                best = candidate;
                bestPenalty = penalty;
                if (penalty == 0) break;
            }

            _targetScreen = best;
            _screenDepth = centerScreen.z;
            if (!_hasScreenPosition)
            {
                _displayScreen = best;
                _hasScreenPosition = true;
            }
        }

        int OverlapPenalty(Camera cam, Vector2 candidate)
        {
            int penalty = 0;
            const float unitDistanceSq = 76f * 76f;
            for (int i = 0; i < _group.Members.Count; i++)
            {
                var member = _group.Members[i];
                if (member == null || member.Data == null ||
                    member.Data.State == UnitState.Dead) continue;
                Vector3 screen = cam.WorldToScreenPoint(
                    member.transform.position + Vector3.up * 0.55f);
                if (screen.z > 0f &&
                    (candidate - new Vector2(screen.x, screen.y)).sqrMagnitude <
                    unitDistanceSq)
                    penalty += 10;
            }

            const float markerDistanceSq = 58f * 58f;
            for (int i = 0; i < ActiveMarkers.Count; i++)
            {
                var other = ActiveMarkers[i];
                if (other == null || other == this || !other._hasScreenPosition) continue;
                if ((candidate - other._targetScreen).sqrMagnitude < markerDistanceSq)
                    penalty += 20;
            }
            return penalty;
        }

        static Vector2 ClampToScreen(Vector2 point)
        {
            point.x = Mathf.Clamp(point.x, ScreenPadding, Screen.width - ScreenPadding);
            point.y = Mathf.Clamp(point.y, ScreenPadding, Screen.height - ScreenPadding);
            return point;
        }

        void UpdateLeaderLine()
        {
            if (_leaderLine == null || _defeated) return;
            Vector3 center = _group.CurrentWorldCenter + Vector3.up * 0.35f;
            _leaderLine.SetPosition(0, center);
            _leaderLine.SetPosition(1, transform.position);
            _leaderLine.enabled = Vector3.Distance(center, transform.position) > 0.8f;
        }

        public bool HitTest(Vector2 screenPosition)
        {
            return gameObject.activeInHierarchy && _hasScreenPosition &&
                Vector2.Distance(screenPosition, _displayScreen) <= HitRadiusPixels;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_ring != null) _ring.enabled = selected;
        }

        public void SetDefeated()
        {
            _defeated = true;
            if (_ring != null) _ring.enabled = false;
            if (_portrait != null)
                _portrait.color = new Color(0.38f, 0.38f, 0.38f, 0.75f);
            if (_leaderLine != null) _leaderLine.enabled = false;
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
                pixels[y * size + x] = new Color32(242, 201, 76, a); // #F2C94C
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                Vector2.one * 0.5f, 100f);
        }
    }
}
