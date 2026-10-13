// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// Measures the managed bytes the current thread allocates while some work runs, and keeps the smallest result over
/// several attempts. Work that allocates does so in every attempt, so the smallest attempt still shows it. A one-off
/// runtime event, such as tier-up code installation or a lazy runtime table, lands in one attempt and is dropped.
/// </summary>
public static class AllocationProbe
{
    /// <summary>The attempts made when the caller names none.</summary>
    private const int DefaultAttempts = 5;

    /// <summary>Runs the work several times and gets the fewest bytes any run allocated on this thread.</summary>
    /// <param name="work">The work, already warmed up by the caller.</param>
    /// <returns>The smallest number of bytes allocated by one run.</returns>
    public static long Minimum(Action work) => Minimum(work, DefaultAttempts);

    /// <summary>Runs the work several times and gets the fewest bytes any run allocated on this thread.</summary>
    /// <param name="work">The work, already warmed up by the caller.</param>
    /// <param name="attempts">How many runs to make, at least one.</param>
    /// <returns>The smallest number of bytes allocated by one run.</returns>
    public static long Minimum(Action work, int attempts)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        var smallest = long.MaxValue;
        for (var attempt = 0; attempt < attempts && smallest > 0; attempt++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            work();
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }
}
