using System.Collections;
using UnityEngine;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Applies the one-cell building rule before placement and attaches final art after spawn.
    /// Kept runtime-driven so generated maps and the hand-authored test map share one path.
    /// </summary>
    public sealed class BuildingArtInstaller : MonoBehaviour
    {
        BuildingRegistry _registry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOneCellDefinitions()
        {
            var house = BuildingCatalog.Get(BuildingType.House);
            var armory = BuildingCatalog.Get(BuildingType.Armory);
            if (house != null) house.Footprint = Vector2Int.one;
            if (armory != null) armory.Footprint = Vector2Int.one;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindObjectOfType<BuildingArtInstaller>() != null) return;
            new GameObject("BuildingArtInstaller").AddComponent<BuildingArtInstaller>();
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

            _registry.BuildingSpawned += OnBuildingSpawned;
            AttachExisting();
        }

        void OnDestroy()
        {
            if (_registry != null) _registry.BuildingSpawned -= OnBuildingSpawned;
        }

        void OnBuildingSpawned(BuildingRuntime building)
        {
            AttachExisting();
        }

        static void AttachExisting()
        {
            var views = FindObjectsOfType<BuildingView>();
            for (int i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view != null && view.GetComponent<BuildingArtPresenter>() == null)
                    view.gameObject.AddComponent<BuildingArtPresenter>();
            }
        }
    }

    /// <summary>Replaces the generated gray footprint with the ownership-specific Sprite.</summary>
    public sealed class BuildingArtPresenter : MonoBehaviour
    {
        const int ArtSortingOrder = 120;
        const float HouseArtScale = 1.25f;
        const float ArmoryArtScale = 1.28f;

        BuildingView _view;
        BuildingRuntime _building;
        Transform _artRoot;
        SpriteRenderer _renderer;

        void Awake()
        {
            _view = GetComponent<BuildingView>();
            _building = _view != null ? _view.Building : null;
            HidePlaceholderVisuals();
            CreateArt();

            var registry = BuildingRegistry.Instance;
            if (registry != null) registry.BuildingOwnershipChanged += OnOwnershipChanged;
        }

        void OnDestroy()
        {
            var registry = BuildingRegistry.Instance;
            if (registry != null) registry.BuildingOwnershipChanged -= OnOwnershipChanged;
        }

        void LateUpdate()
        {
            // Remove the temporary armory badge if its bootstrap attached later this frame.
            var oldBadge = GetComponent<ArmoryScreenMarker>();
            if (oldBadge != null) Destroy(oldBadge);

            if (_artRoot == null) return;
            var cam = Camera.main;
            if (cam != null) _artRoot.rotation = cam.transform.rotation;
        }

        void HidePlaceholderVisuals()
        {
            var footprint = transform.Find("Footprint");
            if (footprint != null) footprint.gameObject.SetActive(false);
            var marker = transform.Find("FloatingMarker");
            if (marker != null) marker.gameObject.SetActive(false);
        }

        void CreateArt()
        {
            if (_building == null) return;
            var go = new GameObject("FinalBuildingArt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * ResolveArtScale();
            _artRoot = go.transform;
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = ArtSortingOrder;
            RefreshSprite();
        }

        void OnOwnershipChanged(BuildingRuntime building)
        {
            if (_building == building) RefreshSprite();
        }

        void RefreshSprite()
        {
            if (_renderer == null || _building == null) return;
            bool occupied = _building.Owner != BuildingOwner.Neutral;
            string path;
            if (_building.Type == BuildingType.Armory)
                path = occupied ? "Battle/Buildings/armory_occupied_v2" :
                    "Battle/Buildings/armory_unoccupied_v2";
            else
                path = occupied ? "Battle/Buildings/house_occupied_v2" :
                    "Battle/Buildings/house_unoccupied_v2";

            _renderer.sprite = Resources.Load<Sprite>(path);
            if (_renderer.sprite == null)
            {
                string fallbackPath = path.Substring(0, path.Length - 3);
                _renderer.sprite = Resources.Load<Sprite>(fallbackPath);
                Debug.LogWarning("Building v2 sprite is missing; using legacy sprite: " +
                    fallbackPath);
            }
            _renderer.color = Color.white;
            if (_renderer.sprite == null)
                Debug.LogError("Building sprite is missing from Resources: " + path);
        }
        float ResolveArtScale()
        {
            return _building != null && _building.Type == BuildingType.Armory
                ? ArmoryArtScale
                : HouseArtScale;
        }
    }
}