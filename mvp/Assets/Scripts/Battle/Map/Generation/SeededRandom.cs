namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// C# port of the JS mulberry32 PRNG used by aw-map1. Uses explicit 32-bit
    /// wraparound (unchecked int / uint) so a given seed always yields the same
    /// sequence inside C#. We do NOT aim for bit-for-bit equality with the JS
    /// output; only for stable, reproducible behaviour within the C# runtime.
    /// </summary>
    public sealed class SeededRandom
    {
        int _state;

        public SeededRandom(uint seed)
        {
            _state = unchecked((int)(seed != 0 ? seed : 1u));
        }

        /// <summary>Returns a value in [0, 1).</summary>
        public float NextFloat()
        {
            unchecked
            {
                _state += (int)0x6D2B79F5;
                int a = _state;

                // t = imul(a ^ (a >>> 15), 1 | a)
                int x = a ^ (int)((uint)a >> 15);
                int y = 1 | a;
                int t = unchecked((int)((uint)x * (uint)y));

                // t = (t + imul(t ^ (t >>> 7), 61 | t)) ^ t
                int x2 = t ^ (int)((uint)t >> 7);
                int y2 = 61 | t;
                int t2 = t + unchecked((int)((uint)x2 * (uint)y2));
                int result = t2 ^ (int)((uint)t2 >> 14);

                return (uint)result / 4294967296f;
            }
        }

        /// <summary>Returns an int in [0, maxExclusive), i.e. floor(NextFloat() * maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            return (int)(NextFloat() * maxExclusive);
        }
    }
}
