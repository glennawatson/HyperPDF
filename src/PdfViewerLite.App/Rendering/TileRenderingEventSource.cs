// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;

namespace PdfViewerLite.App.Rendering;

/// <summary>Reports tile path selection and actual composition to EventPipe.</summary>
[EventSource(Name = "PdfViewerLite.Rendering.Tiles")]
internal sealed class TileRenderingEventSource : EventSource
{
    /// <summary>The event id for a GPU tile drawn into an owned image.</summary>
    private const int GpuSnapshotEventId = 1;

    /// <summary>The event id for a GPU image drawn into the compositor.</summary>
    private const int GpuPresentedEventId = 2;

    /// <summary>The event id for a CPU fallback tile.</summary>
    private const int SoftwarePreparedEventId = 3;

    /// <summary>Gets the process-wide tile event source.</summary>
    internal static TileRenderingEventSource Log { get; } = new();

    /// <summary>Reports that a PDF recording created a GPU tile image.</summary>
    /// <param name="width">The tile width in pixels.</param>
    /// <param name="height">The tile height in pixels.</param>
    [Event(GpuSnapshotEventId, Level = EventLevel.Informational)]
    internal void GpuSnapshot(int width, int height)
    {
        if (IsEnabled())
        {
            WriteEvent(GpuSnapshotEventId, width, height);
        }
    }

    /// <summary>Reports that the compositor drew a retained GPU tile image.</summary>
    /// <param name="width">The tile width in pixels.</param>
    /// <param name="height">The tile height in pixels.</param>
    [Event(GpuPresentedEventId, Level = EventLevel.Informational)]
    internal void GpuPresented(int width, int height)
    {
        if (IsEnabled())
        {
            WriteEvent(GpuPresentedEventId, width, height);
        }
    }

    /// <summary>Reports that a tile prepared CPU pixels for software presentation.</summary>
    /// <param name="width">The tile width in pixels.</param>
    /// <param name="height">The tile height in pixels.</param>
    [Event(SoftwarePreparedEventId, Level = EventLevel.Informational)]
    internal void SoftwarePrepared(int width, int height)
    {
        if (IsEnabled())
        {
            WriteEvent(SoftwarePreparedEventId, width, height);
        }
    }
}
