using UniRx;

using UnityEngine;
using UnityEngine.UI;

using VContainer;

public sealed class PlusPresenter : MonoBehaviour
{
    [SerializeField] Text plusText;       // ���� Plus Text
    [SerializeField] Animator animator;   // Plus�� �پ� �ִ� Animator

    GameEvents events;
    CompositeDisposable cd;

    [Inject] public void Construct(GameEvents e) => events = e;

    void OnEnable()
    {
        if (events == null) return;
        cd = new CompositeDisposable();

        events.ScoreChanged
              .Where(e => e.Delta > 0)
              .Subscribe(e =>
              {
                  if (plusText) plusText.text = $"+{e.Delta}    ";
                  if (animator)
                  {
                      animator.ResetTrigger("Plus");     // ����
                      animator.SetTrigger("PlusBack");
                      animator.SetTrigger("Plus");
                  }
              })
              .AddTo(cd);
    }

    void OnDisable() => cd?.Dispose();
}
