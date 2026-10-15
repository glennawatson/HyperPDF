// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reuses a backend thread's conversion arrays while Skia copies vertex data into owned native resources.</summary>
internal sealed class SkiaVertexArrays
{
    /// <summary>The converted vertex positions.</summary>
    private SKPoint[] _positions = [];

    /// <summary>The converted texture coordinates.</summary>
    private SKPoint[] _texture = [];

    /// <summary>The converted vertex colours.</summary>
    private SKColor[] _colors = [];

    /// <summary>Converts vertex positions until the next call on this session.</summary>
    /// <param name="positions">The managed coordinates.</param>
    /// <returns>The temporary native coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SKPoint[] Positions(ReadOnlySpan<PdfPoint> positions) => CopyPoints(ref _positions, positions);

    /// <summary>Converts texture coordinates until the next call on this session.</summary>
    /// <param name="texture">The managed texture coordinates.</param>
    /// <returns>The temporary native coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SKPoint[] Texture(ReadOnlySpan<PdfPoint> texture) => CopyPoints(ref _texture, texture);

    /// <summary>Converts colours until the next call on this session.</summary>
    /// <param name="colors">The managed colours.</param>
    /// <returns>The temporary native colours.</returns>
    internal SKColor[] Colors(ReadOnlySpan<PdfColor> colors)
    {
        if (_colors.Length != colors.Length)
        {
            _colors = new SKColor[colors.Length];
        }

        for (var i = 0; i < colors.Length; i++)
        {
            _colors[i] = SkiaConversions.ToSkColor(colors[i]);
        }

        return _colors;
    }

    /// <summary>Copies coordinates into an exact-length reusable array.</summary>
    /// <param name="target">The session-owned array.</param>
    /// <param name="source">The coordinates to convert.</param>
    /// <returns>The array Skia copies during this call.</returns>
    private static SKPoint[] CopyPoints(ref SKPoint[] target, ReadOnlySpan<PdfPoint> source)
    {
        if (target.Length != source.Length)
        {
            target = new SKPoint[source.Length];
        }

        for (var i = 0; i < source.Length; i++)
        {
            target[i] = new(source[i].X, source[i].Y);
        }

        return target;
    }
}
