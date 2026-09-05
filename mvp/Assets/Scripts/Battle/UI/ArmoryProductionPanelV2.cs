using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mvp.Battle.Buildings;
using Mvp.Battle.Commanders;
using Mvp.Battle.Economy;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// Replaces the temporary armory panel while preserving its existing call sites.
    /// </summary>
    public sealed class ArmoryUiReplacementBridge : MonoBehaviour
    {
        static readonly FieldInfo BuildingField = typeof(ArmoryProductionPanel).GetField(
            "_building", BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindObjectOfType<ArmoryUiReplacementBridge>() != null) return;
            new GameObject("ArmoryUiReplacementBridge").AddComponent<ArmoryUiReplacementBridge>();
        }

        void LateUpdate()
        {
            var oldPanel = FindObjectOfType<ArmoryProductionPanel>();
            if (oldPanel == null || !oldPanel.gameObject.activeInHierarchy) return;
            var building = BuildingField != null
                ? BuildingField.GetValue(oldPanel) as BuildingRuntime : null;
            oldPanel.gameObject.SetActive(false);
            if (building != null) ArmoryProductionPanelV2.Show(building);
        }
    }

    /// <summary>
    /// Data-driven armory UI. Unit production execution is deliberately not simulated here.
    /// </summary>
    public sealed class ArmoryProductionPanelV2 : MonoBehaviour
    {
        static readonly Color Shade = new Color(0.015f, 0.02f, 0.03f, 0.84f);
        static readonly Color DarkWood = new Color(0.19f, 0.08f, 0.025f, 1f);
        static readonly Color Wood = new Color(0.38f, 0.17f, 0.055f, 1f);
        static readonly Color WoodLight = new Color(0.62f, 0.34f, 0.12f, 1f);
        static readonly Color Paper = new Color(0.88f, 0.76f, 0.56f, 1f);
        static readonly Color PaperDark = new Color(0.66f, 0.49f, 0.28f, 1f);
        static readonly Color Ink = new Color(0.20f, 0.085f, 0.03f, 1f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.23f, 1f);
        static readonly Color Selected = new Color(0.86f, 0.46f, 0.10f, 1f);
        static readonly Color Disabled = new Color(0.29f, 0.25f, 0.22f, 1f);

        static ArmoryProductionPanelV2 _instance;

        readonly List<UnitRow> _rows = new List<UnitRow>();
        readonly List<GroupChip> _groups = new List<GroupChip>();

        BuildingRuntime _building;
        UnitDefinition _selectedUnit;
        CommanderGroupRuntime _targetGroup;
        Transform _unitList;
        Transform _groupList;
        TextMeshProUGUI _gold;
        TextMeshProUGUI _unitName;
        TextMeshProUGUI _stats;
        TextMeshProUGUI _relations;
        TextMeshProUGUI _groupSummary;
        TextMeshProUGUI _status;
        Button _confirm;
        TextMeshProUGUI _confirmText;

        sealed class UnitRow
        {
            public UnitDefinition Definition;
            public Image Background;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Price;
        }

        sealed class GroupChip
        {
            public CommanderGroupRuntime Group;
            public Image Background;
            public TextMeshProUGUI Label;
        }

        public static void Show(BuildingRuntime building)
        {
            if (building == null) return;
            if (_instance == null) CreateInstance();
            if (_instance != null) _instance.Open(building);
        }

        static void CreateInstance()
        {
            var root = new GameObject("ArmoryProductionCanvasV2");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32600;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var go = new GameObject("ArmoryProductionPanelV2", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            _instance = go.AddComponent<ArmoryProductionPanelV2>();
            _instance.Build();
            go.SetActive(false);
        }

        void OnDestroy()
        {
            var economy = BattleEconomyController.Instance;
            if (economy != null) economy.GoldChanged -= OnGoldChanged;
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void Build()
        {
            Stretch(GetComponent<RectTransform>());
            Rect(transform, "Shade", Shade, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, false);

            var frame = Rect(transform, "Frame", DarkWood,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1420f, 820f), true);
            Outline(frame.gameObject, new Color(0.78f, 0.50f, 0.20f, 1f), 4f);

            BuildHeader(frame);
            BuildLeft(frame);
            BuildRight(frame);

            var economy = BattleEconomyController.Instance;
            if (economy != null) economy.GoldChanged += OnGoldChanged;
        }

        void BuildHeader(RectTransform frame)
        {
            var header = Rect(frame, "Header", Wood,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -45f), new Vector2(-24f, 70f), true);
            Outline(header.gameObject, WoodLight, 2f);

            var coinSprite = Resources.Load<Sprite>("SettlementShop/Generated/coin_icon");
            var coin = SpriteImage(header, "Coin", coinSprite, Gold);
            Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(42f, 42f));
            coin.rectTransform.pivot = new Vector2(0f, 0.5f);
            coin.preserveAspect = true;

            _gold = Label(header, "Gold", 26f, FontStyles.Bold,
                TextAlignmentOptions.Left, Gold);
            Place(_gold.rectTransform, new Vector2(0f, 0f), new Vector2(0.28f, 1f),
                new Vector2(70f, 0f), Vector2.zero);

            var title = Label(header, "Title", 33f, FontStyles.Bold,
                TextAlignmentOptions.Center, Gold);
            title.text = "兵工厂 · 单位生产";
            Place(title.rectTransform, new Vector2(0.28f, 0f), new Vector2(0.72f, 1f),
                Vector2.zero, Vector2.zero);

            var close = Command(header, "Close", "×", WoodLight, Gold, Close);
            Place(close.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(52f, 46f));
            close.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
        }

        void BuildLeft(RectTransform frame)
        {
            var left = Rect(frame, "UnitListPanel", Wood,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(286f, -53f), new Vector2(548f, 690f), true);
            Outline(left.gameObject, WoodLight, 2f);

            var heading = Label(left, "Heading", 23f, FontStyles.Bold,
                TextAlignmentOptions.Center, Gold);
            heading.text = "可生产单位";
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -28f), new Vector2(-24f, 42f));

            var viewport = Rect(left, "Viewport", new Color(0.12f, 0.045f, 0.015f, 0.68f),
                Vector2.zero, Vector2.one, new Vector2(0f, -24f), new Vector2(-30f, -80f), false);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport, false);
            _unitList = content.transform;
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 7f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = left.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
        }

        void BuildRight(RectTransform frame)
        {
            var right = Rect(frame, "DetailsPanel", Paper,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(292f, -53f), new Vector2(-600f, -130f), true);
            Outline(right.gameObject, PaperDark, 3f);

            var preview = Rect(right, "Preview", new Color(0.16f, 0.13f, 0.11f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(216f, -150f), new Vector2(386f, 254f), true);
            Outline(preview.gameObject, PaperDark, 2f);

            _unitName = Label(preview, "UnitName", 29f, FontStyles.Bold,
                TextAlignmentOptions.Center, Gold);
            Place(_unitName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -28f), new Vector2(-24f, 44f));

            var model = Label(preview, "ModelPlaceholder", 21f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Color(0.76f, 0.68f, 0.56f, 1f));
            model.text = "单位模型预览\n（暂未接入）";
            Place(model.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(0f, -20f), new Vector2(-32f, -76f));

            var statsPanel = Rect(right, "Stats", new Color(0.77f, 0.62f, 0.40f, 0.78f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(216f, -150f), new Vector2(-454f, 254f), true);
            Outline(statsPanel.gameObject, PaperDark, 2f);
            _stats = Label(statsPanel, "StatsText", 21f, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, Ink);
            Place(_stats.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-42f, -30f));

            var relationsPanel = Rect(right, "Relations", new Color(0.94f, 0.85f, 0.68f, 0.9f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -366f), new Vector2(-44f, 144f), true);
            Outline(relationsPanel.gameObject, PaperDark, 2f);
            _relations = Label(relationsPanel, "RelationsText", 19f, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, Ink);
            Place(_relations.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-40f, -24f));

            var target = Rect(right, "TargetGroup", new Color(0.72f, 0.55f, 0.34f, 0.78f),
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 156f), new Vector2(-44f, 148f), true);
            Outline(target.gameObject, PaperDark, 2f);

            var targetTitle = Label(target, "TargetTitle", 19f, FontStyles.Bold,
                TextAlignmentOptions.Left, Ink);
            targetTitle.text = "加入己方指挥官编队";
            Place(targetTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -20f), new Vector2(-32f, 30f));

            var groupRoot = new GameObject("GroupList", typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            groupRoot.transform.SetParent(target, false);
            _groupList = groupRoot.transform;
            Place(groupRoot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 8f), new Vector2(-30f, -68f));
            var groupLayout = groupRoot.GetComponent<HorizontalLayoutGroup>();
            groupLayout.spacing = 8f;
            groupLayout.childControlWidth = true;
            groupLayout.childForceExpandWidth = true;
            groupLayout.childControlHeight = true;

            _groupSummary = Label(target, "GroupSummary", 16f, FontStyles.Normal,
                TextAlignmentOptions.Left, Ink);
            Place(_groupSummary.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 12f), new Vector2(-32f, 26f));

            _status = Label(right, "Status", 16f, FontStyles.Normal,
                TextAlignmentOptions.Left, Ink);
            Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 0f),
                new Vector2(0f, 37f), new Vector2(-10f, 52f));

            _confirm = Command(right, "Confirm", "确认", WoodLight, Gold, ConfirmSelection);
            Place(_confirm.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-220f, 28f), new Vector2(174f, 58f));
            _confirm.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            _confirmText = _confirm.GetComponentInChildren<TextMeshProUGUI>();

            var cancel = Command(right, "Cancel", "取消", Disabled,
                new Color(0.92f, 0.80f, 0.62f, 1f), Close);
            Place(cancel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 28f), new Vector2(174f, 58f));
            cancel.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
        }

        void Open(BuildingRuntime building)
        {
            _building = building;
            gameObject.SetActive(true);
            _status.text = "选择单位与目标编队。当前 UI 不会扣除金币。";
            RebuildUnits();
            RebuildGroups();
            Refresh();
        }

        void RebuildUnits()
        {
            for (int i = _unitList.childCount - 1; i >= 0; i--)
                Destroy(_unitList.GetChild(i).gameObject);
            _rows.Clear();
            _selectedUnit = null;

            if (_building == null || _building.Definition == null) return;
            var types = ProductionCatalog.GetUnits(_building.Definition.ProductionCatalogId);
            for (int i = 0; i < types.Length; i++)
            {
                var definition = UnitCatalog.Get(types[i]);
                if (definition != null) AddUnitRow(definition);
            }
            if (_rows.Count > 0) _selectedUnit = _rows[0].Definition;
        }

        void AddUnitRow(UnitDefinition definition)
        {
            var go = new GameObject("Unit_" + definition.Type, typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_unitList, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 68f;
            element.minHeight = 68f;
            var background = go.GetComponent<Image>();
            background.color = WoodLight;
            var button = go.GetComponent<Button>();
            button.targetGraphic = background;
            var selected = definition;
            button.onClick.AddListener(() => SelectUnit(selected));

            var icon = Rect(go.transform, "Icon", new Color(0.12f, 0.07f, 0.045f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(50f, 50f), false);
            var glyph = Label(icon, "Glyph", 24f, FontStyles.Bold,
                TextAlignmentOptions.Center, Gold);
            glyph.text = Glyph(definition);
            Stretch(glyph.rectTransform);

            var name = Label(go.transform, "Name", 21f, FontStyles.Bold,
                TextAlignmentOptions.Left, new Color(1f, 0.89f, 0.68f, 1f));
            name.text = definition.DisplayName;
            Place(name.rectTransform, new Vector2(0f, 0.48f), new Vector2(0.60f, 1f),
                new Vector2(70f, -2f), new Vector2(-8f, -4f));

            var meta = Label(go.transform, "Meta", 14f, FontStyles.Normal,
                TextAlignmentOptions.Left, new Color(0.86f, 0.74f, 0.56f, 1f));
            meta.text = Category(definition) + " · " + definition.ProductionSeconds.ToString("0") + "秒";
            Place(meta.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 0.48f),
                new Vector2(70f, 2f), new Vector2(-8f, 0f));

            var price = Label(go.transform, "Price", 19f, FontStyles.Bold,
                TextAlignmentOptions.Center, Gold);
            price.text = definition.Cost + " 金币";
            Place(price.rectTransform, new Vector2(0.68f, 0f), Vector2.one,
                Vector2.zero, new Vector2(-10f, -8f));

            _rows.Add(new UnitRow
            {
                Definition = definition,
                Background = background,
                Name = name,
                Price = price
            });
        }

        void RebuildGroups()
        {
            for (int i = _groupList.childCount - 1; i >= 0; i--)
                Destroy(_groupList.GetChild(i).gameObject);
            _groups.Clear();
            _targetGroup = null;

            var registry = CommanderGroupRegistry.Instance;
            if (registry == null) return;
            for (int i = 0; i < registry.Groups.Count; i++)
            {
                var group = registry.Groups[i];
                if (group == null || group.Team != TeamId.Player || group.IsDefeated) continue;
                AddGroup(group);
                if (group == registry.ActiveGroup) _targetGroup = group;
            }
            if (_targetGroup == null && _groups.Count > 0) _targetGroup = _groups[0].Group;
        }

        void AddGroup(CommanderGroupRuntime group)
        {
            var go = new GameObject("Group_" + group.GroupId, typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_groupList, false);
            go.GetComponent<LayoutElement>().preferredHeight = 48f;
            var background = go.GetComponent<Image>();
            background.color = PaperDark;
            var button = go.GetComponent<Button>();
            button.targetGraphic = background;
            var selected = group;
            button.onClick.AddListener(() => SelectGroup(selected));
            var label = Label(go.transform, "Label", 16f, FontStyles.Bold,
                TextAlignmentOptions.Center, Ink);
            Stretch(label.rectTransform);
            _groups.Add(new GroupChip { Group = group, Background = background, Label = label });
        }

        void SelectUnit(UnitDefinition unit)
        {
            _selectedUnit = unit;
            _status.text = "已选择 " + unit.DisplayName;
            Refresh();
        }

        void SelectGroup(CommanderGroupRuntime group)
        {
            _targetGroup = group;
            _status.text = "目标编队：" + GroupName(group);
            Refresh();
        }

        void Refresh()
        {
            int gold = BattleEconomyController.Instance != null
                ? BattleEconomyController.Instance.PlayerGold : 0;
            _gold.text = "金币  " + gold;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool selected = row.Definition == _selectedUnit;
                bool affordable = gold >= row.Definition.Cost;
                row.Background.color = selected ? Selected : affordable ? WoodLight : Disabled;
                row.Name.color = affordable ? new Color(1f, 0.89f, 0.68f, 1f) : PaperDark;
                row.Price.color = affordable ? Gold : new Color(0.87f, 0.34f, 0.24f, 1f);
            }

            RefreshDetails();
            RefreshGroupState();
            bool canAfford = _selectedUnit != null && gold >= _selectedUnit.Cost;
            bool hasSpace = _targetGroup != null && _targetGroup.AliveMemberCount < 9;
            _confirm.interactable = _selectedUnit != null && canAfford && hasSpace;
            _confirmText.text = _selectedUnit == null ? "请选择单位" :
                !canAfford ? "金币不足" : !hasSpace ? "编队已满" : "确认";
        }

        void RefreshDetails()
        {
            if (_selectedUnit == null)
            {
                _unitName.text = "未选择单位";
                _stats.text = string.Empty;
                _relations.text = string.Empty;
                return;
            }

            var unit = _selectedUnit;
            _unitName.text = unit.DisplayName;
            _stats.text =
                "类型：" + Category(unit) + "\n" +
                "生命：" + unit.MaxHealth + "\n" +
                "速度：" + unit.MoveSpeed.ToString("0.0") + " 格/秒\n" +
                "视野：" + unit.VisionRange + " 格\n" +
                "攻击：" + unit.AttackPower + "\n" +
                "射程：" + Range(unit) + "\n" +
                "攻速：" + unit.AttackCooldown.ToString("0.0") + " 秒/次";

            string features = (unit.Tags & UnitTag.CanCaptureBuilding) != 0
                ? "可占领建筑" : "不可占领建筑";
            if ((unit.Tags & UnitTag.Scout) != 0) features += " · 侦察视野";
            if (unit.AreaRadius > 0f) features += " · 范围伤害 " + unit.AreaRadius.ToString("0.#") + "格";
            _relations.text = "克制单位：" + CounterNames(unit) + "\n" +
                "特性：" + features + "\n" +
                "生产耗时：" + unit.ProductionSeconds.ToString("0") + " 秒";
        }

        void RefreshGroupState()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                var chip = _groups[i];
                bool selected = chip.Group == _targetGroup;
                bool full = chip.Group.AliveMemberCount >= 9;
                chip.Background.color = selected ? Selected : full ? Disabled : PaperDark;
                chip.Label.color = selected ? Color.white : Ink;
                chip.Label.text = GroupName(chip.Group) + "  " + chip.Group.AliveMemberCount + "/9";
            }
            _groupSummary.text = _targetGroup == null ? "没有可用的己方编队" :
                "当前目标：" + GroupName(_targetGroup) + "，容量 " +
                _targetGroup.AliveMemberCount + "/9";
        }

        void ConfirmSelection()
        {
            if (_selectedUnit == null || _targetGroup == null) return;
            _status.text = "已确认：" + _selectedUnit.DisplayName + " → " +
                GroupName(_targetGroup) + "。生产队列接入后将在此正式下单。";
            var battleStatus = BattleUiStatusText.Instance;
            if (battleStatus != null) battleStatus.SetStatus("兵工厂选择已确认，当前未扣除金币");
        }

        void OnGoldChanged(TeamId team, int value)
        {
            if (team == TeamId.Player && gameObject.activeSelf) Refresh();
        }

        void Close()
        {
            gameObject.SetActive(false);
        }

        static string GroupName(CommanderGroupRuntime group)
        {
            if (group == null) return "未知编队";
            return group.Definition != null && !string.IsNullOrEmpty(group.Definition.DisplayName)
                ? group.Definition.DisplayName : "指挥官 " + (group.RosterIndex + 1);
        }

        static string Glyph(UnitDefinition unit)
        {
            if ((unit.Tags & UnitTag.LongRangeMechanical) != 0) return "远";
            if ((unit.Tags & UnitTag.CloseMechanical) != 0) return "甲";
            if ((unit.Tags & UnitTag.Scout) != 0) return "侦";
            if ((unit.Tags & UnitTag.Vehicle) != 0) return "车";
            return "步";
        }

        static string Category(UnitDefinition unit)
        {
            if ((unit.Tags & UnitTag.LongRangeMechanical) != 0) return "远程机械";
            if ((unit.Tags & UnitTag.CloseMechanical) != 0) return "近程机械";
            if ((unit.Tags & UnitTag.Scout) != 0 && (unit.Tags & UnitTag.Vehicle) != 0) return "视野车辆";
            if ((unit.Tags & UnitTag.Scout) != 0) return "视野步兵";
            if ((unit.Tags & UnitTag.Vehicle) != 0) return "车辆";
            return "步系兵种";
        }

        static string Range(UnitDefinition unit)
        {
            return unit.AttackRangeMin > 0f
                ? unit.AttackRangeMin.ToString("0.#") + "-" + unit.AttackRangeMax.ToString("0.#") + " 格"
                : unit.AttackRangeMax.ToString("0.#") + " 格";
        }

        static string CounterNames(UnitDefinition unit)
        {
            if (unit.CounterTargets == null || unit.CounterTargets.Length == 0) return "无明确克制";
            var names = new List<string>();
            for (int i = 0; i < unit.CounterTargets.Length; i++)
            {
                var target = UnitCatalog.Get(unit.CounterTargets[i]);
                names.Add(target != null ? target.DisplayName : unit.CounterTargets[i].ToString());
            }
            return string.Join("、", names.ToArray()) +
                "（×" + unit.CounterDamageMultiplier.ToString("0.##") + "）";
        }

        static RectTransform Rect(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Place(rt, anchorMin, anchorMax, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rt;
        }

        static Image SpriteImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Label(Transform parent, string name, float size,
            FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        static Button Command(Transform parent, string name, string text, Color bg,
            Color fg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = bg;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            Outline(go, new Color(0.16f, 0.065f, 0.02f, 0.95f), 2f);
            var label = Label(go.transform, "Label", 22f, FontStyles.Bold,
                TextAlignmentOptions.Center, fg);
            label.text = text;
            Stretch(label.rectTransform);
            return button;
        }

        static void Outline(GameObject go, Color color, float distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        static void Stretch(RectTransform rt)
        {
            Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
