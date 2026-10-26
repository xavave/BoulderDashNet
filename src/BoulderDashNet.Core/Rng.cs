namespace BoulderDashNet.Core;

/// <summary>Générateur pseudo-aléatoire déterministe (xorshift32) pour des cavernes reproductibles.</summary>
public sealed class Rng(uint seed)
{
    private uint _state = seed == 0 ? 0x9E3779B9u : seed;

    public uint NextUInt()
    {
        var x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    /// <summary>Entier dans [0, max).</summary>
    public int Next(int max) => (int)(NextUInt() % (uint)max);

    /// <summary>Double dans [0, 1).</summary>
    public double NextDouble() => (NextUInt() >> 8) / (double)(1 << 24);
}
