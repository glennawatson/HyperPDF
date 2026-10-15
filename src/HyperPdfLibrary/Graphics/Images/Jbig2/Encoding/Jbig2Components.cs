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
/// the shared pool; copies of the value share it and only the owner disposes. The default value holds no components.
/// </summary>
[DebuggerDisplay("Jbig2Components: {Count} components, {SymbolCount} symbols")]
internal readonly record struct Jbig2Components : IDisposable
{
    /// <summary>The most bytes of symbol bitmaps kept.</summary>
    private const long MaximumBitmapBytes = 1L << 28;

    /// <summary>The value of a byte with every bit set.</summary>
    private const uint AllBits = 0xFF;

    /// <summary>The bounding boxes and black pixel counts.</summary>
    private readonly Jbig2ComponentMeasures _measures;

    /// <summary>The offset of each component's bitmap in the bitmap store, or -1 for a component too large to be a symbol.</summary>
    private readonly int[]? _offset;

    /// <summary>The store of symbol bitmaps.</summary>
    private readonly byte[]? _bits;

    /// <summary>The bitmap of the components too large to be symbols.</summary>
    private readonly byte[]? _residual;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Components"/> struct.</summary>
    /// <param name="measures">The bounding boxes and black pixel counts.</param>
    /// <param name="offset">The offset of each component's bitmap.</param>
    /// <param name="bits">The store of symbol bitmaps.</param>
    /// <param name="residual">The residual bitmap, or <see langword="null"/>.</param>
    /// <param name="symbolCount">The number of symbols.</param>
    /// <param name="residualBox">The place and size of the residual bitmap.</param>
    private Jbig2Components(in Jbig2ComponentMeasures measures, int[] offset, byte[] bits, byte[]? residual, int symbolCount, Jbig2RegionBox residualBox)
    {
        _measures = measures;
        _offset = offset;
        _bits = bits;
        _residual = residual;
        SymbolCount = symbolCount;
        ResidualBox = residualBox;
    }

    /// <summary>Gets a value indicating whether this is the default value, which holds no components.</summary>
    internal bool IsDefault => _offset is null;

    /// <summary>Gets the number of components.</summary>
    internal int Count => _measures.Count;

    /// <summary>Gets the number of components that are symbols.</summary>
    internal int SymbolCount { get; }

    /// <summary>Gets the place and size of the residual bitmap on the page; empty when no component is too large.</summary>
    internal Jbig2RegionBox ResidualBox { get; }

    /// <summary>Gets the residual bitmap rows, or an empty span.</summary>
    internal ReadOnlySpan<byte> ResidualRows => _residual is null ? default : _residual.AsSpan(0, Jbig2Bits.Stride(ResidualBox.Width) * ResidualBox.Height);

    /// <summary>Returns the rented arrays.</summary>
    public void Dispose()
    {
        if (_offset is null)
        {
            return;
        }

        _measures.Dispose();
        ArrayPool<int>.Shared.Return(_offset);
        ReturnBytes(_bits);
        ReturnBytes(_residual);
    }

    /// <summary>Finds the components of a page.</summary>
    /// <param name="ink">The packed rows, 1 for black, with zero padding bits.</param>
    /// <param name="size">The page size.</param>
    /// <param name="maximumSymbolSize">The largest width or height of a symbol.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <param name="components">Receives the components, which the caller disposes.</param>
    /// <returns><see langword="false"/> when the page has too many runs to search.</returns>
    internal static bool TryFind(ReadOnlySpan<byte> ink, Jbig2Size size, int maximumSymbolSize, CancellationToken cancellationToken, out Jbig2Components components)
    {
        components = default;
        var runs = new Jbig2Runs(size.Height);
        try
        {
            if (!runs.TryFill(ink, size, cancellationToken))
            {
                return false;
            }

            components = Build(ref runs, size.Height, maximumSymbolSize, cancellationToken);
            return true;
        }
        finally
        {
            runs.Dispose();
        }
    }

    /// <summary>
    /// Joins the symbol components of several pages into one set, in page order, so one dictionary can serve them all. The
    /// result has no residual bitmap; each page keeps its own.
    /// </summary>
    /// <param name="parts">The pages' components; a page with none is the default value and adds nothing.</param>
    /// <param name="combined">Receives the joined components, which the caller disposes.</param>
    /// <returns><see langword="false"/> when no page has components or their bitmaps are too large to keep together.</returns>
    internal static bool TryCombine(ReadOnlySpan<Jbig2Components> parts, out Jbig2Components combined)
    {
        combined = default;
        var count = 0;
        long total = 0;
        foreach (var part in parts)
        {
            count += part.Count;
            total += part.SymbolBytes();
        }

        if (count == 0 || total > MaximumBitmapBytes)
        {
            return false;
        }

        var measures = new Jbig2ComponentMeasures(count);
        var offsets = ArrayPool<int>.Shared.Rent(count);
        var bits = ArrayPool<byte>.Shared.Rent((int)Math.Max(total, 1));
        var symbols = CopyParts(parts, measures, offsets, bits);
        combined = new(measures, offsets, bits, null, symbols, default);
        return true;
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
    internal bool IsSymbol(int index) => _offset![index] >= 0;

    /// <summary>Gets the packed rows of a symbol component.</summary>
    /// <param name="index">The component, which must be a symbol.</param>
    /// <returns>The rows, 1 for black, with zero padding bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Rows(int index) => _bits.AsSpan(_offset![index], Jbig2Bits.Stride(Width(index)) * Height(index));

    /// <summary>Builds the components from the runs.</summary>
    /// <param name="runs">The runs, joined.</param>
    /// <param name="height">The page height.</param>
    /// <param name="maximumSymbolSize">The largest width or height of a symbol.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The components.</returns>
    private static Jbig2Components Build(ref Jbig2Runs runs, int height, int maximumSymbolSize, CancellationToken cancellationToken)
    {
        var measures = new Jbig2ComponentMeasures(runs.CountRoots());
        var offsets = ArrayPool<int>.Shared.Rent(measures.Count);
        byte[]? bits = null;
        byte[]? residual = null;
        try
        {
            runs.Measure(measures);
            var layout = Classify(measures, offsets, maximumSymbolSize);
            bits = ArrayPool<byte>.Shared.Rent((int)Math.Max(layout.Total, 1));
            bits.AsSpan(0, (int)layout.Total).Clear();
            if (layout.HasResidual)
            {
                var length = Jbig2Bits.Stride(layout.Residual.Width) * layout.Residual.Height;
                residual = ArrayPool<byte>.Shared.Rent(length);
                residual.AsSpan(0, length).Clear();
            }

            var components = new Jbig2Components(measures, offsets, bits, residual, layout.SymbolCount, layout.Residual);
            components.Paint(ref runs, height, cancellationToken);
            return components;
        }
        catch
        {
            measures.Dispose();
            ArrayPool<int>.Shared.Return(offsets);
            ReturnBytes(bits);
            ReturnBytes(residual);
            throw;
        }
    }

    /// <summary>Returns a rented byte array.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    private static void ReturnBytes(byte[]? array)
    {
        if (array is not null)
        {
            ArrayPool<byte>.Shared.Return(array);
        }
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

    /// <summary>Copies the components of several pages into joined storage.</summary>
    /// <param name="parts">The pages' components.</param>
    /// <param name="measures">Receives the measures.</param>
    /// <param name="offsets">Receives each component's bitmap offset.</param>
    /// <param name="bits">Receives the symbol bitmaps.</param>
    /// <returns>The number of symbols.</returns>
    private static int CopyParts(ReadOnlySpan<Jbig2Components> parts, in Jbig2ComponentMeasures measures, int[] offsets, byte[] bits)
    {
        var next = 0;
        var offset = 0;
        var symbols = 0;
        foreach (var part in parts)
        {
            for (var i = 0; i < part.Count; i++)
            {
                measures.Copy(next, part._measures, i);
                if (part.IsSymbol(i))
                {
                    var rows = part.Rows(i);
                    rows.CopyTo(bits.AsSpan(offset));
                    offsets[next] = offset;
                    offset += rows.Length;
                    symbols++;
                }
                else
                {
                    offsets[next] = -1;
                }

                next++;
            }
        }

        return symbols;
    }

    /// <summary>Decides which components are symbols, lays out their bitmaps and sizes the residual bitmap.</summary>
    /// <param name="measures">The measured components.</param>
    /// <param name="offsets">Receives each component's bitmap offset, or -1 for a component too large to be a symbol.</param>
    /// <param name="maximumSymbolSize">The largest width or height of a symbol.</param>
    /// <returns>The layout.</returns>
    private static Layout Classify(in Jbig2ComponentMeasures measures, int[] offsets, int maximumSymbolSize)
    {
        long total = 0;
        var symbols = 0;
        var residual = default(Jbig2RegionBox);
        var hasResidual = false;
        for (var i = 0; i < measures.Count; i++)
        {
            var bytes = (long)Jbig2Bits.Stride(measures.Width(i)) * measures.Height(i);
            if (measures.Width(i) <= maximumSymbolSize && measures.Height(i) <= maximumSymbolSize && total + bytes <= MaximumBitmapBytes)
            {
                offsets[i] = (int)total;
                total += bytes;
                symbols++;
                continue;
            }

            offsets[i] = -1;
            residual = hasResidual ? Union(residual, measures, i) : new(measures.Left(i), measures.Top(i), measures.Width(i), measures.Height(i));
            hasResidual = true;
        }

        return new(total, symbols, hasResidual, residual);
    }

    /// <summary>Grows a box to include a component.</summary>
    /// <param name="box">The box.</param>
    /// <param name="measures">The measured components.</param>
    /// <param name="index">The component.</param>
    /// <returns>The union.</returns>
    private static Jbig2RegionBox Union(Jbig2RegionBox box, in Jbig2ComponentMeasures measures, int index)
    {
        var left = Math.Min(box.X, measures.Left(index));
        var top = Math.Min(box.Y, measures.Top(index));
        var right = Math.Max(box.X + box.Width, measures.Left(index) + measures.Width(index));
        var bottom = Math.Max(box.Y + box.Height, measures.Top(index) + measures.Height(index));
        return new(left, top, right - left, bottom - top);
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

    /// <summary>Paints every run into its component's bitmap, or into the residual bitmap.</summary>
    /// <param name="runs">The runs.</param>
    /// <param name="height">The page height.</param>
    /// <param name="cancellationToken">Cancels the paint.</param>
    private void Paint(ref Jbig2Runs runs, int height, CancellationToken cancellationToken)
    {
        var residualStride = Jbig2Bits.Stride(ResidualBox.Width);
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var run = runs.RowStart(y); run < runs.RowStart(y + 1); run++)
            {
                var component = runs.ComponentOf(run);
                if (_offset![component] >= 0)
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

    /// <summary>How the components divide into symbols and a residual bitmap.</summary>
    /// <param name="Total">The bytes of the symbol bitmaps.</param>
    /// <param name="SymbolCount">The number of symbols.</param>
    /// <param name="HasResidual">Whether any component is too large to be a symbol.</param>
    /// <param name="Residual">The place and size of the residual bitmap.</param>
    private readonly record struct Layout(long Total, int SymbolCount, bool HasResidual, Jbig2RegionBox Residual);
}
