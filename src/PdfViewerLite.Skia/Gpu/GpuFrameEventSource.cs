// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Reports compositor-session submission time to EventPipe.</summary>
[EventSource(Name = "PdfViewerLite.Skia.Frames")]
internal sealed class GpuFrameEventSource : EventSource
{
    /// <summary>Gets the process-wide frame event source.</summary>
    internal static GpuFrameEventSource Log { get; } = new();

    /// <summary>Reports elapsed time from target acquisition through platform session disposal.</summary>
    /// <param name="backend">The Skia GPU backend value.</param>
    /// <param name="submissionMicroseconds">The session duration, excluding physical scanout.</param>
    [Event(1, Level = EventLevel.Informational)]
    internal void FrameSessionSubmitted(int backend, int submissionMicroseconds)
    {
        if (IsEnabled())
        {
            WriteEvent(1, backend, submissionMicroseconds);
        }
    }
}
