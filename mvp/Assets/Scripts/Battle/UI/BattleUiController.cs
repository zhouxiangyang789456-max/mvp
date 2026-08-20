using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mvp.Battle.Formation;
using Mvp.Battle.Map;
using Mvp.CommanderSelect;
using Mvp.Shared;
using Mvp.Battle.Commanders;

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
        GameObject _commanderPanel;
        GameObject _cardBar;
        GameObject _formationPanel;
        Image _commanderPortrait;
        RectTransform _healthBarBg;
        RectTransform _healthBarFill;
        RectTransform[] _traitRoots = new RectTransform[4];
        Button _portraitButton;
        GameObject _startBattleGo;
        Button _startBattleButton;
        GameObject _editFormationGo;
        GameObject _confirmFormationGo;
        GameObject _cancelFormationGo;

        readonly TextMeshProUGUI[] _cardNames = new TextMeshProUGUI[6];
        readonly TextMeshProUGUI[] _cardCounts = new TextMeshProUGUI[6];
        readonly Image[] _cardBadges = new Image[6];
        readonly Button[] _cardButtons = new Button[6];
        readonly int[] _cardCycleIndices = new int[6];

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
            if (CommanderGroupRegistry.Instance != null)
            {
                CommanderGroupRegistry.Instance.ActiveGroupChanged -= OnActiveGroupChanged;
                CommanderGroupRegistry.Instance.CommanderInspected -= OnCommanderInspected;
                CommanderGroupRegistry.Instance.CommanderInspectionClosed -= HideCommanderContext;
            }
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
            CreateCombatFormationControls();
            BindMiniMap();
            BindPortrait();
            CreateStartBattleButton();

            PopulateCommander(commander);
            PopulateCardBar(commander);
            HideCommanderContext();
            RefreshFormationHighlight();
            RefreshPhaseUi();

            StartCoroutine(AutoDeploy());
            StartCoroutine(BindCommanderGroups());
        }

        IEnumerator BindCommanderGroups()
        {
            for (int i = 0; i < 10 && CommanderGroupRegistry.Instance == null; i++)
                yield return null;
            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) yield break;
            registry.ActiveGroupChanged -= OnActiveGroupChanged;
            registry.ActiveGroupChanged += OnActiveGroupChanged;
            registry.CommanderInspected -= OnCommanderInspected;
            registry.CommanderInspected += OnCommanderInspected;
            registry.CommanderInspectionClosed -= HideCommanderContext;
            registry.CommanderInspectionClosed += HideCommanderContext;
            OnActiveGroupChanged(registry.ActiveGroup);
        }

        void OnCommanderInspected(CommanderGroupRuntime group)
        {
            var existingEdit = FormationController.Instance;
            if (existingEdit != null && existingEdit.IsCombatEditing)
                existingEdit.CancelCombatEdit();
            OnActiveGroupChanged(group);
            SetCommanderContextVisible(true);
            if (BattlePhaseState.Current == BattlePhase.Deployment)
            {
                var formation = FormationController.Instance;
                if (formation != null)
                {
                    formation.ExitDeployMode();
                    formation.EnterDeployMode();
                }
            }
            RefreshCombatFormationControls();
        }

        void HideCommanderContext()
        {
            SetCommanderContextVisible(false);
            var formation = FormationController.Instance;
            if (formation != null)
            {
                formation.ExitDeployMode();
                formation.CancelCombatEdit();
            }
            RefreshCombatFormationControls();
        }

        void SetCommanderContextVisible(bool visible)
        {
            if (_commanderPanel != null) _commanderPanel.SetActive(visible);
            if (_cardBar != null) _cardBar.SetActive(visible);
            if (_formationPanel != null) _formationPanel.SetActive(visible);
        }

        void CreateCombatFormationControls()
        {
            if (_formationPanel == null) return;
            _editFormationGo = CreateFormationCommandButton("EditFormationBtn", "调整阵型", OnBeginCombatFormationEdit);
            _confirmFormationGo = CreateFormationCommandButton("ConfirmFormationBtn", "确认重整", OnConfirmCombatFormationEdit);
            _cancelFormationGo = CreateFormationCommandButton("CancelFormationBtn", "取消", OnCancelCombatFormationEdit);
            RefreshCombatFormationControls();
        }

        GameObject CreateFormationCommandButton(string name, string text, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_formationPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(116f, 48f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.05f, 0.20f, 0.24f, 0.96f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 48f;
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.font = ResolveFont(null);
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.86f, 0.48f, 1f);
            label.text = text;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            return go;
        }

        public void RefreshCombatFormationControls()
        {
            bool combat = BattlePhaseState.Current == BattlePhase.Combat;
            var formation = FormationController.Instance;
            bool editing = formation != null && formation.IsCombatEditing;
            if (_editFormationGo != null) _editFormationGo.SetActive(combat && !editing);
            if (_confirmFormationGo != null) _confirmFormationGo.SetActive(combat && editing);
            if (_cancelFormationGo != null) _cancelFormationGo.SetActive(combat && editing);
            for (int i = 0; i < _formationButtons.Length; i++)
                if (_formationButtons[i] != null) _formationButtons[i].interactable = !combat || editing;
            var panelRect = _formationPanel != null ? _formationPanel.GetComponent<RectTransform>() : null;
            if (panelRect != null)
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, combat ? (editing ? 336f : 276f) : 220f);
        }

        void OnActiveGroupChanged(CommanderGroupRuntime group)
        {
            if (group == null || group.Definition == null)
            {
                HideCommanderContext();
                return;
            }
            PopulateCommander(group.Definition);
            PopulateCardBar(group.Definition);
            var formation = FormationController.Instance;
            if (formation != null) formation.SyncFormationContext(group.Formation);
            RefreshFormationHighlight();

            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus("当前指挥官：" + group.Definition.DisplayName);
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
            _commanderPanel = panel.gameObject;

            _nameText = panel.Find("Name")?.GetComponent<TextMeshProUGUI>();
            _commanderPortrait = panel.Find("Portrait")?.GetComponent<Image>();
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
            var cardBar = transform.Find("BattleUI/CardBar");
            _cardBar = cardBar != null ? cardBar.gameObject : null;
            for (int i = 0; i < _cardNames.Length; i++)
            {
                var slot = transform.Find("BattleUI/CardBar/CardSlot" + (i + 1));
                if (slot == null) continue;
                _cardNames[i] = slot.Find("Name")?.GetComponent<TextMeshProUGUI>();
                _cardCounts[i] = slot.Find("Count")?.GetComponent<TextMeshProUGUI>();
                _cardBadges[i] = slot.Find("Badge")?.GetComponent<Image>();
                var image = slot.GetComponent<Image>();
                var button = slot.GetComponent<Button>();
                if (button == null) button = slot.gameObject.AddComponent<Button>();
                if (image != null) button.targetGraphic = image;
                int cardIndex = i;
                button.onClick.AddListener(() => OnUnitCardClick(cardIndex));
                _cardButtons[i] = button;
            }
        }

        void BindFormationButtons()
        {
            var formationPanel = transform.Find("BattleUI/FormationPanel");
            _formationPanel = formationPanel != null ? formationPanel.gameObject : null;
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
            if (_commanderPortrait != null)
            {
                _commanderPortrait.sprite = Resources.Load<Sprite>(c.PortraitAssetId);
                _commanderPortrait.preserveAspect = true;
            }
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

        void OnBeginCombatFormationEdit()
        {
            var registry = CommanderGroupRegistry.Instance;
            var group = registry != null ? registry.ActiveGroup : null;
            var formation = FormationController.Instance;
            string reason = null;
            if (formation == null || !formation.BeginCombatEdit(group, out reason))
            {
                var failedStatus = BattleUiStatusText.Instance;
                if (failedStatus != null) failedStatus.SetStatus(reason ?? "当前无法调整阵型");
                return;
            }
            var selection = Mvp.Battle.Units.UnitSelectionController.Instance;
            if (selection != null) selection.ClearSelection();
            RefreshCombatFormationControls();
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus("阵型编辑：选择单位后点击 3×3 格，完成后确认重整");
        }

        void OnConfirmCombatFormationEdit()
        {
            var formation = FormationController.Instance;
            string reason = null;
            if (formation == null || !formation.ConfirmCombatEdit(out reason))
            {
                var failedStatus = BattleUiStatusText.Instance;
                if (failedStatus != null) failedStatus.SetStatus(reason ?? "重整失败");
                return;
            }
            var selection = Mvp.Battle.Units.UnitSelectionController.Instance;
            if (selection != null) selection.ClearSelection();
            RefreshFormationHighlight();
            RefreshCombatFormationControls();
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus("编队开始按新阵型重整");
        }

        void OnCancelCombatFormationEdit()
        {
            var formation = FormationController.Instance;
            if (formation != null) formation.CancelCombatEdit();
            var selection = Mvp.Battle.Units.UnitSelectionController.Instance;
            if (selection != null) selection.ClearSelection();
            RefreshCombatFormationControls();
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus("已取消阵型修改");
        }

        void OnUnitCardClick(int cardIndex)
        {
            var registry = CommanderGroupRegistry.Instance;
            var group = registry != null ? registry.ActiveGroup : null;
            if (group == null || group.Definition == null ||
                group.Definition.StartingUnits == null || cardIndex < 0 ||
                cardIndex >= group.Definition.StartingUnits.Count) return;

            UnitType type = group.Definition.StartingUnits[cardIndex].UnitType;
            var matches = new System.Collections.Generic.List<Mvp.Battle.Units.UnitView>();
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead ||
                    member.Data.Definition == null || member.Data.Definition.Type != type) continue;
                matches.Add(member);
            }
            if (matches.Count == 0) return;
            int index = _cardCycleIndices[cardIndex] % matches.Count;
            var selection = Mvp.Battle.Units.UnitSelectionController.Instance;
            if (selection != null) selection.Select(matches[index]);
            _cardCycleIndices[cardIndex] = (index + 1) % matches.Count;
        }

        void OnStartBattle()
        {
            if (BattlePhaseState.Current == BattlePhase.Combat) return;
            var fc = FormationController.Instance;
            string validationError;
            if (fc != null && !fc.ValidateAllDeployments(out validationError))
            {
                var invalidStatus = BattleUiStatusText.Instance;
                if (invalidStatus != null) invalidStatus.SetStatus(validationError);
                return;
            }
            if (fc != null) fc.LockAllFormations();
            BattlePhaseState.StartCombat();
            if (Mvp.Battle.Outcome.BattleOutcomeController.Instance != null)
                Mvp.Battle.Outcome.BattleOutcomeController.Instance.NotifyCombatStarted();

            if (fc != null) fc.ExitDeployMode();

            RefreshPhaseUi();

            var status = BattleUiStatusText.Instance;
            if (status != null)
                status.SetStatus("战斗开始：点击指挥官头像或其单位选组，再点击地面移动 / 敌人攻击");

            Debug.Log("[BattleUI] Combat phase started.");
        }

        void RefreshPhaseUi()
        {
            bool deploying = BattlePhaseState.Current == BattlePhase.Deployment;
            if (_startBattleGo != null) _startBattleGo.SetActive(deploying);
            RefreshCombatFormationControls();
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

            var status = BattleUiStatusText.Instance;
            if (status != null)
                status.SetStatus("部署阶段：点击地图上的指挥官头像或旗下单位进行选择");
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
