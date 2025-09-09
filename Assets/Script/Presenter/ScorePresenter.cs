using UniRx;
using UnityEngine;
using VContainer;

public sealed class ScorePresenter : MonoBehaviour
{    
    ScoreModel model;
    ScoreView view;
    CompositeDisposable cd;

    [Inject]
    public void Construct(ScoreModel model, ScoreView view)
    {
        this.model = model;
        this.view = view;
    }

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
