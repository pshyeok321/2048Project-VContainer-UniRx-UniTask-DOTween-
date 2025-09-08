using UniRx;

using UnityEngine;

using VContainer;

public sealed class DebugEventLogger : MonoBehaviour
{
    GameEvents events;
    CompositeDisposable cd;

    [Inject] public void Construct(GameEvents e) => events = e;

    void OnEnable()
    {
        if (events == null) return;
        cd = new CompositeDisposable();
        events.TurnStarted.Subscribe(_ => Debug.Log("[EV] TurnStarted")).AddTo(cd);
        events.TurnEnded.Subscribe(m => Debug.Log($"[EV] TurnEnded moved={m}")).AddTo(cd);
        events.ScoreChanged.Subscribe(e => Debug.Log($"[EV] Score={e.Score} (+{e.Delta})")).AddTo(cd);
        events.BestChanged.Subscribe(b => Debug.Log($"[EV] Best={b}")).AddTo(cd);
        events.TileSpawned.Subscribe(t => Debug.Log($"[EV] Spawn {t.Value} @ {t.Cell}")).AddTo(cd);
        events.Merge.Subscribe(m => Debug.Log($"[EV] Merge {m.Value} x{m.Count} @ {m.Cell}")).AddTo(cd);
    }

    void OnDisable() => cd?.Dispose();
}
