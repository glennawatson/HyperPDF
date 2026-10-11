// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The generic region decoding procedure (T.88 section 6.2): arithmetic templates 0 to 3 with typical prediction,
/// adaptive template pixels and a skip mask, and MMR. Rows are decoded a byte at a time from rolling windows of the two
/// rows above; adaptive pixels away from their nominal places are read one by one.
/// </summary>
internal static partial class Jbig2GenericRegion
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift that takes the 8-pixel window ending three pixels right of the first pixel in a byte.</summary>
    private const int WindowShift = 12;

    /// <summary>The mask of an 8-pixel window.</summary>
    private const uint WindowMask = 0xFF;

    /// <summary>The shift of the byte before the current one in a row window.</summary>
    private const int PreviousByteShift = 16;

    /// <summary>The number of rows above whose pixels form the nominal contexts.</summary>
    private const int RowsAbove = 2;

    /// <summary>The number of generic template 2.</summary>
    private const int TemplateTwo = 2;

    /// <summary>Decodes an arithmetic-coded generic region into a white bitmap.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The <see cref="Jbig2GenericParameters.ContextCount"/> contexts.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="target">The white bitmap receiving the region.</param>
    /// <param name="skip">The pixels to leave white without decoding, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the data ran out; the rows decoded so far are kept.</returns>
    internal static bool Decode(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, in Jbig2GenericParameters parameters, Jbig2Bitmap target, Jbig2Bitmap? skip) =>
        parameters.Template switch
        {
            0 => Decode<Template0>(ref decoder, contexts, parameters, target, skip),
            1 => Decode<Template1>(ref decoder, contexts, parameters, target, skip),
            TemplateTwo => Decode<Template2>(ref decoder, contexts, parameters, target, skip),
            _ => Decode<Template3>(ref decoder, contexts, parameters, target, skip),
        };

    /// <summary>Decodes an MMR-coded generic region into a white bitmap.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="target">The white bitmap receiving the region.</param>
    /// <returns>The bits read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long DecodeMmr(ReadOnlySpan<byte> data, Jbig2Bitmap target) =>
        CcittFaxDecoder.DecodeMmr(data, target.Width, target.Height, target.Data);

    /// <summary>Decodes a region with one template.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="target">The white bitmap receiving the region.</param>
    /// <param name="skip">The skip mask, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the data ran out.</returns>
    private static bool Decode<TTemplate>(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, in Jbig2GenericParameters parameters, Jbig2Bitmap target, Jbig2Bitmap? skip)
        where TTemplate : struct, ITemplate
    {
        var nominal = skip is null && parameters.At.IsNominal(parameters.Template);
        var typical = 0;
        for (var y = 0; y < target.Height; y++)
        {
            decoder.CancellationToken.ThrowIfCancellationRequested();
            if (parameters.TypicalPrediction)
            {
                if (decoder.IsComplete)
                {
                    return false;
                }

                typical ^= decoder.Decode(ref contexts[TTemplate.TypicalContext]);
                if (typical != 0)
                {
                    CopyAbove(target, y);
                    continue;
                }
            }

            var decoded = nominal
                ? DecodeNominalRow<TTemplate>(ref decoder, contexts, target, y)
                : DecodeGeneralRow<TTemplate>(ref decoder, contexts, parameters.At, target, skip, y);
            if (!decoded)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Copies the row above into a row, as typical prediction does; the first row stays white.</summary>
    /// <param name="target">The bitmap.</param>
    /// <param name="y">The row.</param>
    private static void CopyAbove(Jbig2Bitmap target, int y)
    {
        if (y > 0)
        {
            target.CopyRow(y - 1, y);
        }
    }

    /// <summary>Decodes a row whose adaptive pixels are all nominal, a byte at a time.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="target">The bitmap.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="false"/> when the data ran out.</returns>
    private static bool DecodeNominalRow<TTemplate>(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, Jbig2Bitmap target, int y)
        where TTemplate : struct, ITemplate
    {
        var stride = target.Stride;
        var data = target.Data;
        var row = data.Slice(y * stride, stride);
        var above = y > 0 ? data.Slice((y - 1) * stride, stride) : default;
        var above2 = y > 1 ? data.Slice((y - RowsAbove) * stride, stride) : default;
        uint current = 0;
        for (var b = 0; b < stride; b++)
        {
            var window = Window(above, b);
            var window2 = Window(above2, b);
            var bits = Math.Min(ByteBits, target.Width - (b * ByteBits));
            var value = 0;
            for (var k = 0; k < bits; k++)
            {
                if (decoder.IsComplete)
                {
                    row[b] = (byte)value;
                    return false;
                }

                var shift = WindowShift - k;
                var context = TTemplate.Nominal(current, (window >> shift) & WindowMask, (window2 >> shift) & WindowMask);
                var bit = decoder.Decode(ref contexts[(int)context]);
                current = (current << 1) | (uint)bit;
                value |= bit << (Jbig2Bits.BitMask - k);
            }

            row[b] = (byte)value;
        }

        return true;
    }

    /// <summary>Decodes a row whose adaptive pixels are not all nominal, or that has a skip mask, a pixel at a time.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="at">The adaptive template pixels.</param>
    /// <param name="target">The bitmap.</param>
    /// <param name="skip">The skip mask, or <see langword="null"/>.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="false"/> when the data ran out.</returns>
    private static bool DecodeGeneralRow<TTemplate>(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, in Jbig2AtPixels at, Jbig2Bitmap target, Jbig2Bitmap? skip, int y)
        where TTemplate : struct, ITemplate
    {
        var stride = target.Stride;
        var data = target.Data;
        var row = data.Slice(y * stride, stride);
        var above = y > 0 ? data.Slice((y - 1) * stride, stride) : default;
        var above2 = y > 1 ? data.Slice((y - RowsAbove) * stride, stride) : default;
        var skipRow = skip is null ? default : skip.Row(y);
        uint current = 0;
        uint window = 0;
        uint window2 = 0;
        for (var x = 0; x < target.Width; x++)
        {
            var k = x & Jbig2Bits.BitMask;
            if (k == 0)
            {
                window = Window(above, x >> Jbig2Bits.ByteShift);
                window2 = Window(above2, x >> Jbig2Bits.ByteShift);
            }

            var bit = 0;
            if (Jbig2Bits.Get(skipRow, x, target.Width) == 0)
            {
                if (decoder.IsComplete)
                {
                    return false;
                }

                var shift = WindowShift - k;
                var context = TTemplate.Base(current, (window >> shift) & WindowMask, (window2 >> shift) & WindowMask) | AtContext<TTemplate>(target, at, x, y);
                bit = decoder.Decode(ref contexts[(int)context]);
                SetIfBlack(row, x, bit);
            }

            current = (current << 1) | (uint)bit;
        }

        return true;
    }

    /// <summary>Sets a decoded pixel when it is black.</summary>
    /// <param name="row">The row.</param>
    /// <param name="x">The column.</param>
    /// <param name="bit">The pixel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetIfBlack(Span<byte> row, int x, int bit)
    {
        if (bit != 0)
        {
            Jbig2Bits.SetBlack(row, x);
        }
    }

    /// <summary>Gets the context bits of the adaptive template pixels.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="target">The bitmap being decoded.</param>
    /// <param name="at">The adaptive template pixels.</param>
    /// <param name="x">The column being decoded.</param>
    /// <param name="y">The row being decoded.</param>
    /// <returns>The bits, in their context positions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AtContext<TTemplate>(Jbig2Bitmap target, in Jbig2AtPixels at, int x, int y)
        where TTemplate : struct, ITemplate
    {
        uint context = 0;
        for (var i = 0; i < TTemplate.AtCount; i++)
        {
            context |= (uint)target.GetPixel(x + at.X(i), y + at.Y(i)) << TTemplate.AtShift(i);
        }

        return context;
    }

    /// <summary>Gets three bytes of a row around a byte: the one before, the byte and the one after.</summary>
    /// <param name="row">The row, or an empty span above the region.</param>
    /// <param name="b">The byte.</param>
    /// <returns>The bytes, the earliest highest; bytes outside the row are 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Window(ReadOnlySpan<byte> row, int b)
    {
        if (row.IsEmpty)
        {
            return 0;
        }

        var window = (uint)row[b] << ByteBits;
        if (b > 0)
        {
            window |= (uint)row[b - 1] << PreviousByteShift;
        }

        if (b + 1 < row.Length)
        {
            window |= row[b + 1];
        }

        return window;
    }
}
