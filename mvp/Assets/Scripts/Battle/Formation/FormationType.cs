namespace Mvp.Battle.Formation
{
    /// <summary>Formation layouts (战斗页面开发文档: 竖向 / 横向 / 方形).</summary>
    public enum FormationType
    {
        /// <summary>竖向: units line up in a column for narrow-channel / depth pushes.</summary>
        Vertical,
        /// <summary>横向: units line up in a row for a frontal spread.</summary>
        Horizontal,
        /// <summary>方形: units fill a compact rectangle.</summary>
        Square,
        /// <summary>自定义: units keep their player-assigned positions in the 3x3 grid.</summary>
        Custom
    }
}
