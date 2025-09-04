using UnityEngine;
using System.Collections.Generic;

public class TileManager : MonoBehaviour
{
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

    public void Setup(GameObject[] prefabs, Vector2 cellSize, Vector2 originOffset, int w = 4, int h = 4)
    {
        this.prefabs = prefabs;
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
            if (grid[x, y] == null) empties.Add(new Vector2Int(x, y));
        });
        if (empties.Count == 0) return false;

        var c = empties[Random.Range(0, empties.Count)];
        float p2 = currentScore > 800 ? 0.8f : 0.9f;
        int val = Random.value < p2 ? 2 : 4;

        grid[c.x, c.y] = SpawnTile(val, c.x, c.y, pop: true);
        return true;
    }

    public bool IsGameOver()
    {
        bool anyEmpty = false;
        ForEachCell((x, y) =>
        {
            if (grid[x, y] == null) anyEmpty = true;
        });
        if (anyEmpty) return false;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int v = grid[x, y].value;
                if ((In(x + 1, y) && grid[x + 1, y].value == v) ||
                    (In(x, y + 1) && grid[x, y + 1].value == v))
                    return false;
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

        // 이동 벡터 (스캔 방향과 분리)
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
            if (grid[x, y] != null) grid[x, y].mergedThisTurn = false;
        });

        return movedThisTurn;
    }

    void SlideOrMerge(int x, int y, int dx, int dy, ref bool movedThisTurn, ref int addScore)
    {
        var t = grid[x, y];
        if (t == null) return;

        int nx = x, ny = y;

        // 빈칸 끝까지 미끄러짐
        while (In(nx + dx, ny + dy) && grid[nx + dx, ny + dy] == null)
        {
            nx += dx;
            ny += dy;
        }

        // 다음 칸에 타일이 있고 합체 가능?
        if (In(nx + dx, ny + dy) && grid[nx + dx, ny + dy] != null)
        {
            var dst = grid[nx + dx, ny + dy];
            if (!dst.mergedThisTurn && dst.value == t.value)
            {
                movedThisTurn = true;
                int newVal = t.value * 2;

                if (dst.view) Object.Destroy(dst.view);

                // 움직이는 뷰를 목적지까지 combine=true로 이동(도착 시 자기 파괴)
                MoveView(t, nx + dx, ny + dy, combine: true);

                // 그 자리에 새 타일 스폰(pop)
                grid[x, y] = null;
                grid[nx + dx, ny + dy] = SpawnTile(newVal, nx + dx, ny + dy, pop: true);
                grid[nx + dx, ny + dy].mergedThisTurn = true;
                addScore += newVal;
                return;
            }
        }

        // 단순 슬라이드
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
        int idx = Mathf.RoundToInt(Mathf.Log(value, 2)) - 1;
        idx = Mathf.Clamp(idx, 0, prefabs.Length - 1);

        var go = Object.Instantiate(prefabs[idx], CellToWorld(x, y), Quaternion.identity);

        var mv = go.GetComponent<Moving>();
        if (mv) mv.SetLayout(cellSize, originOffset);

        if (pop) go.GetComponent<Animator>()?.SetTrigger("Spawn");

        return new Tile { value = value, view = go, mergedThisTurn = false };
    }

    void MoveView(Tile t, int x, int y, bool combine)
    {
        var mv = t.view.GetComponent<Moving>();
        if (mv) mv.Move(x, y, combine);
        else t.view.transform.position = CellToWorld(x, y);
    }

    Vector3 CellToWorld(int x, int y)
        => new(originOffset.x + cellSize.x * x, originOffset.y + cellSize.y * y, 0);

    bool In(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    void ForEachCell(System.Action<int, int> f)
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                f(x, y);
    }
}
