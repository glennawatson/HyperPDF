// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace AllocationHarness;

/// <summary>
/// Runs the benchmarks for allocation tracing without BenchmarkDotNet's process and build for each benchmark: the
/// generated <see cref="GeneratedCases"/> list calls every benchmark directly, in this one process. Allocations are
/// deterministic, so each benchmark gets one launch, one warm-up call and one measured window.
/// </summary>
internal static class Harness
{
    /// <summary>The marker for the filter argument.</summary>
    private const string FilterOption = "--filter";

    /// <summary>Runs the benchmarks whose names match the filters in the arguments.</summary>
    /// <param name="args">The arguments: <c>--alloc</c>, and <c>--filter</c> followed by globs.</param>
    /// <param name="output">Where progress and failures are written.</param>
    /// <returns>0 when every benchmark ran, 1 when one failed or none matched.</returns>
    internal static async Task<int> RunAsync(string[] args, TextWriter output)
    {
        var filterAt = Array.IndexOf(args, FilterOption);
        var filters = filterAt < 0 ? [] : args[(filterAt + 1)..].Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).Select(Glob).ToArray();
        var all = new List<CaseEntry>();
        GeneratedCases.Add(all);
        var cases = all.Where(entry => filters.Length == 0 || Array.Exists(filters, filter => filter.IsMatch(entry.FilterName))).ToList();
        await output.WriteLineAsync($"Allocation harness: {cases.Count} benchmark(s).");
        var failed = 0;
        foreach (var entry in cases)
        {
            try
            {
                await entry.Run();
            }
            catch (Exception exception)
            {
                failed++;
                await output.WriteLineAsync($"FAILED {entry.Name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        await output.WriteLineAsync($"Allocation harness finished: {cases.Count - failed} ran, {failed} failed.");
        return cases.Count == 0 || failed != 0 ? 1 : 0;
    }

    /// <summary>Turns a glob with <c>*</c> and <c>?</c> into a case-insensitive expression.</summary>
    /// <param name="glob">The glob.</param>
    /// <returns>The expression.</returns>
    private static Regex Glob(string glob) =>
        new($"^{Regex.Escape(glob).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal)}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
