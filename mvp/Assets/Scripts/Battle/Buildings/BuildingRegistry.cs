using System;
using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Generation;
using Mvp.Shared;

namespace Mvp.Battle.Buildings
{
    /// <summary>
    /// Owns all buildings on the battle map (阶段B). Created by <see cref="BattleCore.Ensure"/>.
    /// Buildings are pure runtime data (<see cref="BuildingRuntime"/>) placed over their
    /// footprint cells (blocked + occupied). Default hand-authored buildings are placed for
    /// the TestMap so a dev battle always shows capturable structures; generated maps can
    /// supply <see cref="BuildingSpawnData"/>.
    /// </summary>
    public sealed class BuildingRegistry : MonoBehaviour
    {
        public static BuildingRegistry Instance { get; private set; }

        public event Action<BuildingRuntime> BuildingSpawned;
        public event Action<BuildingRuntime> BuildingOwnershipChanged;
        public event Action<BuildingRuntime> BuildingCaptureProgressChanged;
        public event Action<BuildingRuntime> BuildingContestedChanged;

        readonly Dictionary<int, BuildingRuntime> _byInstanceId =
            new Dictionary<int, BuildingRuntime>();
        readonly Dictionary<Vector2Int, BuildingRuntime> _byCell =
            new Dictionary<Vector2Int, BuildingRuntime>();
        readonly List<BuildingRuntime> _all = new List<BuildingRuntime>();
        int _nextInstanceId;
        bool _placed;

        /// <summary>All registered buildings in insertion order.</summary>
        public IReadOnlyList<BuildingRuntime> AllBuildings { get { return _all; } }

        public bool HasPlacedBuildings { get { return _placed; } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Runs after all Awakes (grid exists); the spawner also calls this explicitly
            // so buildings are placed before any unit spawns regardless of Start order.
            EnsureDefaultBuildingsPlaced();
        }

        void OnDestroy()
        {
            var grid = BattleGridController.Instance;
            if (grid != null)
            {
                for (int i = 0; i < _all.Count; i++)
                {
                    var b = _all[i];
                    foreach (var cell in FootprintCells(b.AnchorCell, b.Footprint))
                        grid.SetBlocked(cell, false);
                }
            }
            _all.Clear();
            _byInstanceId.Clear();
            _byCell.Clear();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Idempotently places buildings for the current map: generated-map spawn data when
        /// present, otherwise the default hand-authored TestMap layout (阶段B fallback).
        /// </summary>
        public void EnsureDefaultBuildingsPlaced()
        {
            if (_placed) return;
            var grid = BattleGridController.Instance;
            if (grid == null) return; // not ready yet; retried in Start

            _placed = true;
            var generated = BattleMapContext.LastGeneratedData;
            if (generated != null && generated.BuildingSpawnData != null &&
                generated.BuildingSpawnData.Count > 0)
            {
                for (int i = 0; i < generated.BuildingSpawnData.Count; i++)
                {
                    var spawn = generated.BuildingSpawnData[i];
                    if (spawn == null || string.IsNullOrEmpty(spawn.DefinitionId)) continue;
                    RegisterBuilding(spawn.DefinitionId, spawn.AnchorCell, spawn.InitialOwner);
                }
                return;
            }

            PlaceDefaultTestMapBuildings();
        }

        // §18.2 defaults on the hand-authored TestMap (Plain 2x2 areas).
        void PlaceDefaultTestMapBuildings()
        {
            RegisterBuilding("building_house", new Vector2Int(5, 7));
            RegisterBuilding("building_house", new Vector2Int(9, 7));
            RegisterBuilding("building_house", new Vector2Int(7, 6));
            RegisterBuilding("building_armory", new Vector2Int(6, 9));
            RegisterBuilding("building_armory", new Vector2Int(9, 9));
        }

        /// <summary>
        /// Validates and registers a building at <paramref name="anchorCell"/> (bottom-left of
        /// footprint). Returns null when the footprint is out of bounds or collides with
        /// terrain/building/unit occupancy.
        /// </summary>
        public BuildingRuntime RegisterBuilding(string definitionId, Vector2Int anchorCell,
            BuildingOwner initialOwner = BuildingOwner.Neutral)
        {
            var definition = BuildingCatalog.Get(definitionId);
            var grid = BattleGridController.Instance;
            if (definition == null || grid == null) return null;

            var footprint = definition.Footprint;
            if (footprint.x <= 0 || footprint.y <= 0) return null;
            for (int i = 0; i < _all.Count; i++)
                if (Overlaps(_all[i], anchorCell, footprint)) return null;

            // Runtime final line of defense (建筑平原约束): every footprint cell must be
            // plain terrain. This is the hard rule so hand-authored maps, old data and any
            // future entry point cannot place a building on forest/beach/road/etc.
            foreach (var cell in FootprintCells(anchorCell, footprint))
            {
                if (!grid.InBounds(cell))
                {
                    Debug.LogWarning("[BuildingRegistry] reject " + definition.Id + " anchor=" + anchorCell
                        + " cell=" + cell + " out of bounds");
                    return null;
                }
                if (!BuildingPlacementRules.CellAllowed(grid.GetTerrain(cell)))
                {
                    Debug.LogWarning("[BuildingRegistry] reject " + definition.Id + " anchor=" + anchorCell
                        + " cell=" + cell + " terrain=" + grid.GetTerrain(cell) + " (must be Plain)");
                    return null;
                }
                if (!grid.IsWalkable(cell) || grid.IsOccupied(cell))
                {
                    Debug.LogWarning("[BuildingRegistry] reject " + definition.Id + " anchor=" + anchorCell
                        + " cell=" + cell + " occupied or unwalkable");
                    return null;
                }
            }

            var runtime = new BuildingRuntime
            {
                DefinitionId = definition.Id,
                Definition = definition,
                Type = definition.Type,
                Footprint = footprint,
                AnchorCell = anchorCell,
                Owner = initialOwner
            };
            runtime.RefreshOperational();

            int id = ++_nextInstanceId;
            runtime.InstanceId = id;
            _byInstanceId[id] = runtime;
            _all.Add(runtime);
            foreach (var cell in FootprintCells(anchorCell, footprint))
            {
                _byCell[cell] = runtime;
                grid.SetBlocked(cell, true);
            }

            var go = new GameObject("Building_" + definition.DisplayName);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<BuildingView>();
            view.Bind(runtime, transform);

            if (BuildingSpawned != null) BuildingSpawned(runtime);
            return runtime;
        }

        public BuildingRuntime GetAt(Vector2Int cell)
        {
            BuildingRuntime b;
            return _byCell.TryGetValue(cell, out b) ? b : null;
        }

        public BuildingRuntime GetByInstanceId(int instanceId)
        {
            BuildingRuntime b;
            return _byInstanceId.TryGetValue(instanceId, out b) ? b : null;
        }

        /// <summary>First building whose footprint overlaps the given rect.</summary>
        public BuildingRuntime GetInRect(Vector2Int minCell, Vector2Int maxCell)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var b = _all[i];
                var bMin = b.AnchorCell;
                var bMax = b.AnchorCell + b.Footprint - Vector2Int.one;
                if (bMin.x <= maxCell.x && bMax.x >= minCell.x &&
                    bMin.y <= maxCell.y && bMax.y >= minCell.y)
                    return b;
            }
            return null;
        }

        /// <summary>
        /// Flips a building's owner (capture completion). Resets both sides' capture progress.
        /// </summary>
        public void SetOwner(BuildingRuntime building, BuildingOwner owner)
        {
            if (building == null || building.Owner == owner) return;
            building.Owner = owner;
            building.RefreshOperational();
            building.CaptureProgressPlayer = 0f;
            building.CaptureProgressEnemy = 0f;
            building.GoldIncomeTimer = 0f;
            if (BuildingOwnershipChanged != null) BuildingOwnershipChanged(building);
        }

        /// <summary>Sets the contested flag and raises the event on change.</summary>
        public void SetContested(BuildingRuntime building, bool contested)
        {
            if (building == null || building.Contested == contested) return;
            building.Contested = contested;
            if (BuildingContestedChanged != null) BuildingContestedChanged(building);
        }

        /// <summary>Raises the capture-progress event (UI redraw).</summary>
        public void NotifyCaptureProgressChanged(BuildingRuntime building)
        {
            if (building != null && BuildingCaptureProgressChanged != null)
                BuildingCaptureProgressChanged(building);
        }

        static IEnumerable<Vector2Int> FootprintCells(Vector2Int anchor, Vector2Int footprint)
        {
            for (int y = 0; y < footprint.y; y++)
                for (int x = 0; x < footprint.x; x++)
                    yield return new Vector2Int(anchor.x + x, anchor.y + y);
        }

        static bool Overlaps(BuildingRuntime other, Vector2Int anchor, Vector2Int footprint)
        {
            var oMin = other.AnchorCell;
            var oMax = other.AnchorCell + other.Footprint - Vector2Int.one;
            var nMin = anchor;
            var nMax = anchor + footprint - Vector2Int.one;
            return oMin.x <= nMax.x && oMax.x >= nMin.x &&
                oMin.y <= nMax.y && oMax.y >= nMin.y;
        }
    }
}
