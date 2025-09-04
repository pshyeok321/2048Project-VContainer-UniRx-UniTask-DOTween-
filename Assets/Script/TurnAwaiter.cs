using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public static class TurnAwaiter
{
    /// <summary>
    /// TurnAnimTracker.Busy == false 가 될 때까지 대기. (안전 타임아웃 포함)
    /// </summary>
    public static async UniTask WaitAnimationsIdleAsync(
        CancellationToken ct,
        int safetyTimeoutMs = 4000)
    {
        // safety timeout과 사용자 취소 모두 존중
        using var timeoutCts = new CancellationTokenSource(safetyTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        await UniTask.WaitUntil(() => !TurnAnimTracker.Busy, cancellationToken: linked.Token)
                     .SuppressCancellationThrow(); // 타임아웃/취소시 예외 억제(상위 로직에서 분기)
    }
}
