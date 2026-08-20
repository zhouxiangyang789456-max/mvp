using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mvp.Battle.Map.Generation;
using Mvp.Shared;

namespace Mvp.CommanderSelect
{
    /// <summary>
    /// Wires up the static CommanderSelectScene UI: card selection, summary/detail
    /// panel updates, and the 出征 (embark) flow into BattleScene.
    /// Attach to the scene Canvas.
    /// </summary>
    public class CommanderSelectController : MonoBehaviour
    {
        const string SceneToLoad = "BattleScene";
        const int MaxCards = 6;

        readonly List<CommanderDefinition> _commanders = new List<CommanderDefinition>();
        readonly Button[] _cardButtons = new Button[MaxCards];
        readonly Image[] _cardImages = new Image[MaxCards];
        readonly TextMeshProUGUI[] _cardNames = new TextMeshProUGUI[MaxCards];
        readonly TextMeshProUGUI[] _cardCounts = new TextMeshProUGUI[MaxCards];
        readonly Image[] _cardPortraits = new Image[MaxCards];

        TextMeshProUGUI _summaryTitle1, _summaryBody1;
        TextMeshProUGUI _summaryTitle2, _summaryBody2;
        TextMeshProUGUI _summaryTitle3, _summaryBody3;
        TextMeshProUGUI _detailTitle, _detailBody;
        Button _embarkButton;
        Button _returnButton;

        [Header("关卡地图")]
        [Tooltip("当前进入的关卡号；BattleGridController 用它在 MapProfile 中查找规则。")]
        [SerializeField] int _levelIndex = 1;
        [Tooltip("关卡随机地图配置资产；为空时战斗场景回退到自身的 ProceduralSettings。")]
        [SerializeField] LevelMapGenerationProfile _mapProfile;

        readonly List<int> _selected = new List<int>();
        int _focused = -1;

        void Start()
        {
            _commanders.Clear();
            _commanders.AddRange(CommanderCatalog.GetAll());

            BindCards();
            BindPanels();
            BindEmbark();
            BindReturn();

            RefreshCards();
            ShowUnselected();
            UpdateEmbarkState();
        }

        void BindCards()
        {
            for (int i = 0; i < MaxCards; i++)
            {
                int idx = i;
                var card = transform.Find("CommanderSelectUI/CommanderCardRow/CommanderCard" + (i + 1));
                if (card == null)
                {
                    Debug.LogWarning("[CommanderSelect] Missing CommanderCard" + (i + 1));
                    continue;
                }

                var img = card.GetComponent<Image>();
                _cardImages[i] = img;

                var btn = card.GetComponent<Button>();
                if (btn == null) btn = card.gameObject.AddComponent<Button>();
                if (img != null) btn.targetGraphic = img;
                btn.onClick.AddListener(() => OnCardClick(idx));
                _cardButtons[i] = btn;

                var nameTxt = card.Find("Name")?.GetComponent<TextMeshProUGUI>();
                _cardNames[i] = nameTxt;

                var countTxt = card.Find("Count")?.GetComponent<TextMeshProUGUI>();
                _cardCounts[i] = countTxt;

                var placeholder = card.Find("Portrait") as RectTransform;
                if (placeholder != null)
                {
                    placeholder.gameObject.SetActive(false);
                    var artGo = new GameObject("PortraitArt", typeof(RectTransform), typeof(Image));
                    artGo.transform.SetParent(card, false);
                    artGo.transform.SetSiblingIndex(placeholder.GetSiblingIndex());
                    var artRt = artGo.GetComponent<RectTransform>();
                    artRt.anchorMin = placeholder.anchorMin;
                    artRt.anchorMax = placeholder.anchorMax;
                    artRt.pivot = placeholder.pivot;
                    artRt.anchoredPosition = placeholder.anchoredPosition;
                    artRt.sizeDelta = placeholder.sizeDelta;
                    var art = artGo.GetComponent<Image>();
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    _cardPortraits[i] = art;
                }
            }
        }

        void BindPanels()
        {
            _summaryTitle1 = transform.Find("CommanderSelectUI/SummaryColumn/SummaryPanel1/Title")?.GetComponent<TextMeshProUGUI>();
            _summaryBody1 = transform.Find("CommanderSelectUI/SummaryColumn/SummaryPanel1/Body")?.GetComponent<TextMeshProUGUI>();
            _summaryTitle2 = transform.Find("CommanderSelectUI/SummaryColumn/SummaryPanel2/Title")?.GetComponent<TextMeshProUGUI>();
            _summaryBody2 = transform.Find("CommanderSelectUI/SummaryColumn/SummaryPanel2/Body")?.GetComponent<TextMeshProUGUI>();
            _summaryTitle3 = transform.Find("CommanderSelectUI/SummaryColumn/SummaryPanel3/Title")?.GetComponent<TextMeshProUGUI>();
            _summaryBody3 = transform.Find("CommanderSelectUI/SummaryColumn/SummaryPanel3/Body")?.GetComponent<TextMeshProUGUI>();
            _detailTitle = transform.Find("CommanderSelectUI/FramedPanel/DetailTitle")?.GetComponent<TextMeshProUGUI>();
            _detailBody = transform.Find("CommanderSelectUI/FramedPanel/DetailBody")?.GetComponent<TextMeshProUGUI>();
        }

        void BindEmbark()
        {
            var embark = transform.Find("CommanderSelectUI/EmbarkBtn");
            if (embark == null) { Debug.LogWarning("[CommanderSelect] EmbarkBtn not found"); return; }
            var img = embark.GetComponent<Image>();
            _embarkButton = embark.GetComponent<Button>();
            if (_embarkButton == null) _embarkButton = embark.gameObject.AddComponent<Button>();
            if (img != null) _embarkButton.targetGraphic = img;
            _embarkButton.onClick.AddListener(OnEmbark);
        }

        void BindReturn()
        {
            var ret = transform.Find("CommanderSelectUI/ReturnBtn");
            if (ret == null) { Debug.LogWarning("[CommanderSelect] ReturnBtn not found"); return; }
            var img = ret.GetComponent<Image>();
            _returnButton = ret.GetComponent<Button>();
            if (_returnButton == null) _returnButton = ret.gameObject.AddComponent<Button>();
            if (img != null) _returnButton.targetGraphic = img;
            _returnButton.onClick.AddListener(OnReturn);
        }

        // ---------------------------------------------------------------- cards

        void RefreshCards()
        {
            for (int i = 0; i < MaxCards; i++)
            {
                bool has = i < _commanders.Count;
                var def = has ? _commanders[i] : null;

                if (_cardNames[i] != null)
                {
                    _cardNames[i].text = has ? def.DisplayName : "待解锁";
                    _cardNames[i].color = has ? new Color(1f, 0.88f, 0.5f) : new Color(0.8f, 0.8f, 0.8f);
                }
                if (_cardCounts[i] != null)
                {
                    int order = _selected.IndexOf(i);
                    _cardCounts[i].text = !has ? "—" : order >= 0 ? (order + 1).ToString() : "+";
                }

                if (_cardButtons[i] != null) _cardButtons[i].interactable = has;
                if (_cardPortraits[i] != null)
                {
                    _cardPortraits[i].sprite = has
                        ? Resources.Load<Sprite>(def.PortraitAssetId) : null;
                    _cardPortraits[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
                if (_cardImages[i] != null) ApplyCardTint(i);
            }
        }

        void ApplyCardTint(int i)
        {
            var img = _cardImages[i];
            if (img == null) return;
            bool has = i < _commanders.Count;
            if (!has)
            {
                img.color = new Color(1f, 1f, 1f, 0.35f);
            }
            else if (_selected.Contains(i))
            {
                img.color = new Color(1f, 0.94f, 0.72f, 1f);
            }
            else
            {
                img.color = Color.white;
            }
        }

        void OnCardClick(int index)
        {
            if (index >= _commanders.Count) return;
            _focused = index;
            if (_selected.Contains(index)) _selected.Remove(index);
            else _selected.Add(index);
            RefreshCards();
            ShowCommander(_commanders[index]);
            UpdateEmbarkState();
        }

        // ------------------------------------------------------------- panels

        void ShowUnselected()
        {
            SetText(_summaryTitle1, "指挥官");
            SetText(_summaryBody1, "未选择");
            SetText(_summaryTitle2, "特性");
            SetText(_summaryBody2, "—");
            SetText(_summaryTitle3, "初始兵种");
            SetText(_summaryBody3, "—");
            SetDetailVisible(false);
            SetText(_detailTitle, "");
            SetText(_detailBody, "");
        }

        void ShowCommander(CommanderDefinition c)
        {
            var sb = new StringBuilder();
            SetDetailVisible(true);
            SetText(_summaryTitle1, "指挥官");
            sb.Clear().Append(c.DisplayName).Append('\n').Append("生命 ").Append(c.CurrentHealth).Append('/').Append(c.MaxHealth);
            SetText(_summaryBody1, sb.ToString());

            SetText(_summaryTitle2, "特性");
            sb.Clear();
            for (int i = 0; i < c.Traits.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(c.Traits[i]);
            }
            SetText(_summaryBody2, sb.ToString());

            SetText(_summaryTitle3, "初始兵种");
            sb.Clear();
            for (int i = 0; i < c.StartingUnits.Count; i++)
            {
                var e = c.StartingUnits[i];
                if (i > 0) sb.Append('\n');
                sb.Append(UnitDisplayName(e.UnitType)).Append(" ×").Append(e.Count);
            }
            SetText(_summaryBody3, sb.ToString());

            SetText(_detailTitle, c.DisplayName);
            sb.Clear();
            sb.Append(c.DisplayName).Append('\n');
            sb.Append("生命 ").Append(c.CurrentHealth).Append('/').Append(c.MaxHealth).Append('\n');
            for (int i = 0; i < c.Traits.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(c.Traits[i]);
            }
            sb.Append('\n').Append("初始部队：");
            for (int i = 0; i < c.StartingUnits.Count; i++)
            {
                var e = c.StartingUnits[i];
                if (i > 0) sb.Append("、");
                sb.Append(UnitDisplayName(e.UnitType)).Append(" ×").Append(e.Count);
            }
            SetText(_detailBody, sb.ToString());
        }

        void SetDetailVisible(bool visible)
        {
            if (_detailTitle != null) _detailTitle.gameObject.SetActive(visible);
            if (_detailBody != null) _detailBody.gameObject.SetActive(visible);
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

        static void SetText(TextMeshProUGUI text, string s)
        {
            if (text != null) text.text = s;
        }

        // ------------------------------------------------------------ buttons

        void UpdateEmbarkState()
        {
            if (_embarkButton == null) return;
            bool ready = _selected.Count > 0;
            _embarkButton.interactable = ready;
            var label = _embarkButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = ready ? new Color(1f, 0.9f, 0.55f) : new Color(0.8f, 0.8f, 0.8f, 0.6f);
            }
        }

        void OnEmbark()
        {
            if (_selected.Count == 0) return;
            var roster = new ExpeditionRosterSnapshot();
            for (int i = 0; i < _selected.Count; i++)
            {
                int commanderIndex = _selected[i];
                if (commanderIndex < 0 || commanderIndex >= _commanders.Count) continue;
                roster.Commanders.Add(ExpeditionCommanderEntry.FromDefinition(
                    _commanders[commanderIndex], roster.Commanders.Count));
            }
            if (roster.IsEmpty) return;

            BattleStartContext.ExpeditionRoster = roster;
            BattleStartContext.SelectedCommander = _commanders[_selected[0]];
            BattleStartContext.LevelIndex = Mathf.Max(1, _levelIndex);
            BattleStartContext.MapProfile = _mapProfile;
            if (_mapProfile != null)
            {
                var rule = _mapProfile.FindRule(BattleStartContext.LevelIndex);
                Debug.Log("[CommanderSelect] Embark level=" + BattleStartContext.LevelIndex
                    + " rule=" + (rule != null ? (rule.DisplayName ?? rule.RuleId) : "(fallback)"));
            }
            Debug.Log("[CommanderSelect] Embark with " + roster.Commanders.Count + " commander group(s).");
            SceneManager.LoadScene(SceneToLoad);
        }

        void OnReturn()
        {
            // No start menu scene in the first MVP; keep as a no-op placeholder.
            Debug.Log("[CommanderSelect] Return clicked (no start scene in MVP).");
        }
    }
}
