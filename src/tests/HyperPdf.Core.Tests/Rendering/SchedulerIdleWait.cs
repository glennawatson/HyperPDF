// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Waits for a render scheduler to go idle after its last tile is reported complete.</summary>
internal static class SchedulerIdleWait
{
    /// <summary>The longest the scheduler is given to go idle.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>The pause between looks at the scheduler.</summary>
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(5);

    /// <summary>Waits until the scheduler has no queued or running work. A tile is reported complete just before its worker marks itself idle.</summary>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="cancellationToken">Ends the wait when the test is cancelled.</param>
    /// <returns><see langword="true"/> when the scheduler went idle within the limit.</returns>
    internal static async Task<bool> UntilIdleAsync(RenderScheduler scheduler, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        using var timer = new PeriodicTimer(Step);
        try
        {
            while (!scheduler.IsIdle)
            {
                _ = await timer.WaitForNextTickAsync(limit.Token);
            }

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return scheduler.IsIdle;
        }
    }
}
