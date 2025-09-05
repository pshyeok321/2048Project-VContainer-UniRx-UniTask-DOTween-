public interface IRandomProvider
{
    int Range(int minInclusive, int maxExclusive);
    float Value01();
    void Reseed(int? seed = null);
}

public interface ITileFactory
{
    /// <summary>
    /// �� ��ǥ�� ���� �ش��ϴ� Ÿ���� ������ parent ������ ���Դϴ�.
    /// </summary>
    UnityEngine.GameObject Create(UnityEngine.Vector2Int cell, int value, UnityEngine.Transform parent);

    /// <summary>
    /// Ÿ�� ��ȯ(�⺻ ������ Destroy). Ǯ�� ���� �� Release�� �ٲٸ� ��.
    /// </summary>
    void Release(UnityEngine.GameObject tile);
}