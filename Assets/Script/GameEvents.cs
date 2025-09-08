// Minimal UniRx event hub (입력 스트림은 포함하지 않음: 기존 입력 로직 유지).
// VContainer에 Singleton으로 등록해서 주입/발행에 사용.
using UniRx;

using UnityEngine;

public readonly struct ScoreChangedEvent
{
    public readonly int Score;
    public readonly int Delta;
    public ScoreChangedEvent(int score, int delta) { Score = score; Delta = delta; }
}

public readonly struct MergeEvent
{
    public readonly int Value;
    public readonly int Count;
    public readonly Vector2Int Cell;
    public MergeEvent(int value, int count, Vector2Int cell) { Value = value; Count = count; Cell = cell; }
}

public readonly struct TileSpawnedEvent
{
    public readonly int Value;
    public readonly Vector2Int Cell;
    public TileSpawnedEvent(int value, Vector2Int cell) { Value = value; Cell = cell; }
}

public sealed class GameEvents : System.IDisposable
{
    public readonly Subject<TileManager.Dir> Input = new Subject<TileManager.Dir>();
    public readonly Subject<Unit> GameOver = new Subject<Unit>();

    public readonly Subject<Unit> TurnStarted = new Subject<Unit>();
    public readonly Subject<bool> TurnEnded = new Subject<bool>(); // moved?
    public readonly Subject<ScoreChangedEvent> ScoreChanged = new Subject<ScoreChangedEvent>();
    public readonly Subject<int> BestChanged = new Subject<int>();
    public readonly Subject<MergeEvent> Merge = new Subject<MergeEvent>();
    public readonly Subject<TileSpawnedEvent> TileSpawned = new Subject<TileSpawnedEvent>();

    public void Dispose()
    {
        Input?.Dispose();
        GameOver?.Dispose();

        TurnStarted?.Dispose();
        TurnEnded?.Dispose();
        ScoreChanged?.Dispose();
        BestChanged?.Dispose();
        Merge?.Dispose();
        TileSpawned?.Dispose();
    }
}
