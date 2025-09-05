using System;

public class SystemRandomProvider : IRandomProvider
{
    System.Random rng = new System.Random();

    public int Range(int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);
    public float Value01() => (float)rng.NextDouble();
    public void Reseed(int? seed = null) => rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
}