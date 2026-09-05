#if UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.EditorTests.Battle.Skills
{
    /// <summary>
    /// Shared fixtures for the battle-skill-system NUnit tests. Units are plain managed
    /// instances created with FormatterServices.GetUninitializedObject and injected into
    /// the private UnitView.Data accessor, so the tests never touch the Unity engine
    /// (safe for both the Unity Test Runner EditMode and a standalone CLR harness).
    /// </summary>
    internal static class SkillTestFixtures
    {
        public static UnitDefinition MakeDefinition(UnitTag tags,
            float minRange, float maxRange, int maxHealth = 100, float moveSpeed = 3f)
        {
            return new UnitDefinition
            {
                Type = UnitType.Infantry,
                DisplayName = "test",
                MaxHealth = maxHealth,
                MoveSpeed = moveSpeed,
                VisionRange = 5,
                AttackRangeMin = minRange,
                AttackRangeMax = maxRange,
                AttackPower = 10,
                AttackCooldown = 1f,
                AreaRadius = 0f,
                Tags = tags
            };
        }

        public static UnitRuntimeData MakeData(string id, UnitDefinition def,
            Vector2Int cell, TeamId team = TeamId.Player, UnitState state = UnitState.Idle)
        {
            return new UnitRuntimeData
            {
                Id = id,
                Team = team,
                Definition = def,
                GridPosition = cell,
                CurrentHealth = def != null ? def.MaxHealth : 0,
                State = state,
                SpawnOrder = 0
            };
        }

        public static UnitView MakeUnit(UnitRuntimeData data)
        {
            var view = (UnitView)FormatterServices.GetUninitializedObject(typeof(UnitView));
            var prop = typeof(UnitView).GetProperty("Data",
                BindingFlags.Instance | BindingFlags.Public);
            prop.SetValue(view, data);
            return view;
        }

        public static CommanderGroupRuntime MakeGroup(string groupId, params UnitView[] members)
        {
            var group = new CommanderGroupRuntime
            {
                GroupId = groupId,
                CommanderId = "cmd_" + groupId,
                Team = TeamId.Player,
                State = CommanderGroupState.Idle
            };
            for (int i = 0; i < members.Length; i++) group.Members.Add(members[i]);
            if (members.Length > 0 && members[0] != null && members[0].Data != null)
                group.AnchorCell = members[0].Data.GridPosition;
            return group;
        }

        /// <summary>
        /// Installs a fake BattleGridController whose terrain is Plain everywhere except
        /// the given Forest cells. Uses reflection so no scene / Awake is required.
        /// </summary>
        public static void InstallGrid(int width, int height, params Vector2Int[] forestCells)
        {
            var grid = (BattleGridController)FormatterServices.GetUninitializedObject(
                typeof(BattleGridController));
            var terrain = new TerrainType[height, width];
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                    terrain[z, x] = TerrainType.Plain;
            for (int i = 0; i < forestCells.Length; i++)
                terrain[forestCells[i].y, forestCells[i].x] = TerrainType.Forest;
            SetField(grid, "_terrain", terrain);
            SetField(grid, "_width", width);
            SetField(grid, "_height", height);
            typeof(BattleGridController).GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static).SetValue(null, grid);
        }

        public static void ClearGrid()
        {
            typeof(BattleGridController).GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static).SetValue(null, null);
        }

        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
#endif
