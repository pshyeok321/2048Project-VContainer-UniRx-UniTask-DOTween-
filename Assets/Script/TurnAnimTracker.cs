using System.Threading;

public static class TurnAnimTracker
{
	static int counter = 0;
	public static bool Busy => counter > 0;

	public static void Inc()
	{
		Interlocked.Increment(ref counter);
	}

	public static void Dec()
	{
		int v = Interlocked.Decrement(ref counter);
		if (v < 0)
			counter = 0; // 안전장치
	}
}
