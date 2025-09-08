using UniRx;

using UnityEngine;
using UnityEngine.UI;

using VContainer;

public sealed class ScorePresenter : MonoBehaviour
{
    [SerializeField] Text scoreText;
    [SerializeField] Text bestText;

    GameEvents events;
    CompositeDisposable cd;

    [Inject] public void Construct(GameEvents e) => events = e;

    void OnEnable()
    {
        if (events == null) return;
        cd = new CompositeDisposable();

        events.ScoreChanged
              .Subscribe(e => { if (scoreText) scoreText.text = e.Score.ToString(); })
              .AddTo(cd);

        events.BestChanged
              .Subscribe(b => { if (bestText) bestText.text = b.ToString(); })
              .AddTo(cd);
    }

    void OnDisable() => cd?.Dispose();
}
