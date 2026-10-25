// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Checks that scarce processors extend parsing budgets without removing hang detection.</summary>
public sealed class MutantRunnerTests
{
    /// <summary>The short deadline used to exercise hang detection.</summary>
    private const int ShortMilliseconds = 25;

    /// <summary>The cleanup bound for the synthetic looping worker.</summary>
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromMinutes(1);

    /// <summary>Available processors scale both deadlines, with a finite maximum on one processor.</summary>
    /// <param name="processors">The processors available to the process.</param>
    /// <param name="parsingSeconds">The expected cooperative deadline.</param>
    /// <param name="hardSeconds">The expected hard deadline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 160, 320)]
    [Arguments(4, 40, 80)]
    [Arguments(16, 10, 20)]
    [Arguments(128, 10, 20)]
    public async Task ProcessorBudgetRemainsBounded(int processors, int parsingSeconds, int hardSeconds)
    {
        var limits = MutantRunner.GetLimits(processors);
        await Assert.That(limits.Parsing).IsEqualTo(TimeSpan.FromSeconds(parsingSeconds));
        await Assert.That(limits.Hard).IsEqualTo(TimeSpan.FromSeconds(hardSeconds));
    }

    /// <summary>A looping file worker that ignores the parsing token fails at the hard bound.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopIgnoringCancellationIsReportedAsHung()
    {
        using var stop = new CancellationTokenSource();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var limits = new MutantLimits(TimeSpan.Zero, TimeSpan.FromMilliseconds(ShortMilliseconds));
        try
        {
            var running = MutantRunner.RunAsync(RobustnessSeeds.CreateMini(), limits, (bytes, token) =>
            {
                _ = token;
                try
                {
                    _ = started.TrySetResult();
                    while (!stop.IsCancellationRequested)
                    {
                        // The worker deliberately repeats the same input and ignores its parsing token.
                        _ = bytes.AsSpan().IndexOf((byte)'%');
                    }

                    return new(true, null);
                }
                finally
                {
                    _ = finished.TrySetResult();
                }
            });

            await started.Task.WaitAsync(CleanupLimit);
            var result = await running;
            await Assert.That(result.Opened).IsFalse();
            await Assert.That(result.Problem).IsEqualTo("hung past the hard limit");
        }
        finally
        {
            await stop.CancelAsync();
            await finished.Task.WaitAsync(CleanupLimit);
        }
    }
}
