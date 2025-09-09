using UniRx;
using UnityEngine;
using VContainer;

public sealed class GameOverPresenter : MonoBehaviour
{
    GameOverModel model;
    GameOverView view;
    CompositeDisposable cd;

    [Inject] public void Construct(GameOverModel model, GameOverView view) 
    {
        this.model = model;
        this.view = view;
    }

    void OnEnable()
    {
        if (model == null || view == null) return;
        cd = new CompositeDisposable();
        model.IsOver
            .Subscribe(view.ShowQuitPanel)
            .AddTo(cd);
    }

    void OnDisable() => cd?.Dispose();
}
