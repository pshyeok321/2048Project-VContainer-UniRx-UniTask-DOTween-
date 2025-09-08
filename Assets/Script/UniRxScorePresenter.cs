// Optional: simple UI binder that listens to GameEvents.
// Attach to a Canvas object and assign Text fields.
// If you use TextMeshPro, switch types accordingly.
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class UniRxScorePresenter : MonoBehaviour
{
    [SerializeField] Text scoreText;
    [SerializeField] Text bestText;

    GameEvents events;
    CompositeDisposable cd;

    [Inject]
    public void Construct(GameEvents events) => this.events = events;

    void OnEnable()
    {
        if (events == null) return;
        cd = new CompositeDisposable();

        events.ScoreChanged
              .Subscribe(e => { if (scoreText) scoreText.text = e.Score.ToString(); })
              .AddTo(cd);

        events.BestChanged
              .Subscribe(best => { if (bestText) bestText.text = best.ToString(); })
              .AddTo(cd);
    }

    void OnDisable()
    {
        cd?.Dispose();
    }
}
