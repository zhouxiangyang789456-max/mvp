using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Skills;
using Mvp.Shared.Skills;

namespace Mvp.Battle.UI
{
    /// <summary>
    /// Runtime-built bottom skill bar (战斗技能系统开发文档 §3). One button per skill,
    /// all released from the currently active commander group. Created by
    /// BattleUiController, refreshed every frame (cooldown / grey-out / active tint)
    /// and re-bound on commander switch. Hidden outside the combat phase.
    /// </summary>
    public sealed class BattleSkillBar : MonoBehaviour
    {
        static readonly string[] _skillOrder =
        {
            SkillIds.Guard,
            SkillIds.Concealment,
            SkillIds.LongRange,
            SkillIds.Sprint,
            SkillIds.Taunt,
            SkillIds.Decoy
        };

        readonly List<BattleSkillButton> _buttons = new List<BattleSkillButton>();
        CommanderGroupRuntime _group;
        SkillTooltipView _tooltip;
        RectTransform _rect;

        public static BattleSkillBar Create(Transform parent, CommanderGroupRuntime group)
        {
            var go = new GameObject("BattleSkillBar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bar = go.AddComponent<BattleSkillBar>();
            bar._group = group;
            bar._rect = go.GetComponent<RectTransform>();
            bar._rect.anchorMin = new Vector2(0.5f, 0f);
            bar._rect.anchorMax = new Vector2(0.5f, 0f);
            bar._rect.pivot = new Vector2(0f, 0f);
            bar._rect.anchoredPosition = new Vector2(-182f, 20f);
            bar.Build(group);
            return bar;
        }

        void Build(CommanderGroupRuntime group)
        {
            // Tooltip is parented to this bar so button anchored positions match.
            _tooltip = SkillTooltipView.Create(transform);

            const float width = 116f;
            const float height = 48f;
            const float gap = 8f;
            int count = 0;
            for (int i = 0; i < _skillOrder.Length; i++)
            {
                var def = SkillCatalog.Get(_skillOrder[i]);
                if (def == null) continue;
                var btn = BattleSkillButton.Create(transform, def, group, _tooltip, OnSkillClick);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(count * (width + gap), 0f);
                _buttons.Add(btn);
                count++;
            }
            _rect.sizeDelta = new Vector2(count * (width + gap) - gap, height);
        }

        public void Bind(CommanderGroupRuntime group)
        {
            _group = group;
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) _buttons[i].Bind(group);
        }

        void OnSkillClick(string skillId)
        {
            if (_group == null) return;
            var skills = GroupSkillController.Instance;
            if (skills == null) return;

            string reason;
            bool isActiveMode = _group.Skills.PersistentMode != PersistentSkillMode.None &&
                ((skillId == SkillIds.Guard && _group.Skills.PersistentMode == PersistentSkillMode.Guard) ||
                 (skillId == SkillIds.Concealment && _group.Skills.PersistentMode == PersistentSkillMode.Concealment));

            bool handled = isActiveMode
                ? skills.TryExitPersistentMode(_group, skillId, out reason)
                : skills.TryActivate(_group, skillId, out reason);

            if (!handled)
            {
                var status = BattleUiStatusText.Instance;
                if (status != null) status.SetStatus(reason ?? "技能不可用");
            }
        }

        void Update()
        {
            float now = Time.time;
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) _buttons[i].Refresh(now);
        }

        void OnDestroy()
        {
            if (_tooltip != null) Destroy(_tooltip.gameObject);
        }
    }
}
