using Mvp.Battle.Outcome;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// Formal in-battle HUD for the timed extraction objective (阶段 E). Replaces
    /// the legacy <c>OnGUI</c> debug counter with a screen-space overlay built at
    /// runtime, matching the project's TextMeshPro + CanvasScaler conventions.
    ///
    /// Shows the countdown (colour-coded and pulsing under the 30s / 10s warning
    /// thresholds), the extraction progress ("已撤离 X / 需撤离 Y") and a short
    /// status line (portal opening / enemies cleared / go-to-portal).
    /// </summary>
    public sealed class ExtractionHud : MonoBehaviour
    {
        const int SortOrder = 600;

        static readonly Color NormalColor = new Color(0.62f, 0.86f, 1f);
        static readonly Color WarnColor = new Color(1f, 0.82f, 0.25f);
        static readonly Color DangerColor = new Color(1f, 0.35f, 0.3f);

        TextMeshProUGUI _countdown;
        TextMeshProUGUI _progress;
        TextMeshProUGUI _status;
        TMP_FontAsset _font;
        bool _built;

        /// <summary>
        /// Creates (or returns the existing) extraction HUD parented under
        /// <paramref name="parent"/> so it is torn down with the battle core.
        /// </summary>
        public static ExtractionHud Show(Transform parent)
        {
            var existing = FindObjectOfType<ExtractionHud>();
            if (existing != null) return existing;
            var go = new GameObject("ExtractionHud", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<ExtractionHud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            _font = Resources.Load<TMP_FontAsset>("Battle/UI/Fonts/SimHei SDF");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0.06f, 0.1f, 0.82f);
            panelImage.raycastTarget = false;
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(360f, 134f);
            prt.anchoredPosition = new Vector2(0f, -16f);

            _countdown = Label(panel.transform, "", 40, new Vector2(0.5f, 0.78f), new Vector2(340, 52));
            _progress = Label(panel.transform, "", 22, new Vector2(0.5f, 0.38f), new Vector2(340, 30));
            _status = Label(panel.transform, "", 20, new Vector2(0.5f, 0.08f), new Vector2(340, 26));
            _built = true;
            gameObject.SetActive(false);
        }

        TextMeshProUGUI Label(Transform parent, string text, float size, Vector2 anchor, Vector2 dims)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = NormalColor;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = dims;
            rt.anchoredPosition = Vector2.zero;
            return label;
        }

        void Update()
        {
            if (!_built) return;
            var c = ExtractionObjectiveController.Instance;
            bool show = c != null && c.IsEnabled && c.IsStarted && !c.Resolved;
            if (!show)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            int secs = Mathf.CeilToInt(c.RemainingSeconds);
            _countdown.text = "撤离倒计时 " + secs + " 秒";
            _countdown.color = c.RemainingSeconds <= 10f ? DangerColor :
                c.RemainingSeconds <= 30f ? WarnColor : NormalColor;
            _countdown.transform.localScale = c.RemainingSeconds <= 10f
                ? Vector3.one * (1f + 0.05f * Mathf.Sin(Time.time * 9f))
                : Vector3.one;

            _progress.text = "已撤离 " + c.ExtractedCount + " / " + c.RequiredCount;
            _status.text = c.IsOpening ? "传送门开启中…" :
                (c.EnemiesCleared ? "敌军已肃清，前往传送门" : "前往传送门撤离");
        }
    }
}
