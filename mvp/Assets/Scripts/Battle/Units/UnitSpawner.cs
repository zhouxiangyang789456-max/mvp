using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Generation;
using Mvp.Battle.Buildings;
using Mvp.Shared;
using Mvp.Battle.Commanders;
using Mvp.CommanderSelect;
using Mvp.Battle.Formation;
using Mvp.Battle.Outcome;
using Mvp.Battle.Traits;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Spawns the starting army from BattleStartContext (伊莲娜: 2 步兵 + 1 坦克)
    /// plus one enemy test unit for verifying attack commands. Placeholder visuals
    /// per 回退策略. Attach to the scene; Start() runs once at battle start.
    /// </summary>
    public sealed class UnitSpawner : MonoBehaviour
    {
        bool _spawned;

        void Start()
        {
            if (_spawned) return;
            _spawned = true;
            SpawnAll();
        }

        public void SpawnAll()
        {
            BattleCore.Ensure();
            var grid = BattleGridController.Instance;
            if (grid == null)
            {
                Debug.LogError("[UnitSpawner] BattleGridController.Instance missing");
                return;
            }

            // 阶段B: buildings must be on the grid before any unit spawn validation
            // (footprint cells are blocked/occupied; unit spawn cells must not overlap).
            var buildings = BuildingRegistry.Instance;
            if (buildings != null) buildings.EnsureDefaultBuildingsPlaced();

            var roster = ResolveRoster();
            TraitEffectService.BuildRuntime(roster.Commanders);
            var deployment = ResolveDeployment(grid, roster.Commanders.Count, 2);
            if (!deployment.Passed)
            {
                Debug.LogError("[UnitSpawner] Cannot allocate deployment zones: " +
                    deployment.FailureReason);
                return;
            }

            for (int r = 0; r < roster.Commanders.Count; r++)
            {
                var entry = roster.Commanders[r];
                var commander = CommanderCatalog.GetById(entry.CommanderId);
                if (commander == null) continue;
                if (!commander.HasValidSingleTypeStartingArmy() ||
                    entry.StartingUnits.Count != 1 ||
                    entry.StartingUnits[0].UnitType != commander.StartingUnits[0].UnitType)
                {
                    Debug.LogError("[UnitSpawner] Invalid single-type starting army for " +
                        entry.CommanderId);
                    continue;
                }
                var zone = deployment.PlayerZones[r];
                var group = new CommanderGroupRuntime
                {
                    GroupId = "player_group_" + entry.RosterIndex,
                    CommanderId = commander.Id,
                    RosterIndex = entry.RosterIndex,
                    Team = TeamId.Player,
                    Definition = commander,
                    Formation = entry.InitialFormation,
                    AnchorCell = ToVector(zone.Anchor),
                    State = CommanderGroupState.Idle
                };

                int spawnOrder = 0;
                bool groupFailed = CountUnits(entry) > zone.Cells.Count;
                for (int e = 0; e < entry.StartingUnits.Count; e++)
                {
                    var unitEntry = entry.StartingUnits[e];
                    var def = UnitCatalog.Get(unitEntry.UnitType);
                    if (def == null)
                    {
                        groupFailed = true;
                        break;
                    }
                    for (int i = 0; i < unitEntry.Count; i++)
                    {
                        int slot = DeploymentAreaPlanner.GetFormationSlotIndex(spawnOrder);
                        if (slot < 0)
                        {
                            groupFailed = true;
                            break;
                        }
                        var cell = ToVector(zone.Cells[slot]);
                        var view = SpawnUnit(def, TeamId.Player, cell, group.GroupId,
                            spawnOrder, slot, unitEntry.MembersPerSlot);
                        if (view != null) group.Members.Add(view);
                        else groupFailed = true;
                        spawnOrder++;
                    }
                    if (groupFailed) break;
                }

                if (groupFailed || group.Members.Count == 0)
                {
                    RollbackGroup(group, grid);
                    Debug.LogError("[UnitSpawner] Failed to spawn complete commander group " +
                        group.GroupId + " in its deployment zone.");
                    continue;
                }

                InitializeFacingTowardMapCenter(group, grid);
                CaptureInitialLayout(group, false);
                if (group.Members.Count > 0)
                    CommanderGroupRegistry.Instance.Register(group, true);
            }

            if (CommanderGroupRegistry.Instance == null ||
                CommanderGroupRegistry.Instance.Groups.Count == 0)
            {
                Debug.LogError("[UnitSpawner] No playable commander group could be spawned.");
            }

            SpawnEnemyGroup("enemy_group_alpha", "enemy_commander_alpha",
                "北境先锋", deployment.EnemyZones[0], 2, 1);
            SpawnEnemyGroup("enemy_group_beta", "enemy_commander_beta",
                "铁卫军团", deployment.EnemyZones[1], 3, 1);
            if (BattleOutcomeController.Instance != null)
                BattleOutcomeController.Instance.NotifyInitialSpawnCompleted();
            if (ExtractionObjectiveController.Instance != null)
                ExtractionObjectiveController.Instance.InitializeAfterSpawn();
        }

        void SpawnEnemyGroup(string groupId, string commanderId, string displayName,
            DeploymentZone zone, int infantryCount, int tankCount)
        {
            var group = new CommanderGroupRuntime
            {
                GroupId = groupId,
                CommanderId = commanderId,
                Team = TeamId.Enemy,
                Definition = new CommanderDefinition
                {
                    Id = commanderId,
                    DisplayName = displayName,
                    MaxHealth = 100,
                    CurrentHealth = 100
                },
                Formation = FormationType.Square,
                AnchorCell = ToVector(zone.Anchor),
                State = CommanderGroupState.Idle
            };

            int spawnOrder = 0;
            bool failed = !SpawnEnemyMembers(group, zone, UnitType.Infantry,
                infantryCount, ref spawnOrder);
            if (!failed)
                failed = !SpawnEnemyMembers(group, zone, UnitType.Tank,
                    tankCount, ref spawnOrder);
            if (failed || group.Members.Count == 0)
            {
                RollbackGroup(group, BattleGridController.Instance);
                Debug.LogError("[UnitSpawner] Failed to spawn complete enemy group " + groupId);
                return;
            }

            InitializeFacingTowardMapCenter(group, BattleGridController.Instance);
            CaptureInitialLayout(group, true);
            CommanderGroupRegistry.Instance.Register(group, false);
        }

        bool SpawnEnemyMembers(CommanderGroupRuntime group, DeploymentZone zone,
            UnitType type, int count,
            ref int spawnOrder)
        {
            var def = UnitCatalog.Get(type);
            if (def == null) return false;
            for (int i = 0; i < count; i++)
            {
                int slot = DeploymentAreaPlanner.GetFormationSlotIndex(spawnOrder);
                if (slot < 0) return false;
                var view = SpawnUnit(def, TeamId.Enemy, ToVector(zone.Cells[slot]),
                    group.GroupId, spawnOrder, slot);
                if (view != null) group.Members.Add(view);
                else return false;
                spawnOrder++;
            }
            return true;
        }

        ExpeditionRosterSnapshot ResolveRoster()
        {
            if (BattleStartContext.ExpeditionRoster != null &&
                !BattleStartContext.ExpeditionRoster.IsEmpty)
                return BattleStartContext.ExpeditionRoster;

            var commander = BattleStartContext.SelectedCommander;
            if (commander == null)
            {
                var all = CommanderCatalog.GetAll();
                if (all.Count > 0) commander = all[0];
            }
            var fallback = new ExpeditionRosterSnapshot();
            if (commander != null)
                fallback.Commanders.Add(ExpeditionCommanderEntry.FromDefinition(commander, 0));
            BattleStartContext.ExpeditionRoster = fallback;
            return fallback;
        }

        static DeploymentPlan ResolveDeployment(BattleGridController grid,
            int playerGroups, int enemyGroups)
        {
            var generated = BattleMapContext.LastGeneratedData;
            if (generated != null &&
                generated.PlayerDeploymentZones.Count >= playerGroups &&
                generated.EnemyDeploymentZones.Count >= enemyGroups)
            {
                var cached = new DeploymentPlan();
                cached.PlayerZones.AddRange(generated.PlayerDeploymentZones);
                cached.EnemyZones.AddRange(generated.EnemyDeploymentZones);
                return cached;
            }

            return DeploymentAreaPlanner.Plan(grid.CreateTerrainSnapshot(),
                playerGroups, enemyGroups, TerrainCatalog.IsWalkable);
        }

        static int CountUnits(ExpeditionCommanderEntry entry)
        {
            int count = 0;
            for (int i = 0; i < entry.StartingUnits.Count; i++)
                count += Mathf.Max(0, entry.StartingUnits[i].Count);
            return count;
        }

        static Vector2Int ToVector(GridCoord cell)
        {
            return new Vector2Int(cell.X, cell.Y);
        }

        static void CaptureInitialLayout(CommanderGroupRuntime group, bool locked)
        {
            var slots = new List<Vector2Int>(group.Members.Count);
            for (int i = 0; i < group.Members.Count; i++)
                slots.Add(group.Members[i].Data.GridPosition);
            group.Layout.Capture(group, group.AnchorCell, group.Members, slots, locked);
        }

        static void InitializeFacingTowardMapCenter(CommanderGroupRuntime group,
            BattleGridController grid)
        {
            if (group == null || grid == null) return;
            var center = new Vector2Int((grid.Width - 1) / 2, (grid.Height - 1) / 2);
            var facing = FormationFacing.Quantize(center - group.AnchorCell);
            if (facing == Vector2Int.zero) facing = FormationFacing.Default;
            group.Facing = facing;

            Vector3 worldDirection = FormationFacing.WorldDirection(facing);
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null ||
                    member.Data.State == UnitState.Dead) continue;
                member.SetFacingDirection(worldDirection);
            }
        }
        static void RollbackGroup(CommanderGroupRuntime group, BattleGridController grid)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                var view = group.Members[i];
                if (view == null) continue;
                if (view.Data != null && grid != null)
                    grid.SetOccupied(view.Data.GridPosition, false);
                Object.Destroy(view.gameObject);
            }
            group.Members.Clear();
        }

        /// <summary>Creates a unit's runtime data + placeholder view on a grid cell.</summary>
        public UnitView SpawnUnit(UnitDefinition def, TeamId team, Vector2Int cell)
        {
            return SpawnUnit(def, team, cell, string.Empty, 0);
        }

        public UnitView SpawnUnit(UnitDefinition def, TeamId team, Vector2Int cell,
            string commanderGroupId, int spawnOrder)
        {
            return SpawnUnit(def, team, cell, commanderGroupId, spawnOrder, spawnOrder);
        }

        UnitView SpawnUnit(UnitDefinition def, TeamId team, Vector2Int cell,
            string commanderGroupId, int spawnOrder, int formationSlotIndex,
            int membersPerSlot = 1)
        {
            var grid = BattleGridController.Instance;
            if (def == null || grid == null) return null;
            if (!grid.InBounds(cell) || !grid.IsWalkable(cell) || grid.IsOccupied(cell)) return null;

            grid.SetOccupied(cell, true);

            int maxHealthBonus = TraitEffectService.GetMaxHealthBonus(def, commanderGroupId);
            int runtimeMaxHealth = def.MaxHealth + maxHealthBonus;
            if (runtimeMaxHealth < 1) runtimeMaxHealth = 1;

            var data = new UnitRuntimeData
            {
                Id = "unit_" + team + "_" + cell.x + "_" + cell.y,
                Team = team,
                Definition = def,
                CommanderGroupId = commanderGroupId,
                FormationSlotIndex = formationSlotIndex,
                SpawnOrder = spawnOrder,
                MembersPerSlot = Mathf.Clamp(membersPerSlot, 1, 3),
                RuntimeMaxHealth = runtimeMaxHealth,
                CurrentHealth = runtimeMaxHealth,
                State = UnitState.Idle,
                GridPosition = cell
            };

            var go = new GameObject(data.Id);
            var view = go.AddComponent<UnitView>();

            var world = grid.GridToWorld(cell);
            world.y = TerrainCatalog.GetElevation(grid.GetTerrain(cell));
            view.Spawn(data, world);
            view.AttachHealthBar();

            return view;
        }
    }
}
