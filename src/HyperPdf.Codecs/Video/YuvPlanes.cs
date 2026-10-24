// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Video;

/// <summary>The three planes of an 8 bit 4:2:0 picture, each row after row with no padding. The colour planes have half the width and height, rounded up.</summary>
/// <param name="y">The luma plane.</param>
/// <param name="u">The Cb plane.</param>
/// <param name="v">The Cr plane.</param>
/// <param name="width">The width of the picture in pixels.</param>
/// <param name="height">The height of the picture in pixels.</param>
[DebuggerDisplay("YuvPlanes: {Width}x{Height}")]
public readonly ref struct YuvPlanes(ReadOnlySpan<byte> y, ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, int width, int height)
{
    /// <summary>Gets the luma plane.</summary>
    public ReadOnlySpan<byte> Y { get; } = y;

    /// <summary>Gets the Cb plane.</summary>
    public ReadOnlySpan<byte> U { get; } = u;

    /// <summary>Gets the Cr plane.</summary>
    public ReadOnlySpan<byte> V { get; } = v;

    /// <summary>Gets the width of the picture in pixels.</summary>
    public int Width { get; } = width;

    /// <summary>Gets the height of the picture in pixels.</summary>
    public int Height { get; } = height;

    /// <summary>Gets the width of the colour planes in samples.</summary>
    public int ChromaWidth => (Width + 1) >> 1;

    /// <summary>Gets the height of the colour planes in samples.</summary>
    public int ChromaHeight => (Height + 1) >> 1;
}
