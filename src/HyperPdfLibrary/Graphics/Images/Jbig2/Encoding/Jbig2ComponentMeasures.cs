// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The bounding box and black pixel count of each component of a page, in struct-of-arrays form. The value holds the
/// rented arrays, so copies share them and only the owner disposes.
/// </summary>
[DebuggerDisplay("Jbig2ComponentMeasures: {Count} components")]
internal readonly record struct Jbig2ComponentMeasures : IDisposable
{
    /// <summary>The left edge of each component.</summary>
    private readonly int[] _left;

    /// <summary>The top edge of each component.</summary>
    private readonly int[] _top;

    /// <summary>The right edge while measuring, then the width of each component.</summary>
    private readonly int[] _width;

    /// <summary>The bottom edge while measuring, then the height of each component.</summary>
    private readonly int[] _height;

    /// <summary>The black pixels of each component.</summary>
    private readonly int[] _ink;

    /// <summary>Initializes a new instance of the <see cref="Jbig2ComponentMeasures"/> struct.</summary>
    /// <param name="count">The number of components.</param>
    internal Jbig2ComponentMeasures(int count)
    {
        Count = count;
        _left = ArrayPool<int>.Shared.Rent(count);
        _top = ArrayPool<int>.Shared.Rent(count);
        _width = ArrayPool<int>.Shared.Rent(count);
        _height = ArrayPool<int>.Shared.Rent(count);
        _ink = ArrayPool<int>.Shared.Rent(count);
    }

    /// <summary>Gets the number of components.</summary>
    internal int Count { get; }

    /// <summary>Returns the rented arrays.</summary>
    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_left);
        ArrayPool<int>.Shared.Return(_top);
        ArrayPool<int>.Shared.Return(_width);
        ArrayPool<int>.Shared.Return(_height);
        ArrayPool<int>.Shared.Return(_ink);
    }

    /// <summary>Gets the left edge of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The column.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Left(int index) => _left[index];

    /// <summary>Gets the top edge of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Top(int index) => _top[index];

    /// <summary>Gets the width of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The width in pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Width(int index) => _width[index];

    /// <summary>Gets the height of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The height in pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Height(int index) => _height[index];

    /// <summary>Gets the black pixels of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Ink(int index) => _ink[index];

    /// <summary>Copies a finished component from another set of measures.</summary>
    /// <param name="index">The component.</param>
    /// <param name="source">The measures to copy from.</param>
    /// <param name="sourceIndex">The component in <paramref name="source"/>.</param>
    internal void Copy(int index, in Jbig2ComponentMeasures source, int sourceIndex)
    {
        _left[index] = source._left[sourceIndex];
        _top[index] = source._top[sourceIndex];
        _width[index] = source._width[sourceIndex];
        _height[index] = source._height[sourceIndex];
        _ink[index] = source._ink[sourceIndex];
    }

    /// <summary>Starts a component with its first run.</summary>
    /// <param name="index">The component.</param>
    /// <param name="left">The run's first column.</param>
    /// <param name="top">The run's row.</param>
    /// <param name="right">The column after the run's last.</param>
    internal void Start(int index, int left, int top, int right)
    {
        _left[index] = left;
        _top[index] = top;
        _width[index] = right;
        _height[index] = top + 1;
        _ink[index] = 0;
    }

    /// <summary>Grows a component to include another run.</summary>
    /// <param name="index">The component.</param>
    /// <param name="left">The run's first column.</param>
    /// <param name="right">The column after the run's last.</param>
    /// <param name="row">The run's row, which is not above any earlier run of the component.</param>
    internal void Extend(int index, int left, int right, int row)
    {
        _left[index] = Math.Min(_left[index], left);
        _width[index] = Math.Max(_width[index], right);
        _height[index] = row + 1;
    }

    /// <summary>Counts black pixels for a component.</summary>
    /// <param name="index">The component.</param>
    /// <param name="pixels">The pixels to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddInk(int index, int pixels) => _ink[index] += pixels;

    /// <summary>Turns the edges gathered while measuring into widths and heights.</summary>
    /// <param name="count">The number of components measured.</param>
    internal void Finish(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _width[i] -= _left[i];
            _height[i] -= _top[i];
        }
    }
}
