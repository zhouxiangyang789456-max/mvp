using Mvp.CommanderSelect;
using Mvp.Progression;
using Mvp.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#endif

namespace Mvp.SettlementShop
{
    public sealed class SettlementShopController : MonoBehaviour
    {
        const int RefreshPrice = 2;
        readonly SettlementShopTransactionService _service = new SettlementShopTransactionService();
        TMP_FontAsset _font;
        TextMeshProUGUI _goldText;
        TextMeshProUGUI _statusText;
        TextMeshProUGUI _rewardText;
        RectTransform _inventoryItems;
        RectTransform _offersRoot;
        RectTransform _commandersRoot;
        RectTransform _modalRoot;
        Sprite _panelSprite, _offerCardSprite, _sellSprite, _inventorySprite, _inventoryPlainSprite;
        Sprite _buttonSprite, _confirmButtonSprite, _closeButtonSprite, _hintPanelSprite;
        Sprite _scrollbarTrackSprite, _scrollbarThumbSprite;
        Sprite _coinIconSprite, _topGoldIconSprite, _coinBagSprite, _cardsIconSprite, _refreshIconSprite, _teamLabelSprite;
        Sprite _teamChipSprite, _traitCellSprite, _traitCellSelectedSprite;
        string _selectedInventoryCardId;
        bool _committed;
        readonly List<string> _visibleCardIds = new List<string>(16);

        public static SettlementShopController OpenShop(SettlementShopOpenArgs args)
        {
            SettlementShopContext.PendingOpenArgs = args;
            if (EventSystem.current == null)
            {
                var events = new GameObject("SettlementEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }
            var existing = FindObjectOfType<SettlementShopController>();
            if (existing != null) return existing;
            return new GameObject("SettlementShop", typeof(RectTransform)).AddComponent<SettlementShopController>();
        }

        void Start()
        {
            _font = Resources.Load<TMP_FontAsset>("Battle/UI/Fonts/SimHei SDF");
            _panelSprite = LoadSprite("shop_panel_v2");
            _offerCardSprite = LoadSprite("offer_card_v2");
            _sellSprite = LoadSprite("sell_zone_v2");
            _inventorySprite = LoadSprite("inventory_card_v2");
            _inventoryPlainSprite = LoadSprite("inventory_card_plain");
            _buttonSprite = LoadSprite("command_button_v2");
            _confirmButtonSprite = LoadSprite("confirm_button");
            _closeButtonSprite = LoadSprite("close_button");
            _hintPanelSprite = LoadSprite("hint_panel");
            _scrollbarTrackSprite = LoadSprite("scrollbar_track");
            _scrollbarThumbSprite = LoadSprite("scrollbar_thumb");
            _coinIconSprite = LoadSprite("coin_icon");
            _topGoldIconSprite = LoadSprite("top_gold_icon");
            _coinBagSprite = LoadSprite("coin_bag");
            _cardsIconSprite = LoadSprite("cards_icon");
            _refreshIconSprite = LoadSprite("refresh_icon");
            _teamLabelSprite = LoadSprite("team_label");
            _teamChipSprite = LoadSprite("team_chip");
            _traitCellSprite = LoadSprite("trait_cell");
            _traitCellSelectedSprite = LoadSprite("trait_cell_selected");
            if (!ValidateVisualResources())
            {
                enabled = false;
                return;
            }
            BuildPage();
            _service.Changed += OnSessionChanged;
            if (!_service.Open(SettlementShopContext.ConsumeOrCreateDefault()))
            {
                SetStatus("无法打开商店会话");
                return;
            }
#if UNITY_EDITOR
            StartCoroutine(CaptureEditorPreview());
#endif
        }

        void OnDestroy()
        {
            _service.Changed -= OnSessionChanged;
            if (!_committed) _service.Suspend();
        }

        Sprite LoadSprite(string name) => Resources.Load<Sprite>("SettlementShop/Generated/" + name);

        bool ValidateVisualResources()
        {
            bool valid = true;
            valid &= RequireSprite(_panelSprite, "shop_panel_v2");
            valid &= RequireSprite(_offerCardSprite, "offer_card_v2");
            valid &= RequireSprite(_sellSprite, "sell_zone_v2");
            valid &= RequireSprite(_inventorySprite, "inventory_card_v2");
            valid &= RequireSprite(_inventoryPlainSprite, "inventory_card_plain");
            valid &= RequireSprite(_buttonSprite, "command_button_v2");
            valid &= RequireSprite(_confirmButtonSprite, "confirm_button");
            valid &= RequireSprite(_closeButtonSprite, "close_button");
            valid &= RequireSprite(_hintPanelSprite, "hint_panel");
            valid &= RequireSprite(_scrollbarTrackSprite, "scrollbar_track");
            valid &= RequireSprite(_scrollbarThumbSprite, "scrollbar_thumb");
            valid &= RequireSprite(_coinIconSprite, "coin_icon");
            valid &= RequireSprite(_topGoldIconSprite, "top_gold_icon");
            valid &= RequireSprite(_coinBagSprite, "coin_bag");
            valid &= RequireSprite(_cardsIconSprite, "cards_icon");
            valid &= RequireSprite(_refreshIconSprite, "refresh_icon");
            valid &= RequireSprite(_teamLabelSprite, "team_label");
            valid &= RequireSprite(_teamChipSprite, "team_chip");
            valid &= RequireSprite(_traitCellSprite, "trait_cell");
            valid &= RequireSprite(_traitCellSelectedSprite, "trait_cell_selected");
            return valid;
        }

        static bool RequireSprite(Sprite sprite, string resourceName)
        {
            if (sprite != null) return true;
            Debug.LogError("[SettlementShop] Missing UI Sprite: SettlementShop/Generated/" + resourceName);
            return false;
        }

        void OnSessionChanged(ShopChangeSet changes, int version)
        {
            if (_service.Session == null) return;
            if (!string.IsNullOrEmpty(_selectedInventoryCardId))
            {
                var selectedCard = _service.Session.GetCard(_selectedInventoryCardId);
                if (selectedCard == null || selectedCard.Location != TraitCardLocation.Inventory)
                    _selectedInventoryCardId = null;
            }
            _goldText.text = _service.Session.Gold.ToString();
            _rewardText.text = "×" + _service.Session.RewardGold;
            RebuildOffers();
            RebuildInventory();
            RebuildCommanders();
        }

        void BuildPage()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32700;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var shade = Panel(transform, "SettlementShade", new Color(0.015f, 0.025f, 0.04f, 0.9f));
            Stretch(shade);
            var main = ImagePanel(shade, "ShopPanel", _panelSprite, new Color(1f, 1f, 1f, 0.99f));
            SetRect(main, new Vector2(0.5f, 1f), new Vector2(1450, 780), new Vector2(0, -16));

            var title = Label(main, "商店", 38, new Vector2(0.5f, 1f), new Vector2(300, 58), new Vector2(0, -50));
            title.color = new Color(0.96f, 0.88f, 0.72f);
            title.fontStyle = FontStyles.Bold;
            AddOutline(title.rectTransform, new Color(0.23f, 0.11f, 0.045f, 0.72f), new Vector2(2f, -2f));

            var close = AddIcon(main, "CloseButton", _closeButtonSprite, new Vector2(1f, 1f), new Vector2(78, 78), new Vector2(-68, -58));
            close.raycastTarget = true;
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;
            closeButton.onClick.AddListener(CloseShop);

            AddIcon(main, "GoldIcon", _topGoldIconSprite, new Vector2(0f, 1f), new Vector2(48, 48), new Vector2(414, -110));
            _goldText = Label(main, "0", 22, new Vector2(0f, 1f), new Vector2(70, 36), new Vector2(465, -116));
            _goldText.alignment = TextAlignmentOptions.Left;

            var inventory = Panel(main, "Inventory", Color.clear);
            SetRect(inventory, new Vector2(0f, 0.5f), new Vector2(310, 640), new Vector2(34, -4));
            AddIcon(inventory, "CardsIcon", _cardsIconSprite, new Vector2(0.18f, 1f), new Vector2(64, 64), new Vector2(18, -56));
            var inventoryTitle = Label(inventory, "我的卡牌", 26, new Vector2(0.58f, 1f), new Vector2(190, 44), new Vector2(22, -58));
            inventoryTitle.color = new Color(0.94f, 0.87f, 0.72f);
            var inventoryDrop = inventory.gameObject.AddComponent<ShopDropTarget>();
            inventoryDrop.Configure(this, ShopDropTargetType.Inventory, null, -1);
            BuildInventoryScroll(inventory);

            _offersRoot = Panel(main, "Offers", Color.clear);
            SetRect(_offersRoot, new Vector2(0f, 0.56f), new Vector2(780, 520), new Vector2(350, 26));

            var sell = ImagePanel(main, "SellZone", _sellSprite, Color.white);
            SetRect(sell, new Vector2(1f, 0.56f), new Vector2(190, 390), new Vector2(-40, 12));
            var sellHeader = ImagePanel(sell, "SellHeader", _hintPanelSprite, Color.white);
            SetRect(sellHeader, new Vector2(0.5f, 1f), new Vector2(190, 54), new Vector2(-12, 22));
            var sellHeaderText = Label(sellHeader, "拖至此处售卖", 16, new Vector2(0.5f, 0.5f), new Vector2(180, 42), Vector2.zero);
            sellHeaderText.color = new Color(0.94f, 0.87f, 0.72f);
            AddIcon(sell, "CoinBag", _coinBagSprite, new Vector2(0.5f, 0.5f), new Vector2(132, 132), new Vector2(0, -8));
            sell.gameObject.AddComponent<ShopDropTarget>().Configure(this, ShopDropTargetType.Sell, null, -1);

            StyledButton(main, "确认", new Vector2(0f, 0f), new Vector2(210, 72),
                new Vector2(430, 90), _confirmButtonSprite, Confirm);
            var refreshButton = StyledButton(main, string.Empty, new Vector2(0f, 0f), new Vector2(190, 64),
                new Vector2(670, 94), _buttonSprite, () => Operate(_service.Refresh(RefreshPrice), "刷新"));
            AddIcon(refreshButton.transform, "RefreshIcon", _refreshIconSprite, new Vector2(0.22f, 0.5f), new Vector2(42, 42), Vector2.zero);
            AddIcon(refreshButton.transform, "RefreshCostIcon", _coinIconSprite, new Vector2(0.58f, 0.5f), new Vector2(42, 42), Vector2.zero);
            var refreshCostText = refreshButton.GetComponentInChildren<TextMeshProUGUI>();
            refreshCostText.text = "×" + RefreshPrice;
            SetRect(refreshCostText.rectTransform, new Vector2(0.82f, 0.5f), new Vector2(52, 34), Vector2.zero);
            var rewardButton = StyledButton(main, "×0", new Vector2(0f, 0f),
                new Vector2(190, 64), new Vector2(910, 94), _buttonSprite, ShowRewardSummary);
            AddIcon(rewardButton.transform, "RewardIcon", _coinIconSprite, new Vector2(0.34f, 0.5f), new Vector2(42, 42), Vector2.zero);
            _rewardText = rewardButton.GetComponentInChildren<TextMeshProUGUI>();
            SetRect(_rewardText.rectTransform, new Vector2(0.67f, 0.5f), new Vector2(68, 36), Vector2.zero);
            _statusText = Label(main, string.Empty, 12, new Vector2(0f, 0f),
                new Vector2(460, 24), new Vector2(535, 158));

            _commandersRoot = Panel(shade, "CommanderLoadouts", Color.clear);
            _commandersRoot.anchorMin = new Vector2(0f, 0f);
            _commandersRoot.anchorMax = new Vector2(1f, 0f);
            _commandersRoot.pivot = new Vector2(0.5f, 0f);
            _commandersRoot.sizeDelta = new Vector2(0, 180);
            _commandersRoot.anchoredPosition = new Vector2(0, 10);

            _modalRoot = Panel(shade, "ModalLayer", Color.clear);
            Stretch(_modalRoot);
            _modalRoot.gameObject.SetActive(false);
        }

        void BuildInventoryScroll(RectTransform inventory)
        {
            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(inventory, false);
            SetRect(scroll.GetComponent<RectTransform>(), new Vector2(0.56f, 0f), new Vector2(212, 486), new Vector2(0, 82));
            var viewport = Panel(scroll.transform, "Viewport", Color.clear);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _inventoryItems = Panel(viewport, "Content", Color.clear);
            _inventoryItems.anchorMin = new Vector2(0, 1);
            _inventoryItems.anchorMax = new Vector2(1, 1);
            _inventoryItems.pivot = new Vector2(0.5f, 1);
            _inventoryItems.anchoredPosition = Vector2.zero;
            var sr = scroll.GetComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = _inventoryItems;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 24;

            var scrollbar = ImagePanel(inventory, "Scrollbar", _scrollbarTrackSprite, Color.white);
            SetRect(scrollbar, new Vector2(1f, 0.5f), new Vector2(26, 470), new Vector2(-4, 4));
            var handle = ImagePanel(scrollbar, "Handle", _scrollbarThumbSprite, Color.white);
            SetRect(handle, new Vector2(0.5f, 1f), new Vector2(22, 112), Vector2.zero);
            var sb = scrollbar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle;
            sb.targetGraphic = handle.GetComponent<Image>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        void RebuildOffers()
        {
            ClearChildren(_offersRoot);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var offer = _service.Session.Offers[i];
                var def = TraitCatalog.Get(offer.DefinitionId);
                var card = ImagePanel(_offersRoot, "Offer" + i, _offerCardSprite, Color.white);
                SetRect(card, new Vector2(0.5f, 0.52f), new Vector2(220, 370), new Vector2((i - 1) * 255f, 0));
                var offerTitle = Label(card, def.DisplayName, 20, new Vector2(0.5f, 0.74f), new Vector2(180, 34), Vector2.zero);
                offerTitle.color = new Color(0.22f, 0.12f, 0.06f);
                offerTitle.fontStyle = FontStyles.Bold;
                var desc = Label(card, def.Description, 17, new Vector2(0.5f, 0.48f), new Vector2(174, 168), Vector2.zero);
                desc.color = new Color(0.18f, 0.11f, 0.055f);
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 15;
                desc.fontSizeMax = 17;
                desc.lineSpacing = 0;
                desc.overflowMode = TextOverflowModes.Truncate;
                var buyHitArea = Panel(card, "BuyHitArea", new Color(1f, 1f, 1f, 0f));
                SetRect(buyHitArea, new Vector2(0.5f, 0f), new Vector2(166, 52), new Vector2(0, 24));
                var buyButton = buyHitArea.gameObject.AddComponent<Button>();
                buyButton.targetGraphic = buyHitArea.GetComponent<Image>();
                if (!offer.Purchased)
                    buyButton.onClick.AddListener(() => Operate(_service.Buy(index), "购买"));
                if (!offer.Purchased)
                {
                    AddIcon(buyHitArea, "PriceIcon", _coinIconSprite, new Vector2(0.38f, 0.5f), new Vector2(32, 32), new Vector2(0, 6));
                    var priceText = Label(buyHitArea, "×" + offer.Price, 18, new Vector2(0.64f, 0.5f),
                        new Vector2(60, 30), new Vector2(0, 6));
                    priceText.color = new Color(0.18f, 0.11f, 0.055f);
                    priceText.fontStyle = FontStyles.Bold;
                }
                else
                {
                    var purchasedText = Label(buyHitArea, "已购买", 16, new Vector2(0.5f, 0.5f),
                        new Vector2(130, 30), Vector2.zero);
                    purchasedText.color = new Color(0.32f, 0.24f, 0.14f);
                }
            }
        }

        void RebuildInventory()
        {
            ClearChildren(_inventoryItems);
            _service.Session.GetOwnedCardIds(_visibleCardIds);
            _inventoryItems.sizeDelta = new Vector2(0, Mathf.Max(486, _visibleCardIds.Count * 116 + 20));
            for (int i = 0; i < _visibleCardIds.Count; i++)
            {
                string id = _visibleCardIds[i];
                var instance = _service.Session.GetCard(id);
                var item = Panel(_inventoryItems, "Card_" + id, Color.clear);
                SetRect(item, new Vector2(0.5f, 1f), new Vector2(212, 172), new Vector2(0, -18 - i * 116));
                bool isEquipped = instance.Location == TraitCardLocation.Equipped;
                var frame = ImagePanel(item, "Frame", isEquipped ? _inventorySprite : _inventoryPlainSprite,
                    id == _selectedInventoryCardId ? new Color(0.62f, 0.86f, 0.96f) : Color.white);
                Stretch(frame);
                frame.GetComponent<Image>().raycastTarget = false;
                if (isEquipped)
                {
                    var owner = CommanderCatalog.GetById(instance.EquippedCommanderId);
                    var portrait = owner == null ? null : Resources.Load<Sprite>(owner.PortraitAssetId);
                    if (portrait != null)
                    {
                        var badge = new GameObject("Owner", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                        badge.transform.SetParent(item, false);
                        badge.sprite = portrait;
                        badge.preserveAspect = true;
                        badge.raycastTarget = false;
                        SetRect(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(70, 70), new Vector2(8, 0));
                    }
                }
                var button = item.gameObject.AddComponent<Button>();
                button.targetGraphic = item.GetComponent<Image>();
                if (instance.Location == TraitCardLocation.Inventory)
                    button.onClick.AddListener(() => SelectInventory(id));
                item.gameObject.AddComponent<ShopDragHandle>().Configure(this, id,
                    instance.Location == TraitCardLocation.Equipped ? instance.EquippedCommanderId : null,
                    instance.EquippedSlotIndex);
            }
            if (_visibleCardIds.Count == 0)
                Label(_inventoryItems, "暂无未装备卡牌", 18, new Vector2(0.5f, 1f), new Vector2(200, 60), new Vector2(0, -70));
        }

        void RebuildCommanders()
        {
            ClearChildren(_commandersRoot);
            var commanderIds = _service.Session.ActiveCommanderIds;
            int count = commanderIds.Count;
            if (count == 0)
            {
                Label(_commandersRoot, "本次出征队伍数据缺失", 20, new Vector2(0.5f, 0f),
                    new Vector2(320, 50), new Vector2(0, 54));
                return;
            }
            const float basePanelWidth = 300f;
            const float panelHeight = 104f;
            const float portraitSize = 72f;
            const float slotWidth = 86f;
            const float slotHeight = 36f;
            const float slotGap = 6f;
            const float panelGap = 12f;
            const float labelWidth = 110f;
            const float labelGap = 20f;
            float visualScale = count <= 4 ? 1f : Mathf.Max(0.76f, 4.7f / count);
            float panelWidth = basePanelWidth * visualScale;
            float totalWidth = labelWidth + labelGap + count * panelWidth + Mathf.Max(0, count - 1) * panelGap;
            float left = -totalWidth * 0.5f;
            float labelX = left + labelWidth * 0.5f;
            var teamLabel = AddIcon(_commandersRoot, "TeamLabel", _teamLabelSprite, new Vector2(0.5f, 0f),
                new Vector2(labelWidth, 58), new Vector2(labelX, 62));
            teamLabel.raycastTarget = false;
            var teamText = Label(_commandersRoot, "队伍", 24, new Vector2(0.5f, 0f),
                new Vector2(100, 48), new Vector2(labelX, 66));
            teamText.color = new Color(0.18f, 0.12f, 0.07f);
            teamText.fontStyle = FontStyles.Bold;
            for (int i = 0; i < count; i++)
            {
                var commander = CommanderCatalog.GetById(commanderIds[i]);
                if (commander == null) continue;
                var panel = Panel(_commandersRoot, commander.Id, Color.clear);
                float x = left + labelWidth + labelGap + panelWidth * (i + 0.5f) + panelGap * i;
                SetRect(panel, new Vector2(0.5f, 0f), new Vector2(300, panelHeight), new Vector2(x, 40));
                panel.localScale = Vector3.one * visualScale;

                var chip = ImagePanel(panel, "ChipBackground", _teamChipSprite, Color.white);
                SetRect(chip, new Vector2(0f, 0f), new Vector2(300, 92), Vector2.zero);
                chip.GetComponent<Image>().raycastTarget = false;

                var portraitSprite = Resources.Load<Sprite>(commander.MapPortraitAssetId);
                if (portraitSprite == null) portraitSprite = Resources.Load<Sprite>(commander.PortraitAssetId);
                if (portraitSprite != null)
                {
                    var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    portrait.transform.SetParent(panel, false);
                    portrait.sprite = portraitSprite;
                    portrait.preserveAspect = true;
                    portrait.raycastTarget = false;
                    SetRect(portrait.rectTransform, new Vector2(0f, 0f), new Vector2(portraitSize, portraitSize), new Vector2(12, 10));
                }

                var loadout = _service.Session.GetLoadout(commander.Id);
                for (int slot = 0; slot < 4; slot++)
                {
                    int slotIndex = slot;
                    string cardId = loadout == null ? null : loadout.TraitCardInstanceIds[slot];
                    var card = string.IsNullOrEmpty(cardId) ? null : _service.Session.GetCard(cardId);
                    var def = card == null ? null : TraitCatalog.Get(card.DefinitionId);
                    var slotRoot = ImagePanel(panel, "TraitSlot" + slot,
                        def == null ? _traitCellSprite : _traitCellSelectedSprite,
                        def == null ? new Color(0.9f, 0.9f, 0.9f, 0.82f) : Color.white);
                    int column = slot % 2;
                    int row = slot / 2;
                    float slotX = 108f + column * (slotWidth + slotGap);
                    float slotY = 50f - row * (slotHeight + 5f);
                    SetRect(slotRoot, new Vector2(0f, 0f), new Vector2(slotWidth, slotHeight), new Vector2(slotX, slotY));
                    if (def != null)
                    {
                        var iconSprite = Resources.Load<Sprite>(def.IconAssetId);
                        if (iconSprite != null)
                        {
                            var icon = AddIcon(slotRoot, "Icon", iconSprite, new Vector2(0.2f, 0.5f),
                                new Vector2(22f, 22f), Vector2.zero);
                            icon.raycastTarget = false;
                        }
                        var traitLabel = Label(slotRoot, def.DisplayName, 10, new Vector2(0.64f, 0.5f),
                            new Vector2(54f, 26f), Vector2.zero);
                        traitLabel.color = new Color(0.25f, 0.13f, 0.07f);
                        traitLabel.enableAutoSizing = true;
                        traitLabel.fontSizeMin = 8;
                        traitLabel.fontSizeMax = 10;
                    }
                    slotRoot.gameObject.AddComponent<ShopDropTarget>().Configure(this,
                        ShopDropTargetType.CommanderSlot, commander.Id, slotIndex);
                    if (card != null)
                        slotRoot.gameObject.AddComponent<ShopDragHandle>().Configure(this, cardId, commander.Id, slotIndex);
                    var btn = slotRoot.gameObject.AddComponent<Button>();
                    btn.targetGraphic = slotRoot.GetComponent<Image>();
                    btn.onClick.AddListener(() => OnSlotClicked(commander.Id, slotIndex));
                }
            }
        }

        public void DropCard(string cardId, string sourceCommander, int sourceSlot,
            ShopDropTargetType target, string targetCommander, int targetSlot)
        {
            if (target == ShopDropTargetType.Inventory && !string.IsNullOrEmpty(sourceCommander))
                Operate(_service.Unequip(sourceCommander, sourceSlot), "卸下");
            else if (target == ShopDropTargetType.CommanderSlot)
            {
                if (!string.IsNullOrEmpty(sourceCommander))
                {
                    Operate(_service.MoveEquippedCard(cardId, sourceCommander, sourceSlot,
                        targetCommander, targetSlot), "移动特性");
                    return;
                }
                Operate(_service.Equip(cardId, targetCommander, targetSlot), "装备");
            }
            else if (target == ShopDropTargetType.Sell && string.IsNullOrEmpty(sourceCommander))
                ShowSellConfirmation(cardId);
        }

        void ShowSellConfirmation(string cardId)
        {
            var card = _service.Session.GetCard(cardId);
            var def = card == null ? null : TraitCatalog.Get(card.DefinitionId);
            if (def == null) return;
            _modalRoot.gameObject.SetActive(true);
            ClearChildren(_modalRoot);
            var shade = Panel(_modalRoot, "Shade", new Color(0, 0, 0, 0.68f)); Stretch(shade);
            var dialog = ImagePanel(shade, "SellConfirm", _panelSprite, Color.white);
            SetRect(dialog, new Vector2(0.5f, 0.5f), new Vector2(560, 310), Vector2.zero);
            Label(dialog, "确认出售“" + def.DisplayName + "”？\n获得金币 ×" + def.SellPrice,
                26, new Vector2(0.5f, 0.62f), new Vector2(450, 120), Vector2.zero);
            StyledButton(dialog, "取消", new Vector2(0.3f, 0.2f), new Vector2(170, 52), Vector2.zero,
                () => _modalRoot.gameObject.SetActive(false));
            StyledButton(dialog, "出售", new Vector2(0.7f, 0.2f), new Vector2(170, 52), Vector2.zero, () =>
            {
                _modalRoot.gameObject.SetActive(false);
                Operate(_service.Sell(cardId), "出售");
            });
        }

        void SelectInventory(string id) { _selectedInventoryCardId = id; SetStatus("已选择卡牌，可点击特性槽装备"); RebuildInventory(); }

        void OnSlotClicked(string commanderId, int slot)
        {
            if (!string.IsNullOrEmpty(_selectedInventoryCardId))
            {
                string selectedCardId = _selectedInventoryCardId;
                _selectedInventoryCardId = null;
                var result = _service.Equip(selectedCardId, commanderId, slot);
                if (result != ShopOperationResult.Success)
                {
                    var selectedCard = _service.Session.GetCard(selectedCardId);
                    if (selectedCard != null && selectedCard.Location == TraitCardLocation.Inventory)
                        _selectedInventoryCardId = selectedCardId;
                    RebuildInventory();
                }
                Operate(result, "装备");
                return;
            }
            Operate(_service.Unequip(commanderId, slot), "卸下");
        }

#if UNITY_EDITOR
        IEnumerator CaptureEditorPreview()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            string screenshotDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "..", "..", "商店页面", "运行截图"));
            Directory.CreateDirectory(screenshotDirectory);
            string screenshotPath = Path.Combine(screenshotDirectory, "ShopUI-Latest.png");
            ScreenCapture.CaptureScreenshot(screenshotPath, 1);
            Debug.Log("[SettlementShop] UI screenshot saved: " + screenshotPath);
        }
#endif

        void CloseShop()
        {
            if (_modalRoot != null && _modalRoot.gameObject.activeSelf)
            {
                _modalRoot.gameObject.SetActive(false);
                return;
            }
            Destroy(gameObject);
        }

        void Confirm()
        {
            var result = _service.Commit();
            _committed = result == ShopOperationResult.Success;
            if (!_committed)
            {
                SetStatus("确认失败：" + result);
                return;
            }
            BattleStartContext.ExpeditionRoster = null;
            BattleStartContext.SelectedCommander = null;
            const string commanderScene = "CommanderSelectScene";
            if (SceneManager.GetActiveScene().name == commanderScene)
            {
                Destroy(gameObject);
                return;
            }
            if (Application.CanStreamedLevelBeLoaded(commanderScene)) SceneManager.LoadScene(commanderScene);
            else SceneManager.LoadScene(0);
        }

        void ShowRewardSummary()
        {
            if (_service.Session != null)
                SetStatus("本关结算获得金币 ×" + _service.Session.RewardGold);
        }

        void Operate(ShopOperationResult result, string action) =>
            SetStatus(result == ShopOperationResult.Success ? action + "成功" : action + "失败：" + result);
        void SetStatus(string text) { if (_statusText != null) _statusText.text = text; }

        RectTransform Panel(Transform parent, string name, Color color) => ImagePanel(parent, name, null, color);
        static void AddOutline(RectTransform target, Color color, Vector2 distance)
        {
            var outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }
        RectTransform ImagePanel(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color; image.preserveAspect = false;
            return go.GetComponent<RectTransform>();
        }

        Image AddIcon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 dimensions, Vector2 position)
        {
            var rect = ImagePanel(parent, name, sprite, Color.white);
            var image = rect.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetRect(rect, anchor, dimensions, position);
            return image;
        }

        TextMeshProUGUI Label(Transform parent, string text, float size, Vector2 anchor, Vector2 dimensions, Vector2 position)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.font = _font; label.fontSize = size;
            label.color = new Color(0.95f, 0.78f, 0.38f); label.alignment = TextAlignmentOptions.Center;
            SetRect(label.rectTransform, anchor, dimensions, position); return label;
        }

        Button StyledButton(Transform parent, string text, Vector2 anchor, Vector2 dimensions,
            Vector2 position, UnityEngine.Events.UnityAction action)
        {
            return StyledButton(parent, text, anchor, dimensions, position, _buttonSprite, action);
        }

        Button StyledButton(Transform parent, string text, Vector2 anchor, Vector2 dimensions,
            Vector2 position, Sprite sprite, UnityEngine.Events.UnityAction action)
        {
            var rt = ImagePanel(parent, "Button", sprite, Color.white); SetRect(rt, anchor, dimensions, position);
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = rt.GetComponent<Image>(); button.onClick.AddListener(action);
            float fontSize = Mathf.Clamp(dimensions.y * 0.45f, 14f, 22f);
            var label = Label(rt, text, fontSize, new Vector2(0.5f, 0.5f), dimensions, Vector2.zero);
            label.color = new Color(0.22f, 0.12f, 0.06f);
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            return button;
        }

        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = position; }
        static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
