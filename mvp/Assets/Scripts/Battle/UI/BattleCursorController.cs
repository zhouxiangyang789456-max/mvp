using UnityEngine;
using UnityEngine.UI;

namespace Mvp.Battle.UI
{
    public sealed class BattleCursorController : MonoBehaviour
    {
        const string SelectCursorPath = "Battle/UI/Cursors/cursor_select_hand";
        const string RangeCursorPath = "Battle/UI/Cursors/cursor_long_range";

        public static BattleCursorController Instance { get; private set; }

        RectTransform _rect;
        RectTransform _canvasRect;
        Canvas _canvas;
        RawImage _image;
        Texture2D _selectCursor;
        Texture2D _rangeCursor;

        public static BattleCursorController Create(Transform canvasRoot)
        {
            if (Instance != null) return Instance;
            if (canvasRoot == null) return null;
            var go = new GameObject("BattleCursor", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(canvasRoot, false);
            return go.AddComponent<BattleCursorController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _rect = GetComponent<RectTransform>();
            _image = GetComponent<RawImage>();
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.GetComponent<RectTransform>() : null;
            _selectCursor = Resources.Load<Texture2D>(SelectCursorPath);
            _rangeCursor = Resources.Load<Texture2D>(RangeCursorPath);
            _image.raycastTarget = false;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            SetRangeHover(false);
            transform.SetAsLastSibling();
            Cursor.visible = false;
        }

        void OnEnable() { Cursor.visible = false; }
        void OnDisable() { Cursor.visible = true; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Cursor.visible = true;
        }

        void Update()
        {
            if (_canvasRect == null || _rect == null) return;
            Vector2 local;
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, Input.mousePosition, eventCamera, out local))
                _rect.anchoredPosition = local;
            transform.SetAsLastSibling();
        }

        public void SetRangeHover(bool active)
        {
            bool showRange = active && _rangeCursor != null;
            if (_image == null || _rect == null) return;
            _image.texture = showRange ? _rangeCursor : _selectCursor;
            _image.enabled = _image.texture != null;
            if (showRange)
            {
                _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.sizeDelta = new Vector2(48f, 48f);
            }
            else
            {
                _rect.pivot = new Vector2(0.18f, 0.82f);
                _rect.sizeDelta = new Vector2(36f, 36f);
            }
        }
    }
}
