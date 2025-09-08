using UnityEngine;
using UnityEngine.Pool;
using DG.Tweening;

public class PooledTileFactory : ITileFactory
{
    readonly GameObject[] prefabs;
    readonly Vector2 cellSize;
    readonly Vector2 originOffset;
    readonly LinkedPool<GameObject>[] pools;
    readonly Transform poolRoot;
    readonly int maxSize;
    readonly bool collectionCheck;

    public PooledTileFactory(GameObject[] prefabs, Vector2 cellSize, Vector2 originOffset, int maxSize = 128, bool collectionCheck = false)
    {
        this.prefabs = prefabs;
        this.cellSize = cellSize;
        this.originOffset = originOffset;
        this.poolRoot = (GameObject.Find("TilePoolRoot") ?? new GameObject("TilePoolRoot")).transform;
        this.maxSize = maxSize;
        this.collectionCheck = collectionCheck;


        pools = new LinkedPool<GameObject>[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            int idx = i; // capture
            pools[i] = new LinkedPool<GameObject>(
                createFunc: () => {
                    var go = Object.Instantiate(prefabs[idx]);
                    go.SetActive(false);
                    var tag = go.GetComponent<PooledTileTag>() ?? go.AddComponent<PooledTileTag>();
                    tag.poolIndex = idx;

                    var mvT = go.GetComponent<MovingDOTween>();
                    if (mvT) mvT.SetLayout(cellSize, originOffset);
                    var mv = go.GetComponent<Moving>();
                    if (mv) mv.SetLayout(cellSize, originOffset);
                    return go;
                },
                actionOnGet: go => {
                    go.transform.localScale = Vector3.one;
                    go.SetActive(true);
                },
                actionOnRelease: go => {
                    DOTween.Kill(go, complete: false);
                    go.SetActive(false);
                    go.transform.SetParent(poolRoot, false);
                },
                actionOnDestroy: go => Object.Destroy(go),
                collectionCheck: collectionCheck,
                maxSize: maxSize
            );
        }
    }

    public GameObject Create(Vector2Int cell, int value, Transform parent)
    {
        int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(value, 2)) - 1, 0, prefabs.Length - 1);
        var go = pools[idx].Get();
        go.transform.position = CellToWorld(cell.x, cell.y);
        if (parent) go.transform.SetParent(parent, true);

        var mvT = go.GetComponent<MovingDOTween>();
        if (mvT) mvT.SetLayout(cellSize, originOffset);
        var mv = go.GetComponent<Moving>();
        if (mv) mv.SetLayout(cellSize, originOffset);
        return go;
    }

    public void Release(GameObject tile)
    {
        if (!tile) return;
        var tag = tile.GetComponent<PooledTileTag>();
        if (tag && tag.poolIndex >= 0 && tag.poolIndex < pools.Length)
        {
            pools[tag.poolIndex].Release(tile);
        }
        else
        {
            Object.Destroy(tile);
        }
    }

    Vector3 CellToWorld(int x, int y)
        => new(originOffset.x + cellSize.x * x, originOffset.y + cellSize.y * y, 0);
}

public class PooledTileTag : MonoBehaviour
{
    public int poolIndex = -1;
}

