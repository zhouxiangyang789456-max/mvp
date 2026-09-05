namespace Mvp.Battle
{
    /// <summary>Pooled world-space UI element kinds (性能文档：UiPool).</summary>
    public enum UiPoolType
    {
        UnitHealthBar,
        SelectionRing,
        MoveTargetMarker,
        DeploymentCellHighlight,
        AttackRangeHighlight,
        /// <summary>远攻 skill range union highlight (per-cell overlap brightness).</summary>
        SkillRangeHighlight,
        /// <summary>远攻 current hovered-cell cursor (pulsing red square).</summary>
        SkillRangeCursor,
        /// <summary>隐蔽 blind zone marker (visual aid while a group is hidden).</summary>
        SkillBlindZone
    }
}
