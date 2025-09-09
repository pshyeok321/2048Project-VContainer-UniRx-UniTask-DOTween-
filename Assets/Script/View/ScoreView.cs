using UnityEngine;
using UnityEngine.UI;

public sealed class ScoreView : MonoBehaviour
{
    [SerializeField] Text scoreText;
    [SerializeField] Text bestText;

    public void SetScore(int score)
    {
        if (scoreText) scoreText.text = score.ToString();
    }

    public void SetBest(int best)
    {
        if (bestText) bestText.text = best.ToString();
    }
}
