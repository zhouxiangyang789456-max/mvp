using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
using Mvp.Shared.Skills;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// Hover tooltip for a skill button (冷却秒数、不可用原因). A small floating panel
    /// repositioned by BattleSkillButton; created at runtime by BattleSkillBar.
    /// </summary>
    public sealed class SkillTooltipView : MonoBehaviour
    {
        TextMeshProUGUI _title;
        TextMeshProUGUI _detail;

        public static SkillTooltipView Create(Transform parent)
        {
            var go = new GameObject("SkillTooltip", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<SkillTooltipView>();
            view.Build();
            go.SetActive(false);
            return view;
        }

        void Build()
        {
            var rt = GetComponent<RectTransform>();
            // Anchored to the bar's left-center so button anchored positions map directly.
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(230f, 66f);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.color = new Color(0.04f, 0.07f, 0.09f, 0.95f);
            bgImage.raycastTarget = false;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            _title = CreateLine("Title", 16f, new Color(1f, 0.86f, 0.48f, 1f), 6f);
            _detail = CreateLine("Detail", 13f, new Color(0.9f, 0.9f, 0.9f, 1f), 28f);
        }

        TextMeshProUGUI CreateLine(string name, float size, Color color, float top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(-20f, 22f);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        public void Show(SkillDefinition def, CommanderGroupRuntime group, float now, Vector2 anchoredPos)
        {
            var rt = GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            if (_title != null) _title.text = def != null ? def.DisplayName : "";
            if (_detail != null) _detail.text = BuildDetail(def, group, now);
            gameObject.SetActive(true);
            rt.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        static string BuildDetail(SkillDefinition def, CommanderGroupRuntime group, float now)
        {
            if (def == null) return "";
            var sb = new StringBuilder();
            if (def.Category == SkillCategory.Persistent)
            {
                bool active = group != null &&
                    ((def.Id == SkillIds.Guard && group.Skills.PersistentMode == PersistentSkillMode.Guard) ||
                     (def.Id == SkillIds.Concealment && group.Skills.PersistentMode == PersistentSkillMode.Concealment));
                sb.Append(active ? "已激活（点击退出）" : "常驻技能");
            }
            else if (def.Id == SkillIds.Sprint && group != null && SprintEffectService.IsActive(group, now))
            {
                sb.Append("冲刺进行中");
            }
            else if (def.Id == SkillIds.Taunt)
            {
                sb.Append(def.RangeCells).Append(" 格内全部敌军强制攻击本编队，暴露 ")
                    .Append(def.DurationSeconds.ToString("0")).Append(" 秒");
                float cooldown = group != null
                    ? TauntEffectService.GetRemainingCooldown(group, now) : 0f;
                sb.Append(" ｜ 冷却 ").Append((cooldown > 0.01f ? cooldown : def.CooldownSeconds)
                    .ToString("0.0")).Append("s");
            }
            else if (def.Id == SkillIds.Decoy)
            {
                sb.Append(def.RangeCells).Append("格内放置分身，吸引周围")
                    .Append(def.EffectRangeCells).Append("格敌军，持续")
                    .Append(def.DurationSeconds.ToString("0")).Append("秒")
                    .Append(" ｜ 冷却 ");
                float cooldown = group != null
                    ? TacticalDecoyService.GetRemainingCooldown(group, now) : 0f;
                sb.Append((cooldown > 0.01f ? cooldown : def.CooldownSeconds)
                    .ToString("0.0")).Append("s");
            }
            else if (def.CooldownSeconds > 0f)
            {
                float cooldown = group != null
                    ? SkillEligibilityService.GetMaxRemainingCooldown(group, def, now)
                    : 0f;
                if (cooldown > 0.01f)
                    sb.Append("冷却 ").Append(cooldown.ToString("0.0")).Append("s");
                else
                    sb.Append("冷却 ").Append(def.CooldownSeconds.ToString("0")).Append("s");
            }
            string reason;
            if (!SkillEligibilityService.CanActivate(group, def, out reason, now))
                sb.Append(" ｜ ").Append(reason);
            return sb.ToString();
        }
    }
}
