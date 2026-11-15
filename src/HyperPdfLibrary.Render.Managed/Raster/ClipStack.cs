// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// The clip in force while a picture is drawn: a rectangle of pixels, and inside it a coverage mask when the clip is not a rectangle
/// on pixel borders. Saving and restoring is cheap, because a mask is only made when a clip path is set.
/// </summary>
[DebuggerDisplay("ClipStack: depth {_saved.Count}, masks {_maskCount}")]
internal sealed class ClipStack : IDisposable
{
    /// <summary>The clips saved, innermost last.</summary>
    private readonly List<ClipEntry> _saved = [];

    /// <summary>The masks made so far, reused from picture to picture. Each is as large as the surface.</summary>
    private readonly List<byte[]> _masks = [];

    /// <summary>The clip in force.</summary>
    private ClipEntry _current;

    /// <summary>The masks in use.</summary>
    private int _maskCount;

    /// <summary>The width of the surface.</summary>
    private int _width;

    /// <summary>The height of the surface.</summary>
    private int _height;

    /// <summary>Gets the pixels that can be drawn to.</summary>
    internal PixelRect Bounds => _current.Bounds;

    /// <summary>Gets a value indicating whether a mask applies inside the bounds.</summary>
    internal bool HasMask => _current.Mask >= 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var mask in _masks)
        {
            ArrayPool<byte>.Shared.Return(mask);
        }

        _masks.Clear();
        _maskCount = 0;
    }

    /// <summary>Starts a picture with the whole surface open.</summary>
    /// <param name="width">The width of the surface.</param>
    /// <param name="height">The height of the surface.</param>
    internal void Reset(int width, int height)
    {
        if (width != _width || height != _height)
        {
            // Masks are as large as the surface, so the ones of another size are given back.
            Dispose();
        }

        _width = width;
        _height = height;
        _saved.Clear();
        _maskCount = 0;
        _current = new(new(0, 0, width, height), -1, 0);
    }

    /// <summary>Remembers the clip.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Save() => _saved.Add(_current with { MaskCount = _maskCount });

    /// <summary>Goes back to the clip last remembered.</summary>
    internal void Restore()
    {
        if (_saved.Count == 0)
        {
            return;
        }

        _current = _saved[^1];
        _maskCount = _current.MaskCount;
        _saved.RemoveAt(_saved.Count - 1);
    }

    /// <summary>Narrows the clip to a rectangle of whole pixels.</summary>
    /// <param name="rectangle">The rectangle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void IntersectRect(in PixelRect rectangle) => _current = _current with { Bounds = _current.Bounds.Intersect(rectangle) };

    /// <summary>Gets the mask of part of a line.</summary>
    /// <param name="y">The line.</param>
    /// <param name="x">The first pixel.</param>
    /// <param name="length">The number of pixels.</param>
    /// <returns>The coverage the clip lets through. Only valid while <see cref="HasMask"/> holds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> MaskRow(int y, int x, int length) => _masks[_current.Mask].AsSpan((y * _width) + x, length);

    /// <summary>Narrows the clip to a shape.</summary>
    /// <param name="shape">The shape's coverage, with its edges added, over pixels inside the clip's bounds.</param>
    /// <param name="evenOdd">Whether the even-odd rule fills the shape.</param>
    /// <param name="scratch">A line of bytes as wide as the surface.</param>
    internal void IntersectShape(CoverageBuffer shape, bool evenOdd, Span<byte> scratch)
    {
        var bounds = new PixelRect(shape.Left, shape.Top, shape.Left + shape.Width, shape.Top + shape.Height);
        var mask = RentMask();
        var previous = _current.Mask;
        for (var row = 0; row < shape.Height; row++)
        {
            var y = shape.Top + row;
            var line = mask.AsSpan((y * _width) + shape.Left, shape.Width);
            var range = shape.ReadRow(row, evenOdd, scratch);
            line.Clear();
            if (range.IsEmpty)
            {
                continue;
            }

            var covered = line[range.Start..range.End];
            scratch[range.Start..range.End].CopyTo(covered);
            if (previous >= 0)
            {
                SpanBlender.Multiply(covered, _masks[previous].AsSpan((y * _width) + shape.Left + range.Start, covered.Length));
            }
        }

        _current = _current with { Bounds = bounds, Mask = _maskCount - 1 };
    }

    /// <summary>Gets a mask to fill, making one when none is free.</summary>
    /// <returns>The mask.</returns>
    private byte[] RentMask()
    {
        if (_maskCount == _masks.Count)
        {
            _masks.Add(ArrayPool<byte>.Shared.Rent(_width * _height));
        }

        _maskCount++;
        return _masks[_maskCount - 1];
    }

    /// <summary>One clip: its bounds, its mask and how many masks were in use when it was saved.</summary>
    /// <param name="Bounds">The pixels that can be drawn to.</param>
    /// <param name="Mask">The mask's place in the list, or -1 when the clip is the whole of its bounds.</param>
    /// <param name="MaskCount">The masks in use when the clip was saved, which are the ones kept when it is restored.</param>
    private readonly record struct ClipEntry(PixelRect Bounds, int Mask, int MaskCount);
}
