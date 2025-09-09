using UnityEngine;

public sealed class GameOverView : MonoBehaviour
{
    [SerializeField] GameObject quitPanel;

    public void ShowQuitPanel(bool show)
    {
        if (quitPanel) quitPanel.SetActive(show);
    }
}