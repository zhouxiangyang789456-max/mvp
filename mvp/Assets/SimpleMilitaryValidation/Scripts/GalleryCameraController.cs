using UnityEngine;

namespace Mvp.Validation
{
    public sealed class GalleryCameraController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        public float fastMultiplier = 2.5f;
        public float lookSensitivity = 2f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (Input.GetKey(KeyCode.E)) input.y += 1f;
            if (Input.GetKey(KeyCode.Q)) input.y -= 1f;

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
            transform.position += transform.TransformDirection(input.normalized) * speed * Time.deltaTime;
        }
    }
}
