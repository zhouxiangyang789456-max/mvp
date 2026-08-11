using UnityEngine;

namespace Mvp.Shared
{
    /// <summary>Kind of order a unit is currently executing.</summary>
    public enum UnitCommandType
    {
        None,
        Move,
        Attack,
        FormationDeploy
    }

    /// <summary>Current order for a unit.</summary>
    public sealed class UnitCommand
    {
        public UnitCommandType Type = UnitCommandType.None;
        public Vector3 TargetPosition;
        public UnitRuntimeData TargetUnit;
    }
}
