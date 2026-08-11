using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Simple world-space billboard health bar built from two sprite quads
    /// (dark background + colored fill). Orientation refreshed on BattleTickService.MediumTick
    /// (性能文档：血条朝向走 MediumTick), cached camera avoids per-frame MainCamera lookups.
    /// </summary>
    public sealed class UnitHealthBar : MonoBehaviour
    {
        public const float WorldWidth = 1.1f;
        public const float WorldHeight = 0.14f;

        Transform _fill;
        SpriteRenderer _fillRenderer;
        Transform _cam;
        bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;

            var sprite = SharedSprites.White;

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(transform, false);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = sprite;
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            bg.sortingOrder = 100;
            bgGo.transform.localScale = new Vector3(WorldWidth, WorldHeight, 1f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            _fill = fillGo.transform;
            _fillRenderer = fillGo.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = sprite;
            _fillRenderer.color = new Color(0.35f, 1f, 0.35f, 0.95f);
            _fillRenderer.sortingOrder = 101;
            SetFill(1f);
        }

        public void SetFill(float f01)
        {
            if (!_built) Build();
            f01 = Mathf.Clamp01(f01);
            _fill.localScale = new Vector3(WorldWidth * f01, WorldHeight, 1f);
            _fill.localPosition = new Vector3((f01 - 1f) * 0.5f * WorldWidth, 0f, 0f);
        }

        public void SetColor(Color c)
        {
            if (!_built) Build();
            if (_fillRenderer != null) _fillRenderer.color = c;
        }

        void OnEnable()
        {
            BattleTickService.MediumTick += Orient;
        }

        void OnDisable()
        {
            BattleTickService.MediumTick -= Orient;
        }

        void Orient()
        {
            if (_cam == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                _cam = cam.transform;
            }
            transform.rotation = _cam.rotation;
        }
    }
}
