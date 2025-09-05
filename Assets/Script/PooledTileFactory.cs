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

                    // 레이아웃 주입(재사용 시에도 안전)
                    var mvT = go.GetComponent<MovingDOTween>();
                    if (mvT) mvT.SetLayout(cellSize, originOffset);
                    var mv = go.GetComponent<Moving>();
                    if (mv) mv.SetLayout(cellSize, originOffset);
                    return go;
                },
                actionOnGet: go => {
                    go.transform.localScale = Vector3.one; // 스케일 초기화
                    go.SetActive(true);
                },
                actionOnRelease: go => {
                    // 안전: 모든 해당 오브젝트 트윈 중지(Dec는 각 컴포넌트 OnDisable/OnKill에서 처리)
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
        if (parent) go.transform.SetParent(parent, true); // 월드 좌표 유지

        // 재사용 시 레이아웃 보정(옵션)
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
            // 풀 인덱스를 모르면 안전하게 파괴 (개발 중 혼용 방지)
            Object.Destroy(tile);
        }
    }

    Vector3 CellToWorld(int x, int y)
        => new(originOffset.x + cellSize.x * x, originOffset.y + cellSize.y * y, 0);
}

public class PooledTileTag : MonoBehaviour
{
    public int poolIndex = -1; // 어느 풀에서 온 타일인지 표시
}

