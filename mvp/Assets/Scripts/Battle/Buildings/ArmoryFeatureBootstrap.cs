using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>Attaches the armory interaction and screen markers after BattleCore starts.</summary>
    public sealed class ArmoryFeatureBootstrap : MonoBehaviour
    {
        BuildingRegistry _registry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindObjectOfType<ArmoryFeatureBootstrap>() != null) return;
            new GameObject("ArmoryFeatureBootstrap").AddComponent<ArmoryFeatureBootstrap>();
        }

        IEnumerator Start()
        {
            for (int i = 0; i < 300 && BuildingRegistry.Instance == null; i++)
                yield return null;
            _registry = BuildingRegistry.Instance;
            if (_registry == null)
            {
                Destroy(gameObject);
                yield break;
            }

            if (FindObjectOfType<ArmoryProximityController>() == null)
                _registry.gameObject.AddComponent<ArmoryProximityController>();

            _registry.BuildingSpawned += OnBuildingSpawned;
            AttachExistingMarkers();
        }

        void OnDestroy()
        {
            if (_registry != null) _registry.BuildingSpawned -= OnBuildingSpawned;
        }

        void OnBuildingSpawned(BuildingRuntime building)
        {
            if (building == null || building.Type != BuildingType.Armory) return;
            AttachExistingMarkers();
        }

        static void AttachExistingMarkers()
        {
            var views = FindObjectsOfType<BuildingView>();
            for (int i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || view.Building == null ||
                    view.Building.Type != BuildingType.Armory) continue;
                if (view.GetComponent<ArmoryScreenMarker>() == null)
                    view.gameObject.AddComponent<ArmoryScreenMarker>();
            }
        }
    }

    /// <summary>Screen-space badge that remains visible above deployment and range overlays.</summary>
    public sealed class ArmoryScreenMarker : MonoBehaviour
    {
        BuildingView _view;
        Canvas _canvas;
        RectTransform _marker;
        TextMeshProUGUI _label;

        void Awake()
        {
            _view = GetComponent<BuildingView>();
        }

        void OnEnable()
        {
            var registry = BuildingRegistry.Instance;
            if (registry != null) registry.BuildingOwnershipChanged += OnOwnershipChanged;
        }

        void OnDisable()
        {
            var registry = BuildingRegistry.Instance;
            if (registry != null) registry.BuildingOwnershipChanged -= OnOwnershipChanged;
        }

        void OnDestroy()
        {
            if (_marker != null) Destroy(_marker.gameObject);
        }

        void LateUpdate()
        {
            EnsureMarker();
            UpdatePosition();
        }

        void EnsureMarker()
        {
            if (_marker != null || _view == null || _view.Building == null) return;
            _canvas = FindBestCanvas();
            if (_canvas == null) return;

            var markerGo = new GameObject("ArmoryScreenBadge_" + _view.Building.InstanceId,
                typeof(RectTransform), typeof(Image), typeof(Outline));
            markerGo.transform.SetParent(_canvas.transform, false);
            _marker = markerGo.GetComponent<RectTransform>();
            _marker.anchorMin = new Vector2(0.5f, 0.5f);
            _marker.anchorMax = new Vector2(0.5f, 0.5f);
            _marker.pivot = new Vector2(0.5f, 0f);
            _marker.sizeDelta = new Vector2(172f, 54f);

            var background = markerGo.GetComponent<Image>();
            background.color = new Color(0.12f, 0.07f, 0.02f, 0.96f);
            background.raycastTarget = false;
            var outline = markerGo.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 0.68f, 0.12f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(markerGo.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 22f;
            _label.fontStyle = FontStyles.Bold;
            _label.enableWordWrapping = false;
            _label.raycastTarget = false;
            _label.color = new Color(1f, 0.88f, 0.36f, 1f);
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);
            RefreshLabel();
        }

        void UpdatePosition()
        {
            if (_marker == null || _canvas == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.45f);
            bool visible = screen.z > 0f;
            if (_marker.gameObject.activeSelf != visible) _marker.gameObject.SetActive(visible);
            if (!visible) return;

            var canvasRect = _canvas.transform as RectTransform;
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _canvas.worldCamera;
            Vector2 local;
            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, uiCamera, out local))
                _marker.anchoredPosition = local;
        }

        void OnOwnershipChanged(BuildingRuntime building)
        {
            if (_view != null && _view.Building == building) RefreshLabel();
        }

        void RefreshLabel()
        {
            if (_label == null || _view == null || _view.Building == null) return;
            var owner = _view.Building.Owner;
            string prefix = owner == BuildingOwner.Player ? "己方" :
                owner == BuildingOwner.Enemy ? "敌方" : "中立";
            _label.text = prefix + "兵工厂  ·  生产";
        }

        static Canvas FindBestCanvas()
        {
            Canvas best = null;
            var canvases = FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                var candidate = canvases[i];
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidate.renderMode == RenderMode.WorldSpace) continue;
                if (best == null || candidate.sortingOrder >= best.sortingOrder) best = candidate;
            }
            return best;
        }
    }
}
