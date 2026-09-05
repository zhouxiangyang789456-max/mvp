using TMPro;
using UnityEngine;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Runtime visual for a building footprint. The footprint tint shows ownership and a
    /// raised billboard label identifies the building even while deployment overlays are visible.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        public static readonly Color PlayerColor = new Color(0.184f, 0.624f, 0.910f); // #2F9FE8
        public static readonly Color EnemyColor = new Color(0.851f, 0.294f, 0.271f);  // #D94B45
        public static readonly Color NeutralColor = new Color(0.6f, 0.6f, 0.6f);

        const int SortingOrder = 60;

        SpriteRenderer _sprite;
        SpriteRenderer _markerBg;
        Transform _markerRoot;
        TextMeshPro _label;
        BuildingRuntime _building;

        public BuildingRuntime Building { get { return _building; } }

        public void Bind(BuildingRuntime building, Transform parent)
        {
            _building = building;
            if (parent != null) transform.SetParent(parent, false);
            transform.position = FootprintCenterWorld(building);

            var quad = new GameObject("Footprint");
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            quad.transform.localScale = new Vector3(building.Footprint.x, building.Footprint.y, 1f);
            _sprite = quad.AddComponent<SpriteRenderer>();
            _sprite.sprite = SharedSprites.White;
            _sprite.sortingOrder = SortingOrder;

            CreateMapMarker(building);
            ApplyColor();

            var registry = BuildingRegistry.Instance;
            if (registry != null)
            {
                registry.BuildingOwnershipChanged += OnOwnershipChanged;
                registry.BuildingCaptureProgressChanged += OnProgressChanged;
                registry.BuildingContestedChanged += OnContestedChanged;
            }
        }

        public void SetRuntime(BuildingRuntime building)
        {
            _building = building;
            ApplyColor();
        }

        void LateUpdate()
        {
            if (_markerRoot == null) return;
            var cam = Camera.main;
            if (cam != null) _markerRoot.rotation = cam.transform.rotation;
        }

        void OnOwnershipChanged(BuildingRuntime building)
        {
            if (_building == building) ApplyColor();
        }

        void OnProgressChanged(BuildingRuntime building)
        {
            if (_building == building) ApplyColor();
        }

        void OnContestedChanged(BuildingRuntime building)
        {
            if (_building == building) ApplyColor();
        }

        void OnDestroy()
        {
            var registry = BuildingRegistry.Instance;
            if (registry != null)
            {
                registry.BuildingOwnershipChanged -= OnOwnershipChanged;
                registry.BuildingCaptureProgressChanged -= OnProgressChanged;
                registry.BuildingContestedChanged -= OnContestedChanged;
            }
        }

        void ApplyColor()
        {
            var color = ResolveColor();
            if (_sprite != null) _sprite.color = color;
            if (_markerBg != null)
            {
                var bg = _building != null && _building.Type == BuildingType.Armory
                    ? new Color(0.16f, 0.09f, 0.02f, 0.96f)
                    : new Color(0.05f, 0.13f, 0.18f, 0.94f);
                _markerBg.color = bg;
            }
            if (_label != null)
            {
                _label.color = _building != null && _building.Type == BuildingType.Armory
                    ? new Color(1f, 0.88f, 0.36f, 1f)
                    : new Color(0.86f, 0.95f, 1f, 1f);
                _label.outlineColor = color;
            }
        }

        void CreateMapMarker(BuildingRuntime building)
        {
            var markerGo = new GameObject("FloatingMarker");
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            _markerRoot = markerGo.transform;

            var bgGo = new GameObject("MarkerBackground");
            bgGo.transform.SetParent(_markerRoot, false);
            bgGo.transform.localPosition = Vector3.zero;
            bgGo.transform.localScale = new Vector3(2.25f, 0.72f, 1f);
            _markerBg = bgGo.AddComponent<SpriteRenderer>();
            _markerBg.sprite = SharedSprites.White;
            _markerBg.sortingOrder = SortingOrder + 20;

            var labelGo = new GameObject("NameLabel");
            labelGo.transform.SetParent(_markerRoot, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            _label = labelGo.AddComponent<TextMeshPro>();
            _label.text = building.Type == BuildingType.Armory ? "兵工厂\n生产" : "楼房\n产金";
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 2.1f;
            _label.fontStyle = FontStyles.Bold;
            _label.enableWordWrapping = false;
            _label.outlineWidth = 0.20f;
            _label.sortingOrder = SortingOrder + 21;
        }

        Color ResolveColor()
        {
            if (_building == null) return NeutralColor;
            if (_building.Owner == BuildingOwner.Player) return PlayerColor;
            if (_building.Owner == BuildingOwner.Enemy) return EnemyColor;

            float required = _building.CaptureRequiredSeconds;
            if (required <= 0f) return NeutralColor;
            float player = _building.CaptureProgressPlayer;
            float enemy = _building.CaptureProgressEnemy;
            float max = Mathf.Max(player, enemy);
            if (max <= 0f) return NeutralColor;
            float frac = Mathf.Clamp01(max / required);
            Color target = player >= enemy ? PlayerColor : EnemyColor;
            return Color.Lerp(NeutralColor, target, frac * 0.85f);
        }

        static Vector3 FootprintCenterWorld(BuildingRuntime building)
        {
            return new Vector3(
                building.AnchorCell.x + (building.Footprint.x - 1) * 0.5f,
                0f,
                building.AnchorCell.y + (building.Footprint.y - 1) * 0.5f);
        }
    }
}
