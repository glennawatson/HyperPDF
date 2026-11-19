// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Cpu;

/// <summary>
/// Keeps small pictures that are drawn smoothly scaled to the same size frame after frame, such as the page pictures of the side
/// panel, so each is scaled once and copied after that. A picture is scaled into a buffer the first time it is drawn, and the
/// buffer is kept once a later frame draws it again, so a picture whose size changes every frame is dropped each frame and never
/// builds up. A kept scaling that goes off screen stays for a bounded time and within a bounded size, so coming back to a tab
/// does not scale its pictures again. Only the thread that draws frames uses it.
/// </summary>
[DebuggerDisplay("ScaledCopyCache: {_copies.Count} scalings, frame {_frame}")]
internal sealed class ScaledCopyCache : IDisposable
{
    /// <summary>The bytes of one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// The most pixels a kept scaling may have. A page picture of the side panel stays under it at twice the usual scale. The picture
    /// that stands in for a whole page while its tiles are made does not at the usual zoom, and must not be kept: it is shown for a
    /// few frames only, so keeping it costs a copy as large as the page for nothing.
    /// </summary>
    private const long MaxPixels = 1L << 18;

    /// <summary>
    /// The most bytes the scalings no frame is drawing may hold together with the ones in use. It covers the page pictures of the
    /// side panel for the tabs a reader moves between, and stays the same however many tabs are open.
    /// </summary>
    private const long MaxKeptBytes = 16L << 20;

    /// <summary>How many frames a scaling may go without being drawn before it is dropped, so nothing is held for good.</summary>
    private const int MaxIdleFrames = 600;

    /// <summary>The scalings seen or kept.</summary>
    private readonly Dictionary<ScaledCopyKey, ScaledCopy> _copies = [];

    /// <summary>The number of the frame being drawn.</summary>
    private int _frame;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var pair in _copies)
        {
            Return(pair.Value.Pixels);
        }

        _copies.Clear();
    }

    /// <summary>Draws a surface smoothly scaled from its scaling, making the scaling when the surface is new or was written again.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame's width, height and bytes per row.</param>
    /// <param name="surface">The surface.</param>
    /// <param name="request">Where the surface goes and how.</param>
    /// <returns><see langword="true"/> when the surface was drawn; <see langword="false"/> when the caller must scale it itself.</returns>
    internal bool TryDraw(Span<byte> frame, in PixelGrid frameGrid, CpuPixelSurface surface, in BlitRequest request)
    {
        if (!IsKept(surface, request))
        {
            return false;
        }

        var version = surface.Version;
        ref var copy = ref CollectionsMarshal.GetValueRefOrAddDefault(_copies, new(surface, request.Width, request.Height), out var known);
        var grid = new PixelGrid(request.Width, request.Height, request.Width * BytesPerPixel);
        var length = grid.Stride * grid.Height;
        var pixels = copy.Pixels;
        if (!known || copy.Version != version || pixels is null)
        {
            // A new surface, or one written again, which shows other pixels: it is scaled once here, for this frame and for the
            // frames after it. Scaling it straight into the frame would mean scaling it again for the next one.
            Return(pixels);
            pixels = ArrayPool<byte>.Shared.Rent(length);
            CpuBlit.Draw(pixels.AsSpan(0, length), grid, surface.Pixels, surface.Grid, new(0, 0, grid.Width, grid.Height, true, true));
            copy = new(pixels, version, _frame, false);
        }
        else
        {
            // Drawn again in a later frame, the scaling is one to keep when it goes off screen.
            copy = new(pixels, version, _frame, copy.Confirmed || copy.Frame != _frame);
        }

        // Scaling writes over what is in the frame, so the kept scaling is copied the same way.
        CpuBlit.Draw(frame, frameGrid, pixels.AsSpan(0, length), grid, request with { Opaque = true });
        return true;
    }

    /// <summary>
    /// Ends a frame. A scaling the frame did not draw stays for a while, so the pictures of a tab the reader comes back to are
    /// copied and not scaled again. It is dropped when its picture was written again or let go, when it has gone unused for
    /// <see cref="MaxIdleFrames"/> frames, or, oldest first, when the scalings together pass <see cref="MaxKeptBytes"/>.
    /// </summary>
    internal void EndFrame()
    {
        var kept = 0L;
        foreach (var pair in _copies)
        {
            var copy = pair.Value;
            var idle = _frame - copy.Frame;
            if (idle != 0 && (!copy.Confirmed || idle > MaxIdleFrames || copy.Version != pair.Key.Surface.Version))
            {
                // A scaling drawn in one frame only is not kept either, so a picture whose size changes every frame never is.
                Return(copy.Pixels);
                _ = _copies.Remove(pair.Key);
                continue;
            }

            kept += SizeOf(pair.Key);
        }

        while (kept > MaxKeptBytes && TryDropOldest(out var freed))
        {
            kept -= freed;
        }

        _frame++;
    }

    /// <summary>Says whether a draw is one whose scaling may be kept: smooth, scaled, and small.</summary>
    /// <param name="surface">The surface.</param>
    /// <param name="request">Where the surface goes and how.</param>
    /// <returns><see langword="true"/> when its scaling may be kept.</returns>
    private static bool IsKept(CpuPixelSurface surface, in BlitRequest request) =>
        request.Smooth
        && request.Width > 0
        && request.Height > 0
        && (request.Width != surface.Width || request.Height != surface.Height)
        && (long)request.Width * request.Height <= MaxPixels;

    /// <summary>Gets the bytes a kept scaling holds.</summary>
    /// <param name="key">The scaling.</param>
    /// <returns>The bytes of its pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SizeOf(in ScaledCopyKey key) => (long)key.Width * key.Height * BytesPerPixel;

    /// <summary>Gives scaled pixels back to the pool.</summary>
    /// <param name="pixels">The pixels, or <see langword="null"/> when none were kept.</param>
    private static void Return(byte[]? pixels)
    {
        if (pixels is not null)
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }

    /// <summary>Drops the kept scaling that has gone longest without being drawn, leaving the ones the frame drew.</summary>
    /// <param name="freed">The bytes it held.</param>
    /// <returns><see langword="true"/> when a scaling was dropped.</returns>
    private bool TryDropOldest(out long freed)
    {
        freed = 0;
        var oldest = default(ScaledCopyKey);
        var oldestFrame = _frame;
        foreach (var pair in _copies)
        {
            if (pair.Value.Pixels is null || pair.Value.Frame >= oldestFrame)
            {
                continue;
            }

            oldest = pair.Key;
            oldestFrame = pair.Value.Frame;
        }

        if (oldestFrame == _frame || !_copies.Remove(oldest, out var copy))
        {
            return false;
        }

        Return(copy.Pixels);
        freed = SizeOf(oldest);
        return true;
    }
}
