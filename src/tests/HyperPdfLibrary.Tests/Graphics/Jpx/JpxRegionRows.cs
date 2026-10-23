// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Serves the rows of a rectangle of a coefficient buffer, standing in for a sub-band or a lower resolution.</summary>
internal sealed class JpxRegionRows : JpxRowSource
{
    /// <summary>The coefficients, which the consumer of the rows may change.</summary>
    private readonly int[] _buffer;

    /// <summary>The distance between rows of the buffer.</summary>
    private readonly int _stride;

    /// <summary>The first column of the rectangle.</summary>
    private readonly int _left;

    /// <summary>The first row of the rectangle.</summary>
    private readonly int _top;

    /// <summary>The next row to give.</summary>
    private int _next;

    /// <summary>Initializes a new instance of the <see cref="JpxRegionRows"/> class.</summary>
    /// <param name="buffer">The coefficients.</param>
    /// <param name="stride">The distance between rows of the buffer.</param>
    /// <param name="left">The first column of the rectangle.</param>
    /// <param name="top">The first row of the rectangle.</param>
    /// <param name="width">The rectangle's width.</param>
    /// <param name="height">The rectangle's height.</param>
    internal JpxRegionRows(int[] buffer, int stride, int left, int top, int width, int height)
    {
        _buffer = buffer;
        _stride = stride;
        _left = left;
        _top = top;
        Width = width;
        Height = height;
    }

    /// <inheritdoc/>
    internal override int Width { get; }

    /// <inheritdoc/>
    internal override int Height { get; }

    /// <inheritdoc/>
    internal override bool IsReady => true;

    /// <inheritdoc/>
    internal override void Prepare()
    {
    }

    /// <inheritdoc/>
    internal override Span<int> NextRow()
    {
        var row = _buffer.AsSpan(((_top + _next) * _stride) + _left, Width);
        _next++;
        return row;
    }

    /// <inheritdoc/>
    internal override void Release()
    {
    }
}
