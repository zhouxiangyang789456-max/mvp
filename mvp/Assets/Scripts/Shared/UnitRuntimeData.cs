using UnityEngine;

namespace Mvp.Shared
{
    /// <summary>Mutable runtime state of a unit instance.</summary>
    public sealed class UnitRuntimeData
    {
        public string Id;
        public TeamId Team;
        public UnitDefinition Definition;
        public Vector2Int GridPosition;
        public Vector3 WorldPosition;
        public UnitCommand CurrentCommand = new UnitCommand();
        public int CurrentHealth;
        public UnitState State;
    }
}
