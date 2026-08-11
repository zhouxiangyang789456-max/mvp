using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mvp.Battle.Formation;
using Mvp.Battle.Map;
using Mvp.CommanderSelect;
using Mvp.Shared;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// Battle page UI (战斗页面开发文档 战斗页面). Attach to the scene Canvas.
    ///
    /// Reads the selected commander (BattleStartContext) and populates the left
    /// commander panel (name / health / traits), the bottom card bar (starting
    /// units), wires the formation buttons, the minimap focus / zoom, the
    /// commander portrait deploy toggle, and creates the "开始战斗" button that
    /// switches the deployment phase to the real-time combat phase.
    /// </summary>
    public sealed class BattleUiController : MonoBehaviour
    {
        public static BattleUiController Instance { get; private set; }

        // ---- cached UI ---------------------------------------------------------

        TextMeshProUGUI _nameText;
        TextMeshProUGUI _healthText;
        RectTransform _healthBarBg;
        RectTransform _healthBarFill;
        RectTransform[] _traitRoots = new RectTransform[4];
        Button _portraitButton;
        GameObject _startBattleGo;
        Button _startBattleButton;

        readonly TextMeshProUGUI[] _cardNames = new TextMeshProUGUI[6];
        readonly TextMeshProUGUI[] _cardCounts = new TextMeshProUGUI[6];
        readonly Image[] _cardBadges = new Image[6];

        readonly Button[] _formationButtons = new Button[3];
        readonly Image[] _formationImages = new Image[3];

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // Re-entering battle always restarts from the deployment phase.
            BattlePhaseState.ResetToDeployment();

            var commander = ResolveCommander();
            BindCommanderPanel();
            BindCardBar();
            BindFormationButtons();
            BindMiniMap();
            BindPortrait();
            CreateStartBattleButton();

            PopulateCommander(commander);
            PopulateCardBar(commander);
            RefreshFormationHighlight();
            RefreshPhaseUi();

            StartCoroutine(AutoDeploy());
        }

        CommanderDefinition ResolveCommander()
        {
            var commander = BattleStartContext.SelectedCommander;
            if (commander == null)
            {
                var all = CommanderCatalog.GetAll();
                if (all != null && all.Count > 0) commander = all[0];
            }
            return commander;
        }

        // ---- panel / card binding ---------------------------------------------

        void BindCommanderPanel()
        {
            var panel = transform.Find("BattleUI/CommanderPanel");
            if (panel == null) return;

            _nameText = panel.Find("Name")?.GetComponent<TextMeshProUGUI>();
            _healthText = panel.Find("HealthBarBg/HealthText")?.GetComponent<TextMeshProUGUI>();
            _healthBarBg = panel.Find("HealthBarBg")?.GetComponent<RectTransform>();
            _healthBarFill = panel.Find("HealthBarBg/HealthBarFill")?.GetComponent<RectTransform>();

            for (int i = 0; i < _traitRoots.Length; i++)
            {
                _traitRoots[i] = panel.Find("Trait" + (i + 1))?.GetComponent<RectTransform>();
            }
        }

        void BindCardBar()
        {
            for (int i = 0; i < _cardNames.Length; i++)
            {
                var slot = transform.Find("BattleUI/CardBar/CardSlot" + (i + 1));
                if (slot == null) continue;
                _cardNames[i] = slot.Find("Name")?.GetComponent<TextMeshProUGUI>();
                _cardCounts[i] = slot.Find("Count")?.GetComponent<TextMeshProUGUI>();
                _cardBadges[i] = slot.Find("Badge")?.GetComponent<Image>();
            }
        }

        void BindFormationButtons()
        {
            for (int i = 0; i < _formationButtons.Length; i++)
            {
                var root = transform.Find("BattleUI/FormationPanel/FormationBtn" + (i + 1));
                if (root == null) continue;

                var img = root.GetComponent<Image>();
                var btn = root.GetComponent<Button>();
                if (btn == null) btn = root.gameObject.AddComponent<Button>();
                if (img != null) btn.targetGraphic = img;

                var label = root.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = i == 0 ? "竖向" : i == 1 ? "横向" : "方形";
                    label.font = ResolveFont(label.font);
                }

                FormationType type = i == 0 ? FormationType.Vertical
                    : i == 1 ? FormationType.Horizontal
                    : FormationType.Square;
                int idx = i;
                btn.onClick.AddListener(() => OnFormationClick(idx, type));

                _formationButtons[i] = btn;
                _formationImages[i] = img;
            }
        }

        void BindMiniMap()
        {
            var minimap = transform.Find("BattleUI/MiniMapPanel");
            if (minimap == null) return;

            var mapArea = minimap.Find("MapArea");
            if (mapArea != null)
            {
                var img = mapArea.GetComponent<Image>();
                var btn = mapArea.GetComponent<Button>();
                if (btn == null) btn = mapArea.gameObject.AddComponent<Button>();
                if (img != null) btn.targetGraphic = img;
                var click = mapArea.gameObject.GetComponent<MiniMapClick>();
                if (click == null) click = mapArea.gameObject.AddComponent<MiniMapClick>();
                click.Owner = this;
            }

            var zoomBtn = minimap.Find("ZoomBtn");
            if (zoomBtn != null)
            {
                var img = zoomBtn.GetComponent<Image>();
                var btn = zoomBtn.GetComponent<Button>();
                if (btn == null) btn = zoomBtn.gameObject.AddComponent<Button>();
                if (img != null) btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    var cam = BattleCameraController.Instance;
                    if (cam != null) cam.ZoomBy(-1);
                });
            }
        }

        void BindPortrait()
        {
            var portrait = transform.Find("BattleUI/CommanderPanel/Portrait");
            if (portrait == null) return;
            var img = portrait.GetComponent<Image>();
            var btn = portrait.GetComponent<Button>();
            if (btn == null) btn = portrait.gameObject.AddComponent<Button>();
            if (img != null) btn.targetGraphic = img;
            btn.onClick.AddListener(OnPortraitClick);
            _portraitButton = btn;
        }

        // ---- start battle button (created at runtime) --------------------------

        void CreateStartBattleButton()
        {
            var existing = transform.Find("BattleUI/StartBattleBtn");
            if (existing != null)
            {
                _startBattleGo = existing.gameObject;
                _startBattleButton = existing.GetComponent<Button>();
                if (_startBattleButton == null)
                    _startBattleButton = existing.gameObject.AddComponent<Button>();
                var existingImage = existing.GetComponent<Image>();
                if (existingImage != null) _startBattleButton.targetGraphic = existingImage;
                _startBattleButton.onClick.AddListener(OnStartBattle);
                return;
            }

            var go = new GameObject("StartBattleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 56f);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -28f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.08f, 0.30f, 0.16f, 0.95f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rt, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = ResolveFont(null);
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "开始战斗";
            label.color = new Color(1f, 0.9f, 0.55f, 1f);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnStartBattle);

            _startBattleGo = go;
            _startBattleButton = btn;
        }

        // ---- population --------------------------------------------------------

        void PopulateCommander(CommanderDefinition c)
        {
            if (c == null) return;

            if (_nameText != null) _nameText.text = c.DisplayName;
            if (_healthText != null) _healthText.text = c.CurrentHealth + "/" + c.MaxHealth;

            if (_healthBarBg != null && _healthBarFill != null)
            {
                float frac = c.MaxHealth > 0 ? c.CurrentHealth / (float)c.MaxHealth : 0f;
                var bgSize = _healthBarBg.rect.size;
                // HealthBarFill is a Simple Image: drive its width from the parent's
                // left edge so it reads as a filling bar.
                _healthBarFill.anchorMin = new Vector2(0f, 0.5f);
                _healthBarFill.anchorMax = new Vector2(0f, 0.5f);
                _healthBarFill.pivot = new Vector2(0f, 0.5f);
                _healthBarFill.anchoredPosition = Vector2.zero;
                _healthBarFill.sizeDelta = new Vector2(bgSize.x * Mathf.Clamp01(frac), bgSize.y);
            }

            for (int i = 0; i < _traitRoots.Length; i++)
            {
                var root = _traitRoots[i];
                if (root == null) continue;
                bool has = c.Traits != null && i < c.Traits.Count;
                root.gameObject.SetActive(has);
                if (!has) continue;

                var label = root.Find("TraitLabel")?.GetComponent<TextMeshProUGUI>();
                if (label == null)
                {
                    var go = new GameObject("TraitLabel");
                    go.transform.SetParent(root, false);
                    label = go.AddComponent<TextMeshProUGUI>();
                    label.font = ResolveFont(null);
                    label.fontSize = 13f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = new Color(1f, 0.9f, 0.55f, 1f);
                    var lrt = go.GetComponent<RectTransform>();
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = Vector2.zero;
                    lrt.offsetMax = Vector2.zero;
                }
                label.text = c.Traits[i];
            }
        }

        void PopulateCardBar(CommanderDefinition c)
        {
            for (int i = 0; i < _cardNames.Length; i++)
            {
                bool has = c != null && c.StartingUnits != null && i < c.StartingUnits.Count;
                if (!has)
                {
                    if (_cardNames[i] != null) { _cardNames[i].text = "待解锁"; _cardNames[i].color = new Color(0.8f, 0.8f, 0.8f, 0.6f); }
                    if (_cardCounts[i] != null) { _cardCounts[i].text = "—"; }
                    if (_cardBadges[i] != null) _cardBadges[i].color = new Color(1f, 1f, 1f, 0.3f);
                    continue;
                }

                var entry = c.StartingUnits[i];
                if (_cardNames[i] != null) { _cardNames[i].text = UnitDisplayName(entry.UnitType); _cardNames[i].color = Color.white; }
                if (_cardCounts[i] != null) _cardCounts[i].text = "×" + entry.Count;
                if (_cardBadges[i] != null) _cardBadges[i].color = Color.white;
            }
        }

        // ---- interactions ------------------------------------------------------

        void OnPortraitClick()
        {
            var fc = FormationController.Instance;
            if (fc != null) fc.ToggleDeploy();

            var status = BattleUiStatusText.Instance;
            if (status != null)
            {
                status.SetStatus(fc != null && fc.IsDeploying
                    ? "部署中：点击阵型按钮切换，点击单位再点击格子调整站位"
                    : "部署范围已关闭");
            }
        }

        void OnFormationClick(int index, FormationType type)
        {
            var fc = FormationController.Instance;
            if (fc != null) fc.SetFormation(type);
            RefreshFormationHighlight();

            var status = BattleUiStatusText.Instance;
            if (status != null)
            {
                string name = index == 0 ? "竖向" : index == 1 ? "横向" : "方形";
                status.SetStatus("当前阵型：" + name);
            }
        }

        void OnStartBattle()
        {
            if (BattlePhaseState.Current == BattlePhase.Combat) return;
            BattlePhaseState.StartCombat();

            var fc = FormationController.Instance;
            if (fc != null) fc.ExitDeployMode();

            RefreshPhaseUi();

            var status = BattleUiStatusText.Instance;
            if (status != null)
                status.SetStatus("战斗开始：点击己方单位 → 点击地面移动 / 点击敌人攻击");

            Debug.Log("[BattleUI] Combat phase started.");
        }

        void RefreshPhaseUi()
        {
            bool deploying = BattlePhaseState.Current == BattlePhase.Deployment;
            if (_startBattleGo != null) _startBattleGo.SetActive(deploying);
        }

        void RefreshFormationHighlight()
        {
            var fc = FormationController.Instance;
            if (fc == null) return;
            FormationType current = fc.CurrentFormation;

            for (int i = 0; i < _formationImages.Length; i++)
            {
                if (_formationImages[i] == null) continue;
                FormationType type = i == 0 ? FormationType.Vertical
                    : i == 1 ? FormationType.Horizontal
                    : FormationType.Square;
                _formationImages[i].color = type == current
                    ? Color.white
                    : new Color(0.88f, 0.88f, 0.88f, 0.96f);
            }
        }

        // ---- minimap -----------------------------------------------------------

        /// <summary>Converts a MapArea click position to a grid cell and recentres the camera.</summary>
        public void OnMiniMapClick(RectTransform rect, Vector2 screenPos)
        {
            var cam = BattleCameraController.Instance;
            var grid = BattleGridController.Instance;
            if (cam == null || grid == null || rect == null) return;

            var canvas = GetComponent<Canvas>();
            Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, uiCam, out local))
                return;

            var size = rect.rect.size;
            if (size.x <= 0.001f || size.y <= 0.001f) return;

            float nx = Mathf.Clamp01((local.x / size.x) + rect.pivot.x);
            float ny = Mathf.Clamp01((local.y / size.y) + rect.pivot.y);

            int cx = Mathf.RoundToInt(nx * (grid.Width - 1));
            int cy = Mathf.RoundToInt(ny * (grid.Height - 1));
            cam.FocusOn(new Vector2Int(cx, cy));
        }

        // ---- helpers -----------------------------------------------------------

        IEnumerator AutoDeploy()
        {
            // Let the spawner / controllers run first (Start order is unspecified).
            yield return null;
            yield return null;
            if (BattlePhaseState.Current != BattlePhase.Deployment) yield break;
            var fc = FormationController.Instance;
            if (fc != null) fc.EnterDeployMode();

            var status = BattleUiStatusText.Instance;
            if (status != null)
                status.SetStatus("部署阶段：点击头像调整阵型，点击“开始战斗”进入战斗");
        }

        static TMP_FontAsset ResolveFont(TMP_FontAsset preferred)
        {
            if (preferred != null) return preferred;
            if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
            return null;
        }

        static string UnitDisplayName(UnitType type)
        {
            switch (type)
            {
                case UnitType.Infantry: return "步兵";
                case UnitType.Tank: return "坦克";
                default: return type.ToString();
            }
        }

        /// <summary>Receives clicks on the minimap MapArea and forwards them to the controller.</summary>
        sealed class MiniMapClick : MonoBehaviour, IPointerClickHandler
        {
            public BattleUiController Owner;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (Owner != null)
                    Owner.OnMiniMapClick(transform as RectTransform, eventData.position);
            }
        }
    }
}
