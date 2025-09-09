using UniRx;

public sealed class GameOverModel
{
    public BoolReactiveProperty IsOver { get; } = new BoolReactiveProperty(false);

    public void SetGameOver() => IsOver.Value = true;

    public void Reset() => IsOver.Value = false;
}