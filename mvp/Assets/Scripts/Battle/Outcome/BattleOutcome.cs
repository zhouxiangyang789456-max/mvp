using System;

namespace Mvp.Battle.Outcome
{
    public enum BattleOutcome { None, Victory, Defeat, Draw, Aborted }
    public enum BattleOutcomeState { Registering, Armed, Running, Candidate, Resolved, Presenting, Transitioning, Aborted }

    [Serializable]
    public sealed class BattleResultSnapshot
    {
        public string BattleId;
        public string LevelId;
        public BattleOutcome Outcome;
        public int ResolutionTick;
        public int InitialPlayerGroups;
        public int InitialEnemyGroups;
        public int InitialPlayerUnits;
        public int InitialEnemyUnits;
        public int SurvivingPlayerGroups;
        public int SurvivingEnemyGroups;
        public int PlayerUnitsLost;
        public int EnemyUnitsDefeated;
        public int RewardGold;
        public string RewardGrantId;
        public int ShopRandomSeed;
    }
}
