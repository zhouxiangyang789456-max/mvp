using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Pooled world-space UI element (selection ring, move marker, deployment highlight,
    /// or a unit health bar). Carries its UiPoolType so UiPool can route it back.
    /// </summary>
    public sealed class PoolableUi : MonoBehaviour
    {
        public UiPoolType Type;
        public UnitHealthBar Bar;

        public void Spawn(UiPoolType type)
        {
            Type = type;
            gameObject.SetActive(true);
        }

        public void Despawn()
        {
            gameObject.SetActive(false);
        }
    }
}
