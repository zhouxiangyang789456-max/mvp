using System;

namespace Mvp.Battle.AI
{
    [Serializable]
    public sealed class EnemyAiConfig
    {
        public int MemoryDurationTicks = 12;
        public int RecoverDurationTicks = 3;

        public void Sanitize()
        {
            if (MemoryDurationTicks < 1) MemoryDurationTicks = 1;
            if (RecoverDurationTicks < 1) RecoverDurationTicks = 1;
        }
    }
}
