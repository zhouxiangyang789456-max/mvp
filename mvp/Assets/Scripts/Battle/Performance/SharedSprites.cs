using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Lazily-created 1x1 white sprite used to build placeholder quads for
    /// selection rings, health bars, highlight cells and effect flashes.
    /// Generated at runtime, never persisted.
    /// </summary>
    public static class SharedSprites
    {
        static Sprite _white;

        public static Sprite White
        {
            get
            {
                if (_white == null) _white = CreateWhite();
                return _white;
            }
        }

        static Sprite CreateWhite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // PPU = 1 so a sprite scaled to (w,h) renders as (w,h) world units.
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
