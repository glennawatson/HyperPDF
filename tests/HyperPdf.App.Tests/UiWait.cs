// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Headless;
using Avalonia.Threading;

namespace HyperPdf.App.Tests;

/// <summary>Pumps the headless dispatcher and renderer until a condition holds.</summary>
internal static class UiWait
{
    /// <summary>
    /// The longest wait. A wait returns as soon as its condition holds, so this only bounds a stuck test; it is long
    /// enough for a slow shared runner rendering and searching under load.
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long to sleep between pumps.</summary>
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Runs dispatcher jobs until <paramref name="condition"/> is true.</summary>
    /// <param name="condition">The condition, which must come to hold; a wait cannot show that something does not happen.</param>
    /// <param name="state">Describes the state the condition reads, for the timeout message, or <see langword="null"/>.</param>
    /// <param name="file">The calling file, filled in by the compiler.</param>
    /// <param name="line">The calling line, filled in by the compiler.</param>
    /// <returns><see langword="true"/>, once the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within the timeout.</exception>
    internal static async Task<bool> UntilAsync(Func<bool> condition, Func<string>? state = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < DefaultTimeout)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }

            await Task.Delay(PumpInterval);
        }

        // A wait that runs out fails here, naming the caller, instead of costing its whole timeout and passing.
        return condition() ? true : throw new TimeoutException($"The condition at {Path.GetFileName(file)}:{line} did not hold within {DefaultTimeout.TotalSeconds} seconds. {state?.Invoke()}");
    }
}
