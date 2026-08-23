using System.Collections.Generic;

namespace Mvp.Progression
{
    /// <summary>构筑成型程度:未定型 / 正在成型 / 流派已成型。</summary>
    public enum TraitBuildMaturity { Unformed, Forming, Formed }

    /// <summary>
    /// 流派成型判定结果 DTO。商店 UI(§5.3)与 Director 流派补强权重(§5.1)只依赖此 DTO,
    /// 不依赖 TraitBuildAnalyzer,因此分析器演进不影响消费方。
    /// </summary>
    public sealed class BuildAffinitySummary
    {
        /// <summary>最高分流派 Id;最高分 0 时为 null。</summary>
        public string PrimaryArchetypeId;

        /// <summary>次高分且 &gt; 0 的流派 Id;无则 null。</summary>
        public string SecondaryArchetypeId;

        /// <summary>主流派得分。</summary>
        public int PrimaryScore;

        /// <summary>主流派推荐补齐标签,按可行动性排序(权重降序→当前计数升序→标签序)。</summary>
        public readonly List<string> RecommendedTags = new List<string>();

        /// <summary>输入卡池的标签计数(防御性拷贝)。</summary>
        public readonly Dictionary<string, int> TagCounts = new Dictionary<string, int>();
    }
}
