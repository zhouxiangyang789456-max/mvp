using Mvp.CommanderSelect;
using Mvp.Progression;
using Mvp.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

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
        Sprite _buttonSprite;
        Sprite _coinIconSprite, _cardsIconSprite, _refreshIconSprite, _teamLabelSprite;
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
            _coinIconSprite = LoadSprite("coin_icon");
            _cardsIconSprite = LoadSprite("cards_icon");
            _refreshIconSprite = LoadSprite("refresh_icon");
            _teamLabelSprite = LoadSprite("team_label");
            if (!ValidateVisualResources())
            {
                enabled = false;
                return;
            }
            BuildPage();
            _service.Changed += OnSessionChanged;
            if (!_service.Open(SettlementShopContext.ConsumeOrCreateDefault())) SetStatus("无法打开商店会话");
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
            valid &= RequireSprite(_coinIconSprite, "coin_icon");
            valid &= RequireSprite(_cardsIconSprite, "cards_icon");
            valid &= RequireSprite(_refreshIconSprite, "refresh_icon");
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
            _rewardText.text = "      ×" + _service.Session.RewardGold;
            RebuildOffers();
            RebuildInventory();
            RebuildCommanders();
        }

        void BuildPage()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var shade = Panel(transform, "SettlementShade", new Color(0.015f, 0.025f, 0.04f, 0.78f));
            Stretch(shade);
            var main = ImagePanel(shade, "ShopPanel", _panelSprite, new Color(1f, 1f, 1f, 0.99f));
            SetRect(main, new Vector2(0.5f, 1f), new Vector2(1280, 700), new Vector2(0, -36));

            Label(main, "商店", 36, new Vector2(0.5f, 1f), new Vector2(240, 50), new Vector2(0, -28));
            AddIcon(main, "GoldIcon", _coinIconSprite, new Vector2(0f, 1f), new Vector2(72, 72), new Vector2(350, -70));
            _goldText = Label(main, "0", 21, new Vector2(0f, 1f), new Vector2(58, 36), new Vector2(420, -86));
            _goldText.alignment = TextAlignmentOptions.Left;

            var inventory = Panel(main, "Inventory", Color.clear);
            SetRect(inventory, new Vector2(0f, 0.5f), new Vector2(280, 600), new Vector2(20, -4));
            AddIcon(inventory, "CardsIcon", _cardsIconSprite, new Vector2(0.2f, 1f), new Vector2(50, 50), new Vector2(0, -35));
            Label(inventory, "我的卡牌", 21, new Vector2(0.56f, 1f), new Vector2(160, 38), new Vector2(0, -36));
            var inventoryDrop = inventory.gameObject.AddComponent<ShopDropTarget>();
            inventoryDrop.Configure(this, ShopDropTargetType.Inventory, null, -1);
            BuildInventoryScroll(inventory);

            _offersRoot = Panel(main, "Offers", Color.clear);
            SetRect(_offersRoot, new Vector2(0f, 0.57f), new Vector2(750, 490), new Vector2(350, 0));

            var sell = ImagePanel(main, "SellZone", _sellSprite, Color.white);
            SetRect(sell, new Vector2(1f, 0.56f), new Vector2(150, 345), new Vector2(-45, -2));
            var sellHeader = ImagePanel(sell, "SellHeader", _buttonSprite, Color.white);
            SetRect(sellHeader, new Vector2(0.5f, 1f), new Vector2(155, 40), new Vector2(0, 24));
            Label(sellHeader, "拖至此处售卖", 15, new Vector2(0.5f, 0.5f), new Vector2(145, 34), Vector2.zero);
            sell.gameObject.AddComponent<ShopDropTarget>().Configure(this, ShopDropTargetType.Sell, null, -1);

            var divider = Panel(main, "BottomDivider", new Color(0.78f, 0.55f, 0.22f, 0.95f));
            SetRect(divider, new Vector2(0f, 0f), new Vector2(880, 2), new Vector2(330, 132));

            var refreshButton = StyledButton(main, string.Empty, new Vector2(0f, 0f), new Vector2(170, 48),
                new Vector2(390, 54), () => Operate(_service.Refresh(RefreshPrice), "刷新"));
            AddIcon(refreshButton.transform, "RefreshIcon", _refreshIconSprite, new Vector2(0.2f, 0.5f), new Vector2(48, 48), Vector2.zero);
            AddIcon(refreshButton.transform, "RefreshCostIcon", _coinIconSprite, new Vector2(0.57f, 0.5f), new Vector2(56, 56), Vector2.zero);
            var refreshCostText = refreshButton.GetComponentInChildren<TextMeshProUGUI>();
            refreshCostText.text = "×" + RefreshPrice;
            SetRect(refreshCostText.rectTransform, new Vector2(0.82f, 0.5f), new Vector2(46, 32), Vector2.zero);
            var rewardButton = StyledButton(main, "      ×0", new Vector2(0f, 0f),
                new Vector2(170, 48), new Vector2(640, 54), ShowRewardSummary);
            AddIcon(rewardButton.transform, "RewardIcon", _coinIconSprite, new Vector2(0.32f, 0.5f), new Vector2(58, 58), Vector2.zero);
            _rewardText = rewardButton.GetComponentInChildren<TextMeshProUGUI>();
            StyledButton(main, "确认", new Vector2(0f, 0f), new Vector2(170, 48), new Vector2(890, 54), Confirm);
            _statusText = Label(main, string.Empty, 12, new Vector2(0f, 0f),
                new Vector2(360, 24), new Vector2(545, 140));

            _commandersRoot = Panel(shade, "CommanderLoadouts", Color.clear);
            _commandersRoot.anchorMin = new Vector2(0f, 0f);
            _commandersRoot.anchorMax = new Vector2(1f, 0f);
            _commandersRoot.pivot = new Vector2(0.5f, 0f);
            _commandersRoot.sizeDelta = new Vector2(0, 380);
            _commandersRoot.anchoredPosition = Vector2.zero;

            _modalRoot = Panel(shade, "ModalLayer", Color.clear);
            Stretch(_modalRoot);
            _modalRoot.gameObject.SetActive(false);
        }

        void BuildInventoryScroll(RectTransform inventory)
        {
            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(inventory, false);
            SetRect(scroll.GetComponent<RectTransform>(), new Vector2(0.46f, 0f), new Vector2(230, 468), new Vector2(0, 46));
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

            var scrollbar = Panel(inventory, "Scrollbar", new Color(0.75f, 0.53f, 0.2f, 0.72f));
            SetRect(scrollbar, new Vector2(1f, 0.5f), new Vector2(3, 420), new Vector2(-8, 8));
            var handle = Panel(scrollbar, "Handle", new Color(0.95f, 0.72f, 0.25f, 1f));
            SetRect(handle, new Vector2(0.5f, 1f), new Vector2(9, 76), Vector2.zero);
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
                SetRect(card, new Vector2(0.5f, 0.52f), new Vector2(170, 390), new Vector2((i - 1) * 250f, 0));
                Label(card, def.DisplayName, 18, new Vector2(0.5f, 0.76f), new Vector2(155, 32), Vector2.zero);
                var desc = Label(card, def.Description, 18, new Vector2(0.5f, 0.5f), new Vector2(150, 175), Vector2.zero);
                desc.color = new Color(0.18f, 0.11f, 0.055f);
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 14;
                desc.fontSizeMax = 18;
                desc.lineSpacing = -1;
                desc.overflowMode = TextOverflowModes.Truncate;
                var buyButton = StyledButton(card, offer.Purchased ? "已购买" : "      ×" + offer.Price,
                    new Vector2(0.5f, 0f), new Vector2(145, 38), new Vector2(0, 34),
                    offer.Purchased ? (UnityEngine.Events.UnityAction)(() => { }) : () => Operate(_service.Buy(index), "购买"));
                buyButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 17;
                if (!offer.Purchased)
                    AddIcon(buyButton.transform, "PriceIcon", _coinIconSprite, new Vector2(0.35f, 0.5f), new Vector2(46, 46), Vector2.zero);
            }
        }

        void RebuildInventory()
        {
            ClearChildren(_inventoryItems);
            _service.Session.GetOwnedCardIds(_visibleCardIds);
            _inventoryItems.sizeDelta = new Vector2(0, Mathf.Max(468, _visibleCardIds.Count * 110 + 8));
            for (int i = 0; i < _visibleCardIds.Count; i++)
            {
                string id = _visibleCardIds[i];
                var instance = _service.Session.GetCard(id);
                var item = Panel(_inventoryItems, "Card_" + id, Color.clear);
                SetRect(item, new Vector2(0.5f, 1f), new Vector2(218, 84), new Vector2(0, -48 - i * 110));
                bool isEquipped = instance.Location == TraitCardLocation.Equipped;
                var frame = ImagePanel(item, "Frame", isEquipped ? _inventorySprite : _inventoryPlainSprite,
                    id == _selectedInventoryCardId ? new Color(1f, 0.88f, 0.55f) : Color.white);
                if (isEquipped)
                {
                    // The equipped sprite includes the portrait circle to the left of the card body.
                    // Keep the rectangular body at the same 218x84 size as the plain card.
                    const float equippedFrameWidth = 242f;
                    SetRect(frame, new Vector2(1f, 0.5f),
                        new Vector2(equippedFrameWidth, 84f), Vector2.zero);
                }
                else
                {
                    Stretch(frame);
                }
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
                        SetRect(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(46, 46), new Vector2(-4, 0));
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
            const float panelWidth = 250f;
            const float panelHeight = 150f;
            const float portraitSize = 86f;
            const float slotSize = 42f;
            const float slotGap = 6f;
            float totalWidth = count * panelWidth;
            float labelX = -totalWidth * 0.5f - 62f;
            if (_teamLabelSprite != null)
            {
                var teamLabel = AddIcon(_commandersRoot, "TeamLabel", _teamLabelSprite, new Vector2(0.5f, 0f),
                    new Vector2(68, 40), new Vector2(labelX, 54));
                teamLabel.raycastTarget = false;
            }
            else
            {
                var teamText = Label(_commandersRoot, "队伍", 24, new Vector2(0.5f, 0f),
                    new Vector2(68, 40), new Vector2(labelX, 54));
                teamText.color = Color.white;
                teamText.fontStyle = FontStyles.Bold;
                AddOutline(teamText.rectTransform, Color.black, new Vector2(2f, -2f));
            }
            for (int i = 0; i < count; i++)
            {
                var commander = CommanderCatalog.GetById(commanderIds[i]);
                if (commander == null) continue;
                var panel = Panel(_commandersRoot, commander.Id, Color.clear);
                float x = -totalWidth * 0.5f + panelWidth * (i + 0.5f);
                SetRect(panel, new Vector2(0.5f, 0f), new Vector2(panelWidth, panelHeight), new Vector2(x, 16));

                var portraitSprite = Resources.Load<Sprite>(commander.MapPortraitAssetId);
                if (portraitSprite == null) portraitSprite = Resources.Load<Sprite>(commander.PortraitAssetId);
                if (portraitSprite != null)
                {
                    var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    portrait.transform.SetParent(panel, false);
                    portrait.sprite = portraitSprite;
                    portrait.preserveAspect = true;
                    SetRect(portrait.rectTransform, new Vector2(0f, 0f), new Vector2(portraitSize, portraitSize), new Vector2(8, 14));
                }
                var name = Label(panel, commander.DisplayName, 16, new Vector2(0f, 0f), new Vector2(105, 28), new Vector2(98, 24));
                name.alignment = TextAlignmentOptions.Left;

                var loadout = _service.Session.GetLoadout(commander.Id);
                for (int slot = 0; slot < 4; slot++)
                {
                    int slotIndex = slot;
                    string cardId = loadout == null ? null : loadout.TraitCardInstanceIds[slot];
                    var card = string.IsNullOrEmpty(cardId) ? null : _service.Session.GetCard(cardId);
                    var def = card == null ? null : TraitCatalog.Get(card.DefinitionId);
                    var slotRoot = Panel(panel, "TraitSlot" + slot,
                        def == null ? new Color(0f, 0f, 0f, 0.38f) : new Color(0f, 0f, 0f, 0.08f));
                    float slotsWidth = 4 * slotSize + 3 * slotGap;
                    float portraitCenterX = 8 + portraitSize * 0.5f;
                    float slotX = portraitCenterX - slotsWidth * 0.5f + slot * (slotSize + slotGap);
                    SetRect(slotRoot, new Vector2(0f, 1f), new Vector2(slotSize, slotSize), new Vector2(slotX, -2));
                    AddOutline(slotRoot,
                        def == null ? new Color(0.86f, 0.78f, 0.58f, 0.72f) : new Color(0.95f, 0.72f, 0.25f, 0.95f),
                        new Vector2(1.2f, -1.2f));
                    if (def != null)
                    {
                        var iconSprite = Resources.Load<Sprite>(def.IconAssetId);
                        if (iconSprite != null)
                        {
                            var icon = AddIcon(slotRoot, "Icon", iconSprite, new Vector2(0.5f, 0.5f),
                                new Vector2(slotSize - 6f, slotSize - 6f), Vector2.zero);
                            icon.raycastTarget = false;
                        }
                        else
                        {
                            var traitLabel = Label(slotRoot, def.DisplayName, 10, new Vector2(0.5f, 0.5f),
                                new Vector2(slotSize - 4f, slotSize - 4f), Vector2.zero);
                            traitLabel.enableAutoSizing = true;
                            traitLabel.fontSizeMin = 8;
                            traitLabel.fontSizeMax = 10;
                        }
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
            var rt = ImagePanel(parent, "Button", _buttonSprite, Color.white); SetRect(rt, anchor, dimensions, position);
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = rt.GetComponent<Image>(); button.onClick.AddListener(action);
            float fontSize = Mathf.Clamp(dimensions.y * 0.45f, 14f, 22f);
            var label = Label(rt, text, fontSize, new Vector2(0.5f, 0.5f), dimensions, Vector2.zero); label.raycastTarget = false; return button;
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
