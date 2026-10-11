// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;

namespace PdfViewerLite.GpuProbe;

/// <summary>Marks one selected measurement phase for the EventPipe allocation audit.</summary>
internal static class GpuProbeEventScope
{
    /// <summary>The environment variable naming the measured phase.</summary>
    private const string AuditPhaseVariable = "PDFVIEWERLITE_GPU_AUDIT_PHASE";

    /// <summary>Emits the selected phase's markers.</summary>
    private static readonly ProbeEvents Events = new();

    /// <summary>Begins a selected measurement phase.</summary>
    /// <param name="phase">The phase name.</param>
    /// <param name="operations">The number of operations in the phase.</param>
    internal static void Start(string phase, int operations)
    {
        if (Environment.GetEnvironmentVariable(AuditPhaseVariable) == phase)
        {
            Events.PhaseStart(phase, operations);
        }
    }

    /// <summary>Ends a selected measurement phase.</summary>
    /// <param name="phase">The phase name.</param>
    internal static void Stop(string phase)
    {
        if (Environment.GetEnvironmentVariable(AuditPhaseVariable) == phase)
        {
            Events.PhaseStop();
        }
    }

    /// <summary>EventPipe events used only by the probe.</summary>
    [EventSource(Name = "PdfViewerLite.GpuProbe")]
    private sealed class ProbeEvents : EventSource
    {
        /// <summary>The event id that closes a measured phase.</summary>
        private const int PhaseStopEventId = 2;

        /// <summary>Starts a named measurement phase.</summary>
        /// <param name="phase">The phase name.</param>
        /// <param name="operations">The number of operations.</param>
        [Event(1, Level = EventLevel.Informational)]
        internal void PhaseStart(string phase, int operations) => WriteEvent(1, phase, operations);

        /// <summary>Ends the selected measurement phase.</summary>
        [Event(PhaseStopEventId, Level = EventLevel.Informational)]
        internal void PhaseStop() => WriteEvent(PhaseStopEventId);
    }
}
