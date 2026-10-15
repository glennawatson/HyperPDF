// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdf.Skia.Gpu;

/// <summary>Owns a graphics backend's queue of images released while its context is current.</summary>
[DebuggerDisplay("Image retirement: retired={_retired}")]
public sealed class SkiaGpuImageRetirement
{
    /// <summary>Serializes retirement and image enqueueing for this backend.</summary>
    private readonly Lock _gate = new();

    /// <summary>The images awaiting the graphics thread.</summary>
    private Queue<SKImage>? _pending;

    /// <summary>Whether the owning context has been abandoned.</summary>
    private bool _retired;

    /// <summary>Queues an image owned by this graphics backend.</summary>
    /// <param name="image">The image whose last tile owner released it.</param>
    public void Enqueue(SKImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_gate)
        {
            if (_retired)
            {
                image.Dispose();
                return;
            }

            (_pending ??= new()).Enqueue(image);
        }
    }

    /// <summary>Releases queued images while the owning context is current.</summary>
    public void Drain()
    {
        if (Volatile.Read(ref _pending) is null)
        {
            return;
        }

        lock (_gate)
        {
            DrainLocked();
        }
    }

    /// <summary>Closes the abandoned context's queue and releases its remaining images.</summary>
    public void Retire()
    {
        lock (_gate)
        {
            _retired = true;
            DrainLocked();
        }
    }

    /// <summary>Releases pending images under the backend's queue lock.</summary>
    private void DrainLocked()
    {
        while (_pending is not null && _pending.TryDequeue(out var image))
        {
            image.Dispose();
        }
    }
}
