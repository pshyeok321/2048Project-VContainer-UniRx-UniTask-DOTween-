using UniRx;
using UnityEngine;
using VContainer;

public sealed class ScorePresenter : MonoBehaviour
{
    [SerializeField] ScoreView view;

    ScoreModel model;
    CompositeDisposable cd;

    [Inject] public void Construct(ScoreModel model) => this.model = model;

    void OnEnable()
    {
        if (model == null || view == null) return;
        cd = new CompositeDisposable();

        model.Score
            .Subscribe(view.SetScore)
            .AddTo(cd);

        model.Best
            .Subscribe(view.SetBest)
            .AddTo(cd);
    }

    void OnDisable() => cd?.Dispose();
}
