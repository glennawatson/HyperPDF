// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The 8-connected black components of a page, found from runs of black pixels. Components small enough to be symbols
/// keep their bitmaps; the black pixels of the others are gathered into one residual bitmap. All storage is rented from
/// the shared pool.
/// </summary>
[DebuggerDisplay("Jbig2Components: {Count} components, {SymbolCount} symbols")]
internal sealed class Jbig2Components : IDisposable
{
    /// <summary>The most bytes of symbol bitmaps kept.</summary>
    private const long MaximumBitmapBytes = 1L << 28;

    /// <summary>The value of a byte with every bit set.</summary>
    private const uint AllBits = 0xFF;

    /// <summary>The bounding boxes and black pixel counts.</summary>
    private readonly Jbig2ComponentMeasures _measures;

    /// <summary>The offset of each component's bitmap in the bitmap store, or -1 for a component too large to be a symbol.</summary>
    private readonly int[] _offset;

    /// <summary>The store of symbol bitmaps.</summary>
    private byte[]? _bits;

    /// <summary>The bitmap of the components too large to be symbols.</summary>
    private byte[]? _residual;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Components"/> class.</summary>
    /// <param name="count">The number of components.</param>
    private Jbig2Components(int count)
    {
        _measures = new(count);
        _offset = ArrayPool<int>.Shared.Rent(count);
    }

    /// <summary>Gets the number of components.</summary>
    internal int Count => _measures.Count;

    /// <summary>Gets the number of components that are symbols.</summary>
    internal int SymbolCount { get; private set; }

    /// <summary>Gets the place and size of the residual bitmap on the page; empty when no component is too large.</summary>
    internal Jbig2RegionBox ResidualBox { get; private set; }

    /// <summary>Gets the residual bitmap rows, or an empty span.</summary>
    internal ReadOnlySpan<byte> ResidualRows => _residual is null ? default : _residual.AsSpan(0, Jbig2Bits.Stride(ResidualBox.Width) * ResidualBox.Height);

    /// <summary>Returns the rented arrays.</summary>
    public void Dispose()
    {
        _measures.Dispose();
        ArrayPool<int>.Shared.Return(_offset);
        ReturnBytes(ref _bits);
        ReturnBytes(ref _residual);
    }

    /// <summary>Finds the components of a page.</summary>
    /// <param name="ink">The packed rows, 1 for black, with zero padding bits.</param>
    /// <param name="size">The page size.</param>
    /// <param name="maximumSymbolSize">The largest width or height of a symbol.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The components, or <see langword="null"/> when the page has too many runs to search.</returns>
    internal static Jbig2Components? Find(ReadOnlySpan<byte> ink, Jbig2Size size, int maximumSymbolSize, CancellationToken cancellationToken)
    {
        using var runs = new Jbig2Runs(size.Height);
        return runs.TryFill(ink, size, cancellationToken) ? Build(runs, size.Height, maximumSymbolSize, cancellationToken) : null;
    }

    /// <summary>
    /// Joins the symbol components of several pages into one set, in page order, so one dictionary can serve them all. The
    /// result has no residual bitmap; each page keeps its own.
    /// </summary>
    /// <param name="parts">The pages' components.</param>
    /// <returns>The joined components, or <see langword="null"/> when their bitmaps are too large to keep together.</returns>
    internal static Jbig2Components? Combine(ReadOnlySpan<Jbig2Components> parts)
    {
        var count = 0;
        long total = 0;
        foreach (var part in parts)
        {
            count += part.Count;
            total += part.SymbolBytes();
        }

        if (total > MaximumBitmapBytes)
        {
            return null;
        }

        var combined = new Jbig2Components(count) { _bits = ArrayPool<byte>.Shared.Rent((int)Math.Max(total, 1)) };
        var next = 0;
        var offset = 0;
        foreach (var part in parts)
        {
            for (var i = 0; i < part.Count; i++)
            {
                combined._measures.Copy(next, part._measures, i);
                if (part.IsSymbol(i))
                {
                    var rows = part.Rows(i);
                    rows.CopyTo(combined._bits.AsSpan(offset));
                    combined._offset[next] = offset;
                    offset += rows.Length;
                    combined.SymbolCount++;
                }
                else
                {
                    combined._offset[next] = -1;
                }

                next++;
            }
        }

        return combined;
    }

    /// <summary>Gets the left edge of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The column.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Left(int index) => _measures.Left(index);

    /// <summary>Gets the top edge of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Top(int index) => _measures.Top(index);

    /// <summary>Gets the width of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The width in pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Width(int index) => _measures.Width(index);

    /// <summary>Gets the height of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The height in pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Height(int index) => _measures.Height(index);

    /// <summary>Gets the black pixels of a component.</summary>
    /// <param name="index">The component.</param>
    /// <returns>The count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Ink(int index) => _measures.Ink(index);

    /// <summary>Determines whether a component is small enough to be a symbol.</summary>
    /// <param name="index">The component.</param>
    /// <returns><see langword="true"/> for a symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSymbol(int index) => _offset[index] >= 0;

    /// <summary>Gets the packed rows of a symbol component.</summary>
    /// <param name="index">The component, which must be a symbol.</param>
    /// <returns>The rows, 1 for black, with zero padding bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Rows(int index) => _bits.AsSpan(_offset[index], Jbig2Bits.Stride(Width(index)) * Height(index));

    /// <summary>Builds the components from the runs.</summary>
    /// <param name="runs">The runs, joined.</param>
    /// <param name="height">The page height.</param>
    /// <param name="maximumSymbolSize">The largest width or height of a symbol.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The components.</returns>
    private static Jbig2Components Build(Jbig2Runs runs, int height, int maximumSymbolSize, CancellationToken cancellationToken)
    {
        var components = new Jbig2Components(runs.CountRoots());
        try
        {
            runs.Measure(components._measures);
            components.Classify(maximumSymbolSize);
            components.Paint(runs, height, cancellationToken);
            return components;
        }
        catch
        {
            components.Dispose();
            throw;
        }
    }

    /// <summary>Returns a rented byte array.</summary>
    /// <param name="array">The array, set to null.</param>
    private static void ReturnBytes(ref byte[]? array)
    {
        if (array is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(array);
        array = null;
    }

    /// <summary>Sets a range of pixels black in a row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="from">The first column.</param>
    /// <param name="to">The column after the last.</param>
    private static void SetRange(Span<byte> row, int from, int to)
    {
        var firstByte = from >> Jbig2Bits.ByteShift;
        var lastByte = (to - 1) >> Jbig2Bits.ByteShift;
        var first = (byte)(AllBits >> (from & Jbig2Bits.BitMask));
        var last = (byte)(AllBits << (Jbig2Bits.BitMask - ((to - 1) & Jbig2Bits.BitMask)));
        if (firstByte == lastByte)
        {
            row[firstByte] |= (byte)(first & last);
            return;
        }

        row[firstByte] |= first;
        row[(firstByte + 1)..lastByte].Fill(byte.MaxValue);
        row[lastByte] |= last;
    }

    /// <summary>Gets the bytes the symbol bitmaps take.</summary>
    /// <returns>The byte count.</returns>
    private long SymbolBytes()
    {
        long bytes = 0;
        for (var i = 0; i < Count; i++)
        {
            bytes += IsSymbol(i) ? (long)Jbig2Bits.Stride(Width(i)) * Height(i) : 0;
        }

        return bytes;
    }

    /// <summary>Decides which components are symbols, lays out their bitmaps and sizes the residual bitmap.</summary>
    /// <param name="maximumSymbolSize">The largest width or height of a symbol.</param>
    private void Classify(int maximumSymbolSize)
    {
        long total = 0;
        var residual = default(Jbig2RegionBox);
        var hasResidual = false;
        for (var i = 0; i < Count; i++)
        {
            var bytes = (long)Jbig2Bits.Stride(Width(i)) * Height(i);
            if (Width(i) <= maximumSymbolSize && Height(i) <= maximumSymbolSize && total + bytes <= MaximumBitmapBytes)
            {
                _offset[i] = (int)total;
                total += bytes;
                SymbolCount++;
                continue;
            }

            _offset[i] = -1;
            residual = hasResidual ? Union(residual, i) : new(Left(i), Top(i), Width(i), Height(i));
            hasResidual = true;
        }

        _bits = ArrayPool<byte>.Shared.Rent((int)Math.Max(total, 1));
        _bits.AsSpan(0, (int)total).Clear();
        if (!hasResidual)
        {
            return;
        }

        ResidualBox = residual;
        var length = Jbig2Bits.Stride(residual.Width) * residual.Height;
        _residual = ArrayPool<byte>.Shared.Rent(length);
        _residual.AsSpan(0, length).Clear();
    }

    /// <summary>Grows a box to include a component.</summary>
    /// <param name="box">The box.</param>
    /// <param name="index">The component.</param>
    /// <returns>The union.</returns>
    private Jbig2RegionBox Union(Jbig2RegionBox box, int index)
    {
        var left = Math.Min(box.X, Left(index));
        var top = Math.Min(box.Y, Top(index));
        var right = Math.Max(box.X + box.Width, Left(index) + Width(index));
        var bottom = Math.Max(box.Y + box.Height, Top(index) + Height(index));
        return new(left, top, right - left, bottom - top);
    }

    /// <summary>Paints every run into its component's bitmap, or into the residual bitmap.</summary>
    /// <param name="runs">The runs.</param>
    /// <param name="height">The page height.</param>
    /// <param name="cancellationToken">Cancels the paint.</param>
    private void Paint(Jbig2Runs runs, int height, CancellationToken cancellationToken)
    {
        var residualStride = Jbig2Bits.Stride(ResidualBox.Width);
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var run = runs.RowStart(y); run < runs.RowStart(y + 1); run++)
            {
                var component = runs.ComponentOf(run);
                if (_offset[component] >= 0)
                {
                    var stride = Jbig2Bits.Stride(Width(component));
                    var row = _bits.AsSpan(_offset[component] + ((y - Top(component)) * stride), stride);
                    SetRange(row, runs.Start(run) - Left(component), runs.End(run) - Left(component));
                    continue;
                }

                var residualRow = _residual.AsSpan((y - ResidualBox.Y) * residualStride, residualStride);
                SetRange(residualRow, runs.Start(run) - ResidualBox.X, runs.End(run) - ResidualBox.X);
            }
        }
    }
}
