using System;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// 2D Perlin noise with an explicit seed. Mirrors the aw-map1 JS implementation
    /// (permutation table shuffled with mulberry32). Uses double precision internally
    /// so results are stable across runs on the same runtime.
    /// </summary>
    public sealed class PerlinNoise2D
    {
        readonly byte[] _perm;

        public PerlinNoise2D(uint seed)
        {
            var p = new byte[256];
            for (int i = 0; i < 256; i++) p[i] = (byte)i;

            var rng = new SeededRandom(seed);
            for (int j = 255; j > 0; j--)
            {
                int k = rng.NextInt(j + 1);
                byte t = p[j];
                p[j] = p[k];
                p[k] = t;
            }

            _perm = new byte[512];
            for (int m = 0; m < 512; m++) _perm[m] = p[m & 255];
        }

        public double Noise2(double x, double y)
        {
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;
            x -= Math.Floor(x);
            y -= Math.Floor(y);

            double u = x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
            double v = y * y * y * (y * (y * 6.0 - 15.0) + 10.0);

            byte[] p = _perm;
            int A = p[X] + Y;
            int B = p[X + 1] + Y;

            double Grad(int h, double dx, double dy)
            {
                int hh = h & 7;
                double uu = hh < 4 ? dx : dy;
                double vv = hh < 4 ? dy : dx;
                return ((hh & 1) != 0 ? -uu : uu) + ((hh & 2) != 0 ? -2.0 * vv : 2.0 * vv);
            }

            double Lerp(double a, double b, double t) => a + t * (b - a);

            return Lerp(
                Lerp(Grad(p[A], x, y), Grad(p[B], x - 1.0, y), u),
                Lerp(Grad(p[A + 1], x, y - 1.0), Grad(p[B + 1], x - 1.0, y - 1.0), u),
                v) * 0.5 + 0.5;
        }
    }
}
