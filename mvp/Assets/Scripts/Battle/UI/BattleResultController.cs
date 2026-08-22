using Mvp.Battle.Outcome;
using Mvp.Progression;
using Mvp.SettlementShop;
using Mvp.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mvp.Battle.UI
{
    public sealed class BattleResultController : MonoBehaviour
    {
        BattleResultSnapshot _result;
        BattleOutcomeController _outcome;
        bool _submitted;
        TMP_FontAsset _font;

        public static BattleResultController Show(BattleResultSnapshot result,
            BattleOutcomeController outcome)
        {
            var existing = FindObjectOfType<BattleResultController>();
            if (existing != null) return existing;
            var go = new GameObject("BattleResult", typeof(RectTransform));
            var controller = go.AddComponent<BattleResultController>();
            controller._result = result;
            controller._outcome = outcome;
            controller.Build();
            return controller;
        }

        void Build()
        {
            _font = Resources.Load<TMP_FontAsset>("Battle/UI/Fonts/SimHei SDF");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            var shade = Panel(transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(shade);
            var panel = Panel(shade, new Color(0.025f, 0.12f, 0.16f, 0.98f));
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(720, 500), Vector2.zero);

            string title = _result.Outcome == BattleOutcome.Victory ? "胜利" :
                _result.Outcome == BattleOutcome.Defeat ? "失败" : "双方全灭";
            Label(panel, title, 58, new Vector2(0.5f, 0.82f), new Vector2(560, 80));
            string details = "消灭敌军：" + _result.EnemyUnitsDefeated +
                "\n我方损失：" + _result.PlayerUnitsLost +
                "\n存活编队：" + _result.SurvivingPlayerGroups;
            if (_result.Outcome == BattleOutcome.Victory)
                details += "\n获得金币：" + _result.RewardGold;
            Label(panel, details, 27, new Vector2(0.5f, 0.52f), new Vector2(500, 190));

            if (_result.Outcome == BattleOutcome.Victory)
                Button(panel, "进入商店", new Vector2(0.5f, 0.16f), OpenShop);
            else
            {
                Button(panel, "重新挑战", new Vector2(0.35f, 0.16f), Retry);
                Button(panel, "返回", new Vector2(0.65f, 0.16f), ReturnToSelection);
            }
        }

        void OpenShop()
        {
            if (!BeginSubmit()) return;
            gameObject.SetActive(false);
            var rollContext = new TraitOfferRollContext
            {
                LevelIndex = BattleStartContext.LevelIndex,
                HasBattleResult = true,
                InitialPlayerUnits = _result.InitialPlayerUnits,
                PlayerUnitsLost = _result.PlayerUnitsLost,
                SurvivingPlayerGroups = _result.SurvivingPlayerGroups,
                InitialEnemyGroups = _result.InitialEnemyGroups,
                SurvivingEnemyGroups = _result.SurvivingEnemyGroups
            };
            TraitShopDirector.CollectOwnedTags(PlayerProgressionStore.Current,
                rollContext.OwnedTraitTags);

            var args = new SettlementShopOpenArgs
            {
                SessionId = _result.BattleId + "_shop",
                RewardGrantId = _result.RewardGrantId,
                RandomSeed = _result.ShopRandomSeed,
                RewardGold = _result.RewardGold,
                RollContext = rollContext
            };
            var roster = BattleStartContext.ExpeditionRoster;
            if (roster != null)
                for (int i = 0; i < roster.Commanders.Count; i++)
                    args.ActiveCommanderIds.Add(roster.Commanders[i].CommanderId);
            SettlementShopController.OpenShop(args);
        }

        void Retry()
        {
            if (!BeginSubmit()) return;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void ReturnToSelection()
        {
            if (!BeginSubmit()) return;
            const string commanderScene = "CommanderSelectScene";
            if (Application.CanStreamedLevelBeLoaded(commanderScene)) SceneManager.LoadScene(commanderScene);
            else SceneManager.LoadScene(0);
        }

        bool BeginSubmit()
        {
            if (_submitted) return false;
            _submitted = true;
            if (_outcome != null) _outcome.BeginTransition();
            return true;
        }

        RectTransform Panel(Transform parent, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        void Label(Transform parent, string text, float size, Vector2 anchor, Vector2 dimensions)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.96f, 0.8f, 0.4f);
            SetRect(label.rectTransform, anchor, dimensions, Vector2.zero);
        }

        void Button(Transform parent, string text, Vector2 anchor, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.06f, 0.28f, 0.18f, 1f);
            SetRect(go.GetComponent<RectTransform>(), anchor, new Vector2(220, 62), Vector2.zero);
            go.GetComponent<Button>().onClick.AddListener(action);
            Label(go.transform, text, 27, new Vector2(0.5f, 0.5f), new Vector2(210, 58));
        }

        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
