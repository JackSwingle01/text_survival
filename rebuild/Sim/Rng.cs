namespace Survival;

/// SplitMix64. Chosen over the BCL's Random because its whole state is one serializable ulong.
public sealed class Rng
{
    public ulong State { get; set; }

    public static Rng ForSystem(int seed, string system)
    {
        // FNV-1a: string.GetHashCode is randomized per process, so it can't seed anything.
        ulong hash = 14695981039346656037;
        foreach (char c in system) { hash ^= c; hash *= 1099511628211; }
        return new Rng { State = hash ^ (ulong)(uint)seed };
    }

    public ulong NextUlong()
    {
        ulong z = State += 0x9E3779B97F4A7C15;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    /// Uniform in [0, 1).
    public double NextDouble() => (NextUlong() >> 11) * (1.0 / (1UL << 53));

    /// Uniform in [0, maxExclusive).
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextUlong() % (ulong)maxExclusive); // ponytail: modulo bias, negligible for game-sized ranges
    }
}
