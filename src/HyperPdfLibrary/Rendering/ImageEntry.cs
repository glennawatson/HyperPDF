// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A decoded image ready to draw, shared through the <see cref="ImageCache"/>. Each user acquires it and releases it when
/// done; the backend image is disposed only once the cache has evicted it and no user holds it. Recorded pictures retain
/// the resources they need, so disposing the image never invalidates a recorded page.
/// </summary>
[DebuggerDisplay("ImageEntry: {Image.Width}x{Image.Height} mask {IsMask}")]
internal sealed class ImageEntry
{
    /// <summary>Guards the user count and the evicted flag.</summary>
    private readonly Lock _gate = new();

    /// <summary>The users holding the entry.</summary>
    private int _users;

    /// <summary>Whether the cache has let go of the entry.</summary>
    private bool _evicted;

    /// <summary>Initializes a new instance of the <see cref="ImageEntry"/> class.</summary>
    /// <param name="image">The backend image, which the entry owns.</param>
    /// <param name="isMask">Whether the image is a stencil mask painted with the fill colour.</param>
    /// <param name="interpolate">Whether the image asks for smoothing when scaled up.</param>
    internal ImageEntry(IPdfRenderImage image, bool isMask, bool interpolate)
    {
        Image = image;
        IsMask = isMask;
        Interpolate = interpolate;
        Bytes = image.PixelBytes;
    }

    /// <summary>Gets the owned backend image.</summary>
    internal IPdfRenderImage Image { get; }

    /// <summary>Gets a value indicating whether the image is a stencil mask painted with the fill colour.</summary>
    internal bool IsMask { get; }

    /// <summary>Gets a value indicating whether the image asks for smoothing when scaled up.</summary>
    internal bool Interpolate { get; }

    /// <summary>Gets the pixel memory the image holds, in bytes.</summary>
    internal long Bytes { get; }

    /// <summary>Gets or sets the entry's place in the cache's recency list; only the cache touches it, under its lock.</summary>
    internal LinkedListNode<ImageEntry>? Node { get; set; }

    /// <summary>Gets or sets the stream the cache holds the entry under; only the cache touches it, under its lock.</summary>
    internal ImageKey? Key { get; set; }

    /// <summary>Gets a value indicating whether the backend image has been disposed.</summary>
    internal bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _evicted && _users == 0;
            }
        }
    }

    /// <summary>Marks the entry as in use.</summary>
    internal void Acquire()
    {
        lock (_gate)
        {
            _users++;
        }
    }

    /// <summary>Ends a use, disposing the image when the cache has already evicted it.</summary>
    internal void Release()
    {
        lock (_gate)
        {
            _users--;
            DisposeIfUnused();
        }
    }

    /// <summary>Marks the entry as evicted, disposing the image now if nobody uses it.</summary>
    internal void Evict()
    {
        lock (_gate)
        {
            _evicted = true;
            DisposeIfUnused();
        }
    }

    /// <summary>Disposes the image when evicted and idle. The caller holds the lock.</summary>
    private void DisposeIfUnused()
    {
        if (_evicted && _users <= 0)
        {
            Image.Dispose();
        }
    }
}
