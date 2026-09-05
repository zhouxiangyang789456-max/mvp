using UnityEngine;

namespace Mvp.Shared
{
    public enum UnitExitState
    {
        Active,
        Extracting,
        Extracted
    }

    /// <summary>Mutable runtime state of a unit instance.</summary>
    public sealed class UnitRuntimeData
    {
        public string Id;
        public TeamId Team;
        public UnitDefinition Definition;
        public string CommanderGroupId;
        public int FormationSlotIndex;
        public int SpawnOrder;
        /// <summary>Visual members represented by this one logical grid unit.</summary>
        public int MembersPerSlot = 1;
        public Vector2Int GridPosition;
        public Vector3 WorldPosition;
        public UnitCommand CurrentCommand = new UnitCommand();
        public int CurrentHealth;
        /// <summary>
        /// Effective max health after trait modifiers (settled once at spawn).
        /// Zero means "no trait modification; fall back to Definition.MaxHealth".
        /// </summary>
        public int RuntimeMaxHealth;
        public UnitState State;
        public UnitExitState ExitState = UnitExitState.Active;
    }
}
