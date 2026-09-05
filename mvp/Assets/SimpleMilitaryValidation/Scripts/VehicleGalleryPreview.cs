using SimpleMilitary.VehicleAnimation;
using UnityEngine;

namespace Mvp.Validation
{
    public sealed class VehicleGalleryPreview : MonoBehaviour
    {
        public Transform aimTarget;
        public float movementDistance = 1.25f;
        public float movementSpeed = 0.55f;

        private Vector3 _origin;
        private VehicleWheels _wheels;

        private void Start()
        {
            _origin = transform.position;
            _wheels = GetComponent<VehicleWheels>();

            VehicleTurretAim aim = GetComponent<VehicleTurretAim>();
            if (aim != null)
                aim.target = aimTarget;
        }

        private void Update()
        {
            float time = Time.time;
            if (_wheels != null)
                transform.position = _origin + Vector3.forward * (Mathf.Sin(time * movementSpeed) * movementDistance);

            if (aimTarget != null)
            {
                aimTarget.position = _origin + new Vector3(
                    Mathf.Sin(time * 0.6f) * 3f,
                    1.5f,
                    3.5f + Mathf.Cos(time * 0.6f) * 2f);
            }
        }
    }
}
