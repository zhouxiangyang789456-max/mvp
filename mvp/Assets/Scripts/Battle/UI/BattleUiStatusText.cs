using TMPro;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Shows a short status line for the currently selected unit (battle UI debug/placeholder).
    /// Attached to the StatusText TextMeshProUGUI in BattleScene.
    /// </summary>
    public class BattleUiStatusText : MonoBehaviour
    {
        public static BattleUiStatusText Instance { get; private set; }

        TextMeshProUGUI _text;

        void Awake()
        {
            Instance = this;
            _text = GetComponent<TextMeshProUGUI>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetStatus(string s)
        {
            if (_text != null) _text.text = s;
        }
    }
}
