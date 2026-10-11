// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Releases retired GPU images while their owning graphics context is current.</summary>
public static class SkiaGpuImageRetirement
{
    /// <summary>Per-context queues that vanish after their context has no managed owners.</summary>
    private static readonly ConditionalWeakTable<GRContext, ContextState> States = new();

    /// <summary>Queues an image for release on the context that created it.</summary>
    /// <param name="context">The graphics context that owns the image.</param>
    /// <param name="image">The image to release.</param>
    public static void Enqueue(GRContext context, SKImage image)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(image);
        var state = States.GetValue(context, static _ => new ContextState());
        lock (state.Gate)
        {
            if (state.Retired)
            {
                // Abandoned contexts no longer have live GPU commands that refer to the image.
                image.Dispose();
                return;
            }

            state.Pending.Enqueue(image);
        }
    }

    /// <summary>Releases queued images while the given context is current.</summary>
    /// <param name="context">The current graphics context.</param>
    public static void Drain(GRContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!States.TryGetValue(context, out var state))
        {
            return;
        }

        lock (state.Gate)
        {
            while (state.Pending.TryDequeue(out var image))
            {
                image.Dispose();
            }
        }
    }

    /// <summary>Closes a context and releases any images queued before abandonment.</summary>
    /// <param name="context">The context about to be abandoned.</param>
    public static void Retire(GRContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var state = States.GetValue(context, static _ => new ContextState());
        lock (state.Gate)
        {
            state.Retired = true;
            while (state.Pending.TryDequeue(out var image))
            {
                image.Dispose();
            }
        }
    }

    /// <summary>Serializes image release for one graphics context.</summary>
    private sealed class ContextState
    {
        /// <summary>Gets the gate for this context's release queue.</summary>
        internal Lock Gate { get; } = new();

        /// <summary>Gets the images waiting for the graphics thread.</summary>
        internal Queue<SKImage> Pending { get; } = new();

        /// <summary>Gets or sets whether the context can no longer submit GPU work.</summary>
        internal bool Retired { get; set; }
    }
}
