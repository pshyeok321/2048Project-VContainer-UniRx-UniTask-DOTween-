using UnityEngine;
using UnityEngine.UI;

public sealed class PlusView : MonoBehaviour
{
    [SerializeField] Text plusText;
    [SerializeField] Animator animator;

    public void Show(int delta)
    {
        if (plusText) plusText.text = $"+{delta}    ";
        if (animator)
        {
            animator.ResetTrigger("Plus");
            animator.SetTrigger("PlusBack");
            animator.SetTrigger("Plus");
        }
    }
}