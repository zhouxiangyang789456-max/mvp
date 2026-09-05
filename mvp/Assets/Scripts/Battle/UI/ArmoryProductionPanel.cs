using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mvp.Battle.Buildings;
using Mvp.Battle.Economy;
using Mvp.Battle.Units;
using Mvp.Shared;

namespace Mvp.Battle.UI
{
    /// <summary>Minimal runtime armory panel so captured armories have a visible production UI entry.</summary>
    public sealed class ArmoryProductionPanel : MonoBehaviour
    {
        static ArmoryProductionPanel _instance;

        RectTransform _panel;
        TextMeshProUGUI _title;
        TextMeshProUGUI _body;
        Transform _listRoot;
        BuildingRuntime _building;

        public static void Show(BuildingRuntime building)
        {
            if (building == null) return;
            if (_instance == null) CreateInstance();
            _instance.Open(building);
        }

        static void CreateInstance()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ArmoryProductionPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<ArmoryProductionPanel>();
            _instance.Build(canvas);
        }

        void Build(Canvas canvas)
        {
            _panel = GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(620f, 520f);

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.13f, 0.16f, 0.96f);

            _title = CreateText("Title", transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
            var titleRt = _title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);
            titleRt.sizeDelta = new Vector2(-40f, 48f);

            _body = CreateText("Body", transform, 18, FontStyles.Normal, TextAlignmentOptions.Left);
            var bodyRt = _body.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -74f);
            bodyRt.sizeDelta = new Vector2(-56f, 72f);

            var listGo = new GameObject("UnitList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(transform, false);
            _listRoot = listGo.transform;
            var listRt = listGo.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.offsetMin = new Vector2(32f, 76f);
            listRt.offsetMax = new Vector2(-32f, -160f);
            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var close = CreateButton("CloseButton", transform, "关闭", Close);
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 22f);
            closeRt.sizeDelta = new Vector2(180f, 50f);
        }

        void Open(BuildingRuntime building)
        {
            _building = building;
            gameObject.SetActive(true);
            _title.text = "兵工厂";
            var economy = BattleEconomyController.Instance;
            int gold = economy != null ? economy.PlayerGold : 0;
            _body.text = "已占领兵工厂\n当前金币：" + gold + "\n选择单位后续会接入生产队列。";
            RebuildList();
        }

        void RebuildList()
        {
            for (int i = _listRoot.childCount - 1; i >= 0; i--)
                Destroy(_listRoot.GetChild(i).gameObject);

            if (_building == null || _building.Definition == null) return;
            var units = ProductionCatalog.GetUnits(_building.Definition.ProductionCatalogId);
            for (int i = 0; i < units.Length; i++)
            {
                var def = UnitCatalog.Get(units[i]);
                if (def == null) continue;
                var row = CreateButton("Unit_" + def.Type, _listRoot, def.DisplayName + "    " + def.Cost + " 金币    " + def.ProductionSeconds + "秒", null);
                row.interactable = false;
            }
        }

        void Close()
        {
            gameObject.SetActive(false);
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(1f, 0.86f, 0.48f, 1f);
            text.enableWordWrapping = true;
            return text;
        }

        static Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.04f, 0.18f, 0.22f, 0.98f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 46f;

            var text = CreateText("Label", go.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            var rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return button;
        }
    }
}
