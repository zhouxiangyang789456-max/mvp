using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
using Mvp.Shared.Skills;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// A single skill bar button (战斗技能系统开发文档 §3): name label, optional icon,
    /// cooldown drain overlay, active/greyed tints and a hover tooltip. Clicking routes
    /// to GroupSkillController; an already-active persistent mode toggles off instead.
    /// </summary>
    public sealed class BattleSkillButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        SkillDefinition _def;
        CommanderGroupRuntime _group;
        Action<string> _onClick;
        Image _background;
        Image _cooldownFill;
        TextMeshProUGUI _label;
        SkillTooltipView _tooltip;
        RectTransform _rect;

        public string SkillId { get { return _def != null ? _def.Id : null; } }

        public static BattleSkillButton Create(Transform parent, SkillDefinition def,
            CommanderGroupRuntime group, SkillTooltipView tooltip, Action<string> onClick)
        {
            var go = new GameObject("Skill_" + def.Id,
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var btn = go.AddComponent<BattleSkillButton>();
            btn._def = def;
            btn._group = group;
            btn._tooltip = tooltip;
            btn._onClick = onClick;
            btn.Build();
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => { if (btn._onClick != null) btn._onClick(btn._def.Id); });
            return btn;
        }

        public void Bind(CommanderGroupRuntime group) { _group = group; }

        void Build()
        {
            _rect = GetComponent<RectTransform>();
            _rect.sizeDelta = new Vector2(116f, 48f);
            _background = GetComponent<Image>();
            _background.color = new Color(0.05f, 0.20f, 0.24f, 0.96f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            _label = labelGo.GetComponent<TextMeshProUGUI>();
            _label.font = TMP_Settings.defaultFontAsset;
            _label.fontSize = 18f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.text = _def.DisplayName;
            _label.color = new Color(1f, 0.86f, 0.48f, 1f);
            _label.raycastTarget = false;

            var overlayGo = new GameObject("Cooldown", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(transform, false);
            var overlayRt = overlayGo.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            _cooldownFill = overlayGo.GetComponent<Image>();
            _cooldownFill.color = new Color(0f, 0f, 0f, 0.66f);
            _cooldownFill.type = Image.Type.Filled;
            _cooldownFill.fillMethod = Image.FillMethod.Vertical;
            _cooldownFill.fillOrigin = (int)Image.OriginVertical.Top;
            _cooldownFill.fillAmount = 0f;
            _cooldownFill.raycastTarget = false;
            overlayGo.SetActive(false);
        }

        public void Refresh(float now)
        {
            if (_def == null) return;
            bool active = IsActiveMode();
            string reason;
            bool canActivate = SkillEligibilityService.CanActivate(_group, _def, out reason, now);

            var button = GetComponent<Button>();
            button.interactable = canActivate || active;

            if (active)
                _background.color = new Color(0.10f, 0.34f, 0.20f, 0.95f);
            else if (canActivate)
                _background.color = new Color(0.05f, 0.20f, 0.24f, 0.96f);
            else
                _background.color = new Color(0.13f, 0.14f, 0.16f, 0.95f);

            if (_label != null)
                _label.text = _def.Id == SkillIds.Sprint && _group != null &&
                    SprintEffectService.IsActive(_group, now)
                    ? "冲刺中"
                    : _def.DisplayName;

            bool showCooldown = false;
            float fill = 0f;
            if (_def.Category != SkillCategory.Persistent && _def.CooldownSeconds > 0f &&
                !(_def.Id == SkillIds.Sprint && _group != null && SprintEffectService.IsActive(_group, now)))
            {
                float remaining = SkillEligibilityService.GetMaxRemainingCooldown(_group, _def, now);
                showCooldown = remaining > 0.01f;
                fill = Mathf.Clamp01(remaining / _def.CooldownSeconds);
            }
            if (_cooldownFill != null)
            {
                _cooldownFill.gameObject.SetActive(showCooldown);
                _cooldownFill.fillAmount = fill;
            }
        }

        bool IsActiveMode()
        {
            if (_group == null || _def == null) return false;
            if (_def.Id == SkillIds.Guard)
                return _group.Skills.PersistentMode == PersistentSkillMode.Guard;
            if (_def.Id == SkillIds.Concealment)
                return _group.Skills.PersistentMode == PersistentSkillMode.Concealment;
            return false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_tooltip != null && _rect != null)
            {
                float now = Time.time;
                var anchored = _rect.anchoredPosition +
                    new Vector2(0f, _rect.sizeDelta.y * 0.5f + 8f);
                _tooltip.Show(_def, _group, now, anchored);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltip != null) _tooltip.Hide();
        }
    }
}
