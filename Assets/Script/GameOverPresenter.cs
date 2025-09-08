using UniRx;
using UnityEngine;
using VContainer;

public sealed class GameOverPresenter : MonoBehaviour
{
    [SerializeField] GameObject quitPanel; // ���� Quit �г� �巡��
    GameEvents events;
    CompositeDisposable cd;

    [Inject] public void Construct(GameEvents e) => events = e;

    void OnEnable()
    {
        if (events == null) return;
        cd = new CompositeDisposable();
        events.GameOver.Subscribe(_ =>
        {
            if (quitPanel) quitPanel.SetActive(true);
        }).AddTo(cd);
    }

    void OnDisable() => cd?.Dispose();
}
