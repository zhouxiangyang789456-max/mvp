using SimpleMilitary.VehicleAnimation;
using UnityEngine;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Bridges battle state/fire events to the transform-driven animations on
    /// Simple Military vehicles. Logical movement remains owned by UnitView.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class VehicleUnitAnimationDriver : MonoBehaviour
    {
        [SerializeField] float recoilDegrees = 7f;
        [SerializeField] float recoilDuration = 0.18f;

        UnitView _unit;
        VehicleTurretAim _aim;
        Transform _barrel;
        float _recoilRemaining;

        public void Initialize(UnitView unit)
        {
            _unit = unit;
            _aim = GetComponent<VehicleTurretAim>();
            if (_aim == null) _aim = GetComponentInChildren<VehicleTurretAim>();
            _barrel = _aim != null ? _aim.barrel : null;
        }

        public void PlayAttack()
        {
            _recoilRemaining = recoilDuration;
        }

        void Update()
        {
            if (_unit == null || _aim == null) return;
            var selection = UnitSelectionController.Instance;
            bool isTank = _unit.Data != null && _unit.Data.Definition != null &&
                (_unit.Data.Definition.Type == Mvp.Shared.UnitType.Tank ||
                 _unit.Data.Definition.Type == Mvp.Shared.UnitType.HeavyTank);
            if (isTank && _unit.Data.Team == Mvp.Shared.TeamId.Player &&
                selection != null && selection.HasMapAimPoint)
            {
                _aim.SetAimPoint(selection.LastMapAimPoint);
            }
            else
            {
                var combat = UnitCombatController.Instance;
                var target = combat != null ? combat.GetTarget(_unit) : null;
                if (target != null)
                    _aim.SetAimTarget(target.transform);
                else if (_unit.Data != null)
                    _aim.SetAimPoint(_unit.transform.position + _unit.transform.forward * 4f);
            }

            if (_recoilRemaining > 0f)
                _recoilRemaining -= Time.deltaTime;
        }

        void LateUpdate()
        {
            if (_barrel == null || _recoilRemaining <= 0f || recoilDuration <= 0f) return;
            float normalized = Mathf.Clamp01(_recoilRemaining / recoilDuration);
            float pulse = Mathf.Sin(normalized * Mathf.PI) * recoilDegrees;
            _barrel.localRotation *= Quaternion.AngleAxis(-pulse, Vector3.right);
        }
    }
}
