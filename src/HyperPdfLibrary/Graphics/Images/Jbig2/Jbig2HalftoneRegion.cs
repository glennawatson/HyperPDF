// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The halftone region decoding procedure (T.88 section 6.6): Gray-coded bit planes of pattern indices, decoded with
/// the generic region procedure, select a pattern for each cell of a grid placed on the region.
/// </summary>
internal static class Jbig2HalftoneRegion
{
    /// <summary>The fraction bits of grid coordinates.</summary>
    private const int GridShift = 8;

    /// <summary>The bits of the MMR end-of-block code.</summary>
    private const int EndOfBlockBits = 24;

    /// <summary>The MMR end-of-block code: two end-of-line codes.</summary>
    private const uint EndOfBlock = 0x001001;

    /// <summary>The most bit planes, enough for 65536 patterns.</summary>
    private const int MaxPlanes = 16;

    /// <summary>Decodes an arithmetic-coded halftone region.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The cleared generic contexts for the template, shared by every plane.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="patterns">The pattern dictionary.</param>
    /// <param name="region">The bitmap receiving the region.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns><see langword="false"/> when a plane cannot be made or the budget is spent.</returns>
    internal static bool DecodeArithmetic(
        ref Jbig2ArithmeticDecoder decoder,
        Span<byte> contexts,
        Jbig2HalftoneSettings settings,
        Jbig2PatternDictionary patterns,
        Jbig2Bitmap region,
        Jbig2Workspace workspace)
    {
        var count = PlaneCount(patterns.Patterns.Count);
        var planes = RentPlanes(count, workspace.ScratchPools);
        using var skip = settings.EnableSkip ? CreateSkip(settings, patterns, workspace.ScratchPools) : null;
        try
        {
            var parameters = new Jbig2GenericParameters(settings.Template, false, Jbig2AtPixels.ForTemplate(settings.Template));
            for (var i = count - 1; i >= 0; i--)
            {
                planes[i] = CreatePlane(settings, workspace);
                if (planes[i] is not { } plane)
                {
                    return false;
                }

                _ = Jbig2GenericRegion.Decode(ref decoder, contexts, parameters, plane, skip);
                GrayDecode(planes, i, count);
            }

            return Render(settings, patterns, planes.AsSpan(0, count), region, workspace);
        }
        finally
        {
            Release(planes, count, workspace.ScratchPools);
        }
    }

    /// <summary>Decodes an MMR-coded halftone region.</summary>
    /// <param name="reader">The reader, at the first plane.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="patterns">The pattern dictionary.</param>
    /// <param name="region">The bitmap receiving the region.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns><see langword="false"/> when a plane cannot be made or the budget is spent.</returns>
    internal static bool DecodeMmr(ref Jbig2Reader reader, Jbig2HalftoneSettings settings, Jbig2PatternDictionary patterns, Jbig2Bitmap region, Jbig2Workspace workspace)
    {
        var count = PlaneCount(patterns.Patterns.Count);
        var planes = RentPlanes(count, workspace.ScratchPools);
        try
        {
            for (var i = count - 1; i >= 0; i--)
            {
                planes[i] = CreatePlane(settings, workspace);
                if (planes[i] is not { } plane)
                {
                    return false;
                }

                var start = reader.Offset;
                reader.BitPosition = ((long)start << Jbig2Bits.ByteShift) + Jbig2GenericRegion.DecodeMmr(reader.Data[start..], plane);
                SkipEndOfBlock(ref reader);
                GrayDecode(planes, i, count);
            }

            return Render(settings, patterns, planes.AsSpan(0, count), region, workspace);
        }
        finally
        {
            Release(planes, count, workspace.ScratchPools);
        }
    }

    /// <summary>Gets the number of bit planes needed to index the patterns.</summary>
    /// <param name="patterns">The number of patterns.</param>
    /// <returns>The planes, at least one.</returns>
    private static int PlaneCount(int patterns)
    {
        var bits = 1;
        while (bits < MaxPlanes && (1 << bits) < patterns)
        {
            bits++;
        }

        return bits;
    }

    /// <summary>Creates a white plane of the grid's size.</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns>The plane, or <see langword="null"/>.</returns>
    private static Jbig2Bitmap? CreatePlane(Jbig2HalftoneSettings settings, Jbig2Workspace workspace) =>
        workspace.TryCharge((long)settings.GridWidth * settings.GridHeight)
            ? Jbig2Bitmap.Create(settings.GridWidth, settings.GridHeight, workspace.ScratchPools)
            : null;

    /// <summary>Turns a decoded plane from Gray code into binary by XORing the plane above it.</summary>
    /// <param name="planes">The planes.</param>
    /// <param name="index">The plane just decoded.</param>
    /// <param name="count">The number of planes.</param>
    private static void GrayDecode(Jbig2Bitmap?[] planes, int index, int count)
    {
        if (index < count - 1 && planes[index] is { } plane && planes[index + 1] is { } above)
        {
            _ = Jbig2Composer.Compose(plane, above.View, 0, 0, Jbig2ComposeOperator.Xor);
        }
    }

    /// <summary>Moves past an MMR end-of-block code when one follows the plane, then to the next byte.</summary>
    /// <param name="reader">The reader.</param>
    private static void SkipEndOfBlock(ref Jbig2Reader reader)
    {
        var start = reader.BitPosition;
        if (!reader.TryReadBits(EndOfBlockBits, out var code) || code != EndOfBlock)
        {
            reader.BitPosition = start;
        }

        reader.AlignByte();
    }

    /// <summary>Builds the mask of grid cells whose pattern would fall wholly outside the region.</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="patterns">The pattern dictionary.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The mask, or <see langword="null"/> when it cannot be made.</returns>
    private static Jbig2Bitmap? CreateSkip(Jbig2HalftoneSettings settings, Jbig2PatternDictionary patterns, ScratchPools scratchPools)
    {
        var skip = Jbig2Bitmap.Create(settings.GridWidth, settings.GridHeight, scratchPools);
        if (skip is null)
        {
            return null;
        }

        for (var m = 0; m < settings.GridHeight; m++)
        {
            var row = skip.Row(m);
            for (var n = 0; n < settings.GridWidth; n++)
            {
                var x = CellX(settings, m, n);
                var y = CellY(settings, m, n);
                if (x + patterns.Width <= 0 || x >= settings.Width || y + patterns.Height <= 0 || y >= settings.Height)
                {
                    Jbig2Bits.SetBlack(row, n);
                }
            }
        }

        return skip;
    }

    /// <summary>Places the pattern of every grid cell in the region.</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="patterns">The pattern dictionary.</param>
    /// <param name="planes">The binary planes, least significant first.</param>
    /// <param name="region">The bitmap receiving the region.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns><see langword="false"/> when the budget is spent.</returns>
    private static bool Render(Jbig2HalftoneSettings settings, Jbig2PatternDictionary patterns, ReadOnlySpan<Jbig2Bitmap?> planes, Jbig2Bitmap region, Jbig2Workspace workspace)
    {
        region.Fill(settings.DefaultPixel);
        var last = patterns.Patterns.Count - 1;
        for (var m = 0; m < settings.GridHeight; m++)
        {
            for (var n = 0; n < settings.GridWidth; n++)
            {
                var index = Math.Min(GrayValue(planes, n, m), last);
                var pattern = patterns.Patterns.Get(index);
                if (!workspace.TryCharge(Jbig2Composer.Compose(region, pattern, CellX(settings, m, n), CellY(settings, m, n), settings.Operator)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Gets the pattern index of a grid cell from the planes.</summary>
    /// <param name="planes">The binary planes, least significant first.</param>
    /// <param name="n">The grid column.</param>
    /// <param name="m">The grid row.</param>
    /// <returns>The index.</returns>
    private static int GrayValue(ReadOnlySpan<Jbig2Bitmap?> planes, int n, int m)
    {
        var value = 0;
        for (var i = 0; i < planes.Length; i++)
        {
            value |= (planes[i]?.GetPixel(n, m) ?? 0) << i;
        }

        return value;
    }

    /// <summary>Gets the region column of a grid cell's top-left corner, rounding down as T.88 does.</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="m">The grid row.</param>
    /// <param name="n">The grid column.</param>
    /// <returns>The column.</returns>
    private static long CellX(Jbig2HalftoneSettings settings, int m, int n) =>
        (settings.GridX + ((long)m * settings.VectorY) + ((long)n * settings.VectorX)) >> GridShift;

    /// <summary>Gets the region row of a grid cell's top-left corner, rounding down as T.88 does.</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="m">The grid row.</param>
    /// <param name="n">The grid column.</param>
    /// <returns>The row.</returns>
    private static long CellY(Jbig2HalftoneSettings settings, int m, int n) =>
        (settings.GridY + ((long)m * settings.VectorX) - ((long)n * settings.VectorY)) >> GridShift;

    /// <summary>Rents a cleared array for the planes.</summary>
    /// <param name="count">The number of planes.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The array.</returns>
    private static Jbig2Bitmap?[] RentPlanes(int count, ScratchPools scratchPools)
    {
        var planes = scratchPools.Get<Jbig2Bitmap?>().Rent(count);
        planes.AsSpan(0, count).Clear();
        return planes;
    }

    /// <summary>Disposes the planes and returns their array to the pool.</summary>
    /// <param name="planes">The planes.</param>
    /// <param name="count">The number of planes.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    private static void Release(Jbig2Bitmap?[] planes, int count, ScratchPools scratchPools)
    {
        for (var i = 0; i < count; i++)
        {
            planes[i]?.Dispose();
        }

        scratchPools.Get<Jbig2Bitmap?>().Return(planes, true);
    }
}
