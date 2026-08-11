using UnityEngine;
using Mvp.Battle.Map;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// Fixed-tilt RTS camera (战斗页面开发文档 摄像机控制). Only translates and zooms -
    /// rotation is locked to the scene's oblique top-down angle.
    ///
    /// - Middle-mouse drag pans on the ground plane.
    /// - WASD / arrow keys pan.
    /// - Mouse wheel zooms the distance to the ground focus.
    /// - The ground focus is clamped inside the map bounds so the camera cannot
    ///   drift off the battlefield.
    /// - FocusOn(cell) is used by the minimap to recentre the view.
    /// </summary>
    public sealed class BattleCameraController : MonoBehaviour
    {
        public static BattleCameraController Instance { get; private set; }

        [Header("Pan")]
        [Tooltip("World units per second at distance 10; scaled by current zoom.")]
        [SerializeField] float _keyboardPanSpeed = 7f;
        [SerializeField] float _dragPanScale = 1f;

        [Header("Zoom")]
        [SerializeField] float _zoomStep = 0.12f;
        [SerializeField] float _minDistance = 4f;
        [SerializeField] float _maxDistance = 20f;

        [Header("Bounds")]
        [Tooltip("How far the ground focus may leave the grid before clamping.")]
        [SerializeField] float _boundsMargin = 1.5f;

        Camera _cam;
        Vector3 _viewDir;      // unit camera forward (fixed)
        float _distance;       // distance from camera to ground focus
        Vector3 _focus;        // ground point under the screen centre (clamped)
        bool _dragging;
        Vector3 _lastDragGround;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _cam = GetComponent<Camera>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _viewDir = transform.forward;
            _focus = CameraGround(transform.position, transform.forward);
            _distance = Mathf.Max(0.1f, Vector3.Distance(transform.position, _focus));
            Apply();
        }

        void Update()
        {
            if (_cam == null) return;
            HandleDrag();
            HandleKeyboard();
            HandleZoom();
        }

        // ---- pan ----------------------------------------------------------------

        void HandleDrag()
        {
            bool down = Input.GetMouseButton(2);
            if (down && !_dragging)
            {
                _dragging = true;
                _lastDragGround = GroundAt(Input.mousePosition);
            }
            else if (down && _dragging)
            {
                Vector3 ground = GroundAt(Input.mousePosition);
                Vector3 delta = ground - _lastDragGround;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    _focus -= delta * _dragPanScale;
                    _lastDragGround = ground;
                    ClampFocus();
                    Apply();
                }
            }
            else if (_dragging)
            {
                _dragging = false;
            }
        }

        void HandleKeyboard()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (h == 0f && v == 0f) return;

            Vector3 right = transform.right; right.y = 0f; right.Normalize();
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            float speed = _keyboardPanSpeed * (_distance / 10f);
            _focus += (right * h + fwd * v) * speed * Time.deltaTime;
            ClampFocus();
            Apply();
        }

        // ---- zoom ---------------------------------------------------------------

        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;
            _distance *= (1f - scroll * _zoomStep);
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            Apply();
        }

        /// <summary>Zooms by a signed step (-1 in, +1 out); used by the minimap zoom button.</summary>
        public void ZoomBy(int dir)
        {
            _distance *= (1f + dir * _zoomStep);
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            Apply();
        }

        // ---- focus --------------------------------------------------------------

        /// <summary>Recentres the camera on a grid cell (minimap navigation).</summary>
        public void FocusOn(Vector2Int cell)
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return;
            _focus = grid.GridToWorld(cell);
            ClampFocus();
            Apply();
        }

        void ClampFocus()
        {
            var grid = BattleGridController.Instance;
            if (grid == null) return;
            _focus.x = Mathf.Clamp(_focus.x, -_boundsMargin, grid.Width - 1 + _boundsMargin);
            _focus.z = Mathf.Clamp(_focus.z, -_boundsMargin, grid.Height - 1 + _boundsMargin);
        }

        void Apply()
        {
            if (_viewDir.sqrMagnitude < 0.0001f) return;
            transform.position = _focus - _viewDir * _distance;
        }

        Vector3 GroundAt(Vector3 screenPos)
        {
            var ray = _cam.ScreenPointToRay(screenPos);
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            if (plane.Raycast(ray, out dist)) return ray.GetPoint(dist);
            return _focus;
        }

        static Vector3 CameraGround(Vector3 pos, Vector3 dir)
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            Ray ray = new Ray(pos, dir);
            if (plane.Raycast(ray, out dist)) return ray.GetPoint(dist);
            return new Vector3(pos.x, 0f, pos.z);
        }
    }
}
