// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;

namespace HyperPdf.Benchmarks;

/// <summary>Runs lookup benchmarks without loading the PDF engine or its dependencies.</summary>
public static class Program
{
    /// <summary>Measures the selected cases and rejects empty or failed measurements.</summary>
    /// <param name="args">The BenchmarkDotNet command-line arguments.</param>
    /// <returns>Zero when every selected benchmark produced statistics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<int> Main(string[] args) => Array.IndexOf(args, "--alloc") >= 0
        ? AllocationHarness.Harness.RunAsync(args, Console.Out)
        : Task.FromResult(RunBenchmarks(args));

    /// <summary>Runs throughput measurements and requires statistics for every selected case.</summary>
    /// <param name="args">The BenchmarkDotNet options.</param>
    /// <returns>Zero when the selected measurements succeed.</returns>
    private static int RunBenchmarks(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(UiTextBenchmarks).Assembly).Run(args).ToArray();
        var failed = Array.Exists(summaries, static summary => summary.HasCriticalValidationErrors || summary.Reports.Any(static report => report.ResultStatistics is null));
        return summaries.Length == 0 || failed ? 1 : 0;
    }
}
