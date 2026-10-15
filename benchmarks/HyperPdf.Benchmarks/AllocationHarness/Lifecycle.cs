// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AllocationHarness;

/// <summary>
/// What the generated code calls to run one benchmark: global set-up, one warm-up call, a measured window of calls long
/// enough for the runtime's allocation sampling, and global clean-up. The window is marked in the EventPipe trace for
/// <c>scripts/AllocationAudit.cs</c>.
/// </summary>
internal static class Lifecycle
{
    /// <summary>How long a measured window runs, in milliseconds, so the runtime's sampling of about every 100 KB has data.</summary>
    private const double WindowMilliseconds = 150;

    /// <summary>The most calls a window makes.</summary>
    private const int MaxCalls = 100_000;

    /// <summary>The most calls a window makes when set-up and clean-up run around each one.</summary>
    private const int MaxHookedCalls = 64;

    /// <summary>Runs one benchmark.</summary>
    /// <param name="name">The benchmark's full name with its parameters.</param>
    /// <param name="operationsPerInvoke">The operations one call stands for.</param>
    /// <param name="globalSetup">The global set-up, or <see langword="null"/>.</param>
    /// <param name="call">One measured call.</param>
    /// <param name="iterationSetup">Runs before each measured call, or <see langword="null"/>.</param>
    /// <param name="iterationCleanup">Runs after each measured call, or <see langword="null"/>.</param>
    /// <param name="globalCleanup">The global clean-up, or <see langword="null"/>.</param>
    /// <returns>A task.</returns>
    internal static async Task RunAsync(
        string name,
        int operationsPerInvoke,
        Func<ValueTask>? globalSetup,
        Func<ValueTask> call,
        Func<ValueTask>? iterationSetup,
        Func<ValueTask>? iterationCleanup,
        Func<ValueTask>? globalCleanup)
    {
        await Run(globalSetup);
        try
        {
            await Run(iterationSetup);
            await call();
            await Run(iterationCleanup);
            HarnessEvents.Log.BenchmarkStart(name);
            await MeasureAsync(call, iterationSetup, iterationCleanup, operationsPerInvoke);
        }
        finally
        {
            await Run(globalCleanup);
        }
    }

    /// <summary>Consumes a result so the call that made it cannot be dropped, without boxing it.</summary>
    /// <typeparam name="T">The result's type.</typeparam>
    /// <param name="value">The result.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Use<T>(T value) => _ = value;

    /// <summary>Formats one parameter of a benchmark name the way BenchmarkDotNet does: strings are quoted.</summary>
    /// <param name="name">The parameter's name, with any leading separator.</param>
    /// <param name="value">The value.</param>
    /// <returns>The text, such as <c>Mode: "Paths"</c>.</returns>
    internal static string Part(string name, object? value) =>
        value is string text ? $"{name}: \"{text}\"" : $"{name}: {Convert.ToString(value, CultureInfo.InvariantCulture)}";

    /// <summary>Runs a hook, if there is one.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>A task.</returns>
    private static async ValueTask Run(Func<ValueTask>? hook)
    {
        if (hook is not null)
        {
            await hook();
        }
    }

    /// <summary>
    /// Runs calls until the window's time is spent, so the runtime's allocation sampling has enough calls to attribute; the
    /// window is marked in the trace and its call count is reported when it closes. With iteration set-up or clean-up, each
    /// call is its own window so the hooks' allocations are left out.
    /// </summary>
    /// <param name="call">One measured call.</param>
    /// <param name="iterationSetup">Runs before each call, or <see langword="null"/>.</param>
    /// <param name="iterationCleanup">Runs after each call, or <see langword="null"/>.</param>
    /// <param name="operationsPerCall">The operations one call stands for.</param>
    /// <returns>A task.</returns>
    private static async Task MeasureAsync(Func<ValueTask> call, Func<ValueTask>? iterationSetup, Func<ValueTask>? iterationCleanup, int operationsPerCall)
    {
        var started = Stopwatch.GetTimestamp();
        if (iterationSetup is null && iterationCleanup is null)
        {
            var calls = 0;
            HarnessEvents.Log.WindowStart();
            do
            {
                await call();
                calls++;
            }
            while (calls < MaxCalls && Stopwatch.GetElapsedTime(started).TotalMilliseconds < WindowMilliseconds);

            HarnessEvents.Log.WindowStop((long)calls * operationsPerCall);
            return;
        }

        for (var calls = 0; calls < MaxHookedCalls && Stopwatch.GetElapsedTime(started).TotalMilliseconds < WindowMilliseconds; calls++)
        {
            await Run(iterationSetup);
            HarnessEvents.Log.WindowStart();
            await call();
            HarnessEvents.Log.WindowStop(operationsPerCall);
            await Run(iterationCleanup);
        }
    }
}
