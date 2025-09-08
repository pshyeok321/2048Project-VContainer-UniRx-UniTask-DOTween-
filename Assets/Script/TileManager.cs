using UnityEngine;
using System.Text;
using System.Collections.Generic;
using VContainer;
// ★ UniRx 이벤트 타입은 GameEvents.cs에 있으니 별도 using 없이 참조만.

public class TileManager : MonoBehaviour
{
    private IRandomProvider rng;   // 주입됨 (없으면 UnityEngine.Random 사용)
    private ITileFactory factory;  // 주입됨 (없으면 기존 Instantiate 사용)

    // UniRx 이벤트 허브
    private GameEvents events;

    [Inject]
    public void Construct(IRandomProvider rng, ITileFactory factory, GameEvents events) // ★ GameEvents 주입 추가
    {
        this.rng = rng;
        this.factory = factory;
        this.events = events; // ★ 보관
    }

    [Header("Layout")]
    [SerializeField] Vector2 cellSize = new(1.2f, 1.2f);
    [SerializeField] Vector2 originOffset = new(-1.8f, -1.8f);

    [Header("Board")]
    [SerializeField] int width = 4;
    [SerializeField] int height = 4;

    GameObject[] prefabs;

    class Tile
    {
        public int value;
        public GameObject view;
        public bool mergedThisTurn;
    }

    Tile[,] grid;

    public enum Dir { Up, Down, Left, Right }

    public void Setup(Vector2 cellSize, Vector2 originOffset, int w = 4, int h = 4)
    {
        this.prefabs = null;
        this.cellSize = cellSize;
        this.originOffset = originOffset;
        this.width = w;
        this.height = h;
        grid = new Tile[width, height];
    }

    public bool Spawn(int currentScore)
    {
        var empties = new List<Vector2Int>();
        ForEachCell((x, y) =>
        {
            if (grid[x, y] == null)
                empties.Add(new Vector2Int(x, y));
        });

        if (empties.Count == 0)
            return false;

        int index = rng != null ? rng.Range(0, empties.Count) : UnityEngine.Random.Range(0, empties.Count);
        var c = empties[index];
        float p2 = currentScore > 800 ? 0.8f : 0.9f;
        float r = rng != null ? rng.Value01() : UnityEngine.Random.value;
        int val = r < p2 ? 2 : 4;

        grid[c.x, c.y] = SpawnTile(val, c.x, c.y, pop: true);
        return true;
    }

    public bool IsGameOver()
    {
        bool anyEmpty = false;
        ForEachCell((x, y) =>
        {
            if (grid[x, y] == null)
                anyEmpty = true;
        });

        if (anyEmpty)
            return false;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int v = grid[x, y].value;
                if ((In(x + 1, y) && grid[x + 1, y].value == v) ||
                    (In(x, y + 1) && grid[x, y + 1].value == v))
                    return false;
            }
        }

        return true;
    }

    public bool Sweep(Dir dir, out int addScore, out bool movedThisTurn)
    {
        movedThisTurn = false;
        addScore = 0;

        int sx = dir == Dir.Right ? width - 1 : 0;
        int ex = dir == Dir.Right ? -1 : width;
        int sy = dir == Dir.Up ? height - 1 : 0;
        int ey = dir == Dir.Up ? -1 : height;

        int dx = dir == Dir.Right ? 1 : dir == Dir.Left ? -1 : 0;
        int dy = dir == Dir.Up ? 1 : dir == Dir.Down ? -1 : 0;

        if (dx != 0)
        {
            for (int y = 0; y < height; y++)
                for (int x = sx; x != ex; x += (dir == Dir.Right ? -1 : 1))
                    SlideOrMerge(x, y, dx, 0, ref movedThisTurn, ref addScore);
        }
        else
        {
            for (int x = 0; x < width; x++)
                for (int y = sy; y != ey; y += (dir == Dir.Up ? -1 : 1))
                    SlideOrMerge(x, y, 0, dy, ref movedThisTurn, ref addScore);
        }

        ForEachCell((x, y) =>
        {
            if (grid[x, y] != null)
                grid[x, y].mergedThisTurn = false;
        });

        return movedThisTurn;
    }

    void SlideOrMerge(int x, int y, int dx, int dy, ref bool movedThisTurn, ref int addScore)
    {
        var t = grid[x, y];
        if (t == null) return;

        int nx = x, ny = y;

        while (In(nx + dx, ny + dy) && grid[nx + dx, ny + dy] == null)
        { nx += dx; ny += dy; }

        if (In(nx + dx, ny + dy) && grid[nx + dx, ny + dy] != null)
        {
            var dst = grid[nx + dx, ny + dy];
            if (!dst.mergedThisTurn && dst.value == t.value)
            {
                movedThisTurn = true;
                int newVal = t.value * 2;

                if (dst.view)
                {
                    if (this.factory != null)
                    {
                        this.factory.Release(dst.view);
                    }
                    else
                    {
                        Object.Destroy(dst.view);
                    }
                }

                MoveView(t, nx + dx, ny + dy, combine: true);

                grid[x, y] = null;
                grid[nx + dx, ny + dy] = SpawnTile(newVal, nx + dx, ny + dy, pop: true);
                grid[nx + dx, ny + dy].mergedThisTurn = true;

                TileFXDOTween.Merge(grid[nx + dx, ny + dy].view); // ✅ 머지 임팩트

                addScore += newVal;

                // 머지 이벤트 발행 (항상 2개 병합, 도착 좌표 기준)
                events?.Merge.OnNext(new MergeEvent(newVal, 2, new Vector2Int(nx + dx, ny + dy)));

                return;
            }
        }

        if (nx != x || ny != y)
        {
            movedThisTurn = true;
            grid[x, y] = null;
            grid[nx, ny] = t;
            MoveView(t, nx, ny, combine: false);
        }
    }

    Tile SpawnTile(int value, int x, int y, bool pop)
    {
        GameObject go;

        if (factory != null)
        {
            // DI된 팩토리가 전담 생성
            go = factory.Create(new Vector2Int(x, y), value, transform);
        }
        else
        {
            // 팩토리가 없을 때만 프리팹 인덱싱
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogError("[TileManager] Prefabs not assigned and no factory. Cannot spawn tile.");
                return null;
            }

            int idx = Mathf.RoundToInt(Mathf.Log(value, 2)) - 1;
            idx = Mathf.Clamp(idx, 0, prefabs.Length - 1);

            go = Instantiate(prefabs[idx], transform);
            go.transform.position = CellToWorld(x, y);
        }

        TileFXDOTween.Spawn(go);

        // DOTween/Moving 모두 지원
        var mvd = go.GetComponent<MovingDOTween>();
        if (mvd) mvd.SetLayout(cellSize, originOffset);
        else
        {
            var mv = go.GetComponent<Moving>();
            if (mv) mv.SetLayout(cellSize, originOffset);
        }

        if (pop) TileFXDOTween.Spawn(go); // DOTween 스폰 팝

        // 이벤트 발행
        events?.TileSpawned.OnNext(new TileSpawnedEvent(value, new Vector2Int(x, y)));

        return new Tile { value = value, view = go, mergedThisTurn = false };
    }


    void MoveView(Tile t, int x, int y, bool combine)
    {
        // === DOTween 우선, 없으면 기존 Moving, 둘 다 없으면 즉시 텔레포트 ===
        var mvd = t.view.GetComponent<MovingDOTween>();
        if (mvd) mvd.Move(x, y, combine);
        else
        {
            var mv = t.view.GetComponent<Moving>();
            if (mv) mv.Move(x, y, combine);
            else t.view.transform.position = CellToWorld(x, y);
        }
    }

    Vector3 CellToWorld(int x, int y) =>
        new(originOffset.x + cellSize.x * x, originOffset.y + cellSize.y * y, 0);

    bool In(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    void ForEachCell(System.Action<int, int> f) { for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) f(x, y); }
}
