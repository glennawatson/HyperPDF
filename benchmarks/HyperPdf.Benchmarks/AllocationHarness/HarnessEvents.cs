// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;

namespace AllocationHarness;

/// <summary>
/// The markers the allocation harness writes into the EventPipe trace: which benchmark is running and where its measured
/// calls start and stop. <c>scripts/AllocationAudit.cs</c> counts only the allocations sampled between them.
/// </summary>
[EventSource(Name = SourceName)]
internal sealed class HarnessEvents : EventSource
{
    /// <summary>The name of the event source.</summary>
    internal const string SourceName = "HyperPdf.AllocationHarness";

    /// <summary>The event id of a benchmark start.</summary>
    private const int BenchmarkStartId = 1;

    /// <summary>The event id of a window start.</summary>
    private const int WindowStartId = 2;

    /// <summary>The event id of a window stop.</summary>
    private const int WindowStopId = 3;

    /// <summary>Gets the one instance.</summary>
    internal static HarnessEvents Log { get; } = new();

    /// <summary>Marks the start of a benchmark, after its set-up and warm-up call.</summary>
    /// <param name="benchmarkName">The benchmark's full name with its parameters.</param>
    [Event(BenchmarkStartId, Level = EventLevel.Informational)]
    internal void BenchmarkStart(string benchmarkName) => WriteEvent(BenchmarkStartId, benchmarkName);

    /// <summary>Marks the start of a measured window.</summary>
    [Event(WindowStartId, Level = EventLevel.Informational)]
    internal void WindowStart() => WriteEvent(WindowStartId);

    /// <summary>Marks the end of a measured window.</summary>
    /// <param name="totalOperations">The operations the window ran.</param>
    [Event(WindowStopId, Level = EventLevel.Informational)]
    internal void WindowStop(long totalOperations) => WriteEvent(WindowStopId, totalOperations);
}
