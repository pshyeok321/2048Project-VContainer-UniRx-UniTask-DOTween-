using UniRx;

public sealed class PlusModel : System.IDisposable
{
    public Subject<int> Delta { get; } = new Subject<int>();

    public void Publish(int amount)
    {
        if (amount > 0)
            Delta.OnNext(amount);
    }

    public void Dispose()
    {
        Delta?.Dispose();
    }
}