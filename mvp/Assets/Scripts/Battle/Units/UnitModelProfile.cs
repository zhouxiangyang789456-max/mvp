using UnityEngine;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Describes how an imported visual prefab is presented inside one logical
    /// battle unit. The profile keeps package-specific scale/orientation out of
    /// UnitView and does not contain mutable battle state.
    /// </summary>
    public sealed class UnitModelProfile : MonoBehaviour
    {
        public float ModelScale = 0.58f;
        public float HealthAnchorY = 0.72f;
        public float GroundClearance = 0.05f;
        public bool SingleVisualPerSlot = true;
        public Vector3 ContainerEuler = Vector3.zero;
        public Vector3 InstanceEuler = Vector3.zero;
        public float FacingYawOffset;
    }
}
