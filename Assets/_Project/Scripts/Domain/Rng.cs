namespace NW.Domain
{
    /// <summary>
    /// Deterministic xorshift128 RNG. One named stream per system (board / director /
    /// combat) so replays and the headless balance simulator stay reproducible
    /// regardless of call interleaving between systems.
    /// </summary>
    public sealed class Rng
    {
        private uint _x, _y, _z, _w;

        public Rng(int seed)
        {
            // SplitMix-style scramble so adjacent seeds diverge immediately.
            uint s = (uint)seed;
            _x = s ^ 0x9E3779B9u;
            _y = (s << 13) | 1u;
            _z = s * 0x85EBCA6Bu + 0xC2B2AE35u;
            _w = ~s | 1u;
            for (int i = 0; i < 8; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ (t ^ (t >> 8));
            return _w;
        }

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        public int Next(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive) => min + Next(maxExclusive - min);

        /// <summary>Uniform float in [0, 1).</summary>
        public float Next01() => (NextUInt() & 0xFFFFFF) / 16777216f;
    }
}
