using SimpleMilitary.VehicleAnimation;
using UnityEngine;

namespace Mvp.Validation
{
    public sealed class SimpleMilitaryValidationController : MonoBehaviour
    {
        public Transform movingVehicle;
        public Transform turretTarget;
        public Animator characterAnimator;

        private Vector3 _vehicleOrigin;

        private void Start()
        {
            if (movingVehicle != null)
                _vehicleOrigin = movingVehicle.position;

            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
                characterAnimator.Play("Idle", 0, 0f);
            }
        }

        private void Update()
        {
            float time = Time.time;

            if (movingVehicle != null)
            {
                float distance = Mathf.Sin(time * 0.55f) * 2.5f;
                movingVehicle.position = _vehicleOrigin + Vector3.forward * distance;
            }

            if (turretTarget != null)
            {
                turretTarget.position = new Vector3(
                    Mathf.Sin(time * 0.7f) * 5f,
                    1.25f + Mathf.Sin(time * 0.4f) * 0.5f,
                    5f + Mathf.Cos(time * 0.7f) * 3f);
            }
        }
    }
}
