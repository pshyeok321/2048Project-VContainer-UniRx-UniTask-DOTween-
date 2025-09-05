public interface IRandomProvider
{
    int Range(int minInclusive, int maxExclusive);
    float Value01();
    void Reseed(int? seed = null);
}

public interface ITileFactory
{
    /// <summary>
    /// 셀 좌표와 값에 해당하는 타일을 생성해 parent 하위에 붙입니다.
    /// </summary>
    UnityEngine.GameObject Create(UnityEngine.Vector2Int cell, int value, UnityEngine.Transform parent);

    /// <summary>
    /// 타일 반환(기본 구현은 Destroy). 풀링 도입 시 Release만 바꾸면 됨.
    /// </summary>
    void Release(UnityEngine.GameObject tile);
}