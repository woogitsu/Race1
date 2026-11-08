namespace Race1.Core
{
    /// <summary>
    /// SplitMix64-based generator. System.Random output is not guaranteed to be stable across
    /// runtimes, so route and world generation use this to stay reproducible from a seed.
    /// Value type: copying the struct copies the state (used for checkpoints).
    /// </summary>
    public struct DeterministicRng
    {
        private ulong _state;

        public DeterministicRng(ulong seed)
        {
            _state = seed;
        }

        /// <summary>SplitMix64 finaliser: maps a 64-bit value to a well-mixed 64-bit value.</summary>
        public static ulong Finalize(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Deterministically combines two keys into one seed.</summary>
        public static ulong Combine(ulong a, ulong b) => Finalize(Finalize(a) + b + 0x9E3779B97F4A7C15UL);

        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Finalize(_state);
        }

        /// <summary>Uniform value in [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        public double Range(double lo, double hi) => lo + (hi - lo) * NextDouble();

        public int NextInt(int lo, int hiExclusive) => lo + (int)(NextDouble() * (hiExclusive - lo));

        public bool Chance(double probability) => NextDouble() < probability;
    }
}
