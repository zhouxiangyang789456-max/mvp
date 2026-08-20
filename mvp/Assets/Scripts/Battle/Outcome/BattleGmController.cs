using Mvp.Battle.UI;
using UnityEngine;

namespace Mvp.Battle.Outcome
{
    /// <summary>Development-only battle shortcuts. F8 resolves the current battle as victory.</summary>
    public sealed class BattleGmController : MonoBehaviour
    {
        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Input.GetKeyDown(KeyCode.F8)) return;
            var outcome = BattleOutcomeController.Instance;
            bool success = outcome != null && outcome.TryForceVictory();
            var status = BattleUiStatusText.Instance;
            if (status != null)
                status.SetStatus(success ? "GM：已强制判定胜利" : "GM：当前状态不能强制胜利");
            if (!success) Debug.LogWarning("[BattleGM] Force victory rejected by current battle state.");
#endif
        }
    }
}
