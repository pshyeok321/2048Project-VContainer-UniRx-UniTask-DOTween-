using UnityEngine;

public class DefaultTileFactory : ITileFactory
{
    readonly GameObject[] prefabs;
    readonly Vector2 cellSize;
    readonly Vector2 originOffset;

    public DefaultTileFactory(GameObject[] prefabs, Vector2 cellSize, Vector2 originOffset)
    {
        this.prefabs = prefabs;
        this.cellSize = cellSize;
        this.originOffset = originOffset;
    }

    public GameObject Create(Vector2Int cell, int value, Transform parent)
    {
        var go = Object.Instantiate(SelectPrefab(value), parent);
        go.transform.position = CellToWorld(cell.x, cell.y);

        // 레거시 Moving / DOTween 버전 모두 레이아웃 주입
        var mvT = go.GetComponent<MovingDOTween>();
        if (mvT) mvT.SetLayout(cellSize, originOffset);
        var mv = go.GetComponent<Moving>();
        if (mv) mv.SetLayout(cellSize, originOffset);
        return go;
    }

    public void Release(GameObject tile)
    {
        Object.Destroy(tile);
    }

    GameObject SelectPrefab(int value)
    {
        // 2 -> idx 0, 4 -> 1, ...  value = 2^(idx+1)
        int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(value, 2)) - 1, 0, prefabs.Length - 1);
        return prefabs[idx];
    }

    Vector3 CellToWorld(int x, int y)
        => new(originOffset.x + cellSize.x * x, originOffset.y + cellSize.y * y, 0);
}