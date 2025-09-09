using UniRx;
using UnityEngine;

public sealed class ScoreModel
{
    public ReactiveProperty<int> Score { get; } = new ReactiveProperty<int>(0);
    public ReactiveProperty<int> Best { get; } = new ReactiveProperty<int>(0);

    public void Reset()
    {
        Score.Value = 0;
        Best.Value = PlayerPrefs.GetInt("BestScore", 0);
    }

    public bool Add(int amount)
    {
        if (amount <= 0) return false;
        Score.Value += amount;
        bool bestUpdated = false;
        if (Score.Value > Best.Value)
        {
            Best.Value = Score.Value;
            PlayerPrefs.SetInt("BestScore", Best.Value);
            bestUpdated = true;
        }
        return bestUpdated;
    }
}
