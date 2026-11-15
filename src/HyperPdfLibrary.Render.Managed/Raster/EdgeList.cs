// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// The straight edges of a shape in device pixels, four floats an edge: where it starts and where it ends. Curves are flattened into
/// edges before they get here. The list is reused from shape to shape, so drawing a page does not allocate for each path.
/// </summary>
[DebuggerDisplay("EdgeList: {Count} edges")]
internal sealed class EdgeList : IDisposable
{
    /// <summary>The floats of one edge.</summary>
    internal const int Stride = 4;

    /// <summary>The smallest rise of an edge that is kept. A flatter edge covers less than any pixel shows.</summary>
    private const float MinRise = 1E-6F;

    /// <summary>The edges the list holds before it first grows.</summary>
    private const int InitialEdges = 256;

    /// <summary>The edge coordinates.</summary>
    private float[] _values = ArrayPool<float>.Shared.Rent(InitialEdges * Stride);

    /// <summary>Gets a value indicating whether level edges are kept. They cover nothing, but a list used to walk a line needs them.</summary>
    internal bool KeepsLevelEdges { get; init; }

    /// <summary>Gets the number of edges.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the smallest x of any edge.</summary>
    internal float Left { get; private set; }

    /// <summary>Gets the smallest y of any edge.</summary>
    internal float Top { get; private set; }

    /// <summary>Gets the largest x of any edge.</summary>
    internal float Right { get; private set; }

    /// <summary>Gets the largest y of any edge.</summary>
    internal float Bottom { get; private set; }

    /// <summary>Gets the edge coordinates, <see cref="Stride"/> floats an edge.</summary>
    internal ReadOnlySpan<float> Values => _values.AsSpan(0, Count * Stride);

    /// <inheritdoc/>
    public void Dispose()
    {
        ArrayPool<float>.Shared.Return(_values);
        _values = [];
        Count = 0;
    }

    /// <summary>Empties the list.</summary>
    internal void Clear()
    {
        Count = 0;
        Left = float.MaxValue;
        Top = float.MaxValue;
        Right = float.MinValue;
        Bottom = float.MinValue;
    }

    /// <summary>Adds an edge. An edge that does not move down or up covers nothing and is left out.</summary>
    /// <param name="x0">The x of the start.</param>
    /// <param name="y0">The y of the start.</param>
    /// <param name="x1">The x of the end.</param>
    /// <param name="y1">The y of the end.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(float x0, float y0, float x1, float y1)
    {
        Left = MathF.Min(Left, MathF.Min(x0, x1));
        Right = MathF.Max(Right, MathF.Max(x0, x1));
        if (!KeepsLevelEdges && MathF.Abs(y0 - y1) < MinRise)
        {
            return;
        }

        Top = MathF.Min(Top, MathF.Min(y0, y1));
        Bottom = MathF.Max(Bottom, MathF.Max(y0, y1));
        var at = Count * Stride;
        if (at + Stride > _values.Length)
        {
            Grow();
        }

        ref var first = ref _values[at];
        first = x0;
        Unsafe.Add(ref first, 1) = y0;
        Unsafe.Add(ref first, 1 + 1) = x1;
        Unsafe.Add(ref first, Stride - 1) = y1;
        Count++;
    }

    /// <summary>Makes room for more edges.</summary>
    private void Grow()
    {
        var larger = ArrayPool<float>.Shared.Rent(_values.Length + _values.Length);
        _values.AsSpan(0, Count * Stride).CopyTo(larger);
        ArrayPool<float>.Shared.Return(_values);
        _values = larger;
    }
}
