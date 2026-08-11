namespace Mvp.Shared
{
    /// <summary>Battle stage (战斗页面开发文档: 部署阶段 -> 实时战斗阶段).</summary>
    public enum BattlePhase
    {
        Deployment,
        Combat
    }

    /// <summary>Current battle phase. Defaults to Deployment until "开始战斗" is pressed.</summary>
    public static class BattlePhaseState
    {
        public static BattlePhase Current = BattlePhase.Deployment;

        public static void StartCombat()
        {
            Current = BattlePhase.Combat;
        }

        public static void ResetToDeployment()
        {
            Current = BattlePhase.Deployment;
        }
    }
}
