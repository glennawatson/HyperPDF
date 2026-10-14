// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The generic region decoding procedure for the extended template (T.88 section 6.2.5, GBTEMPLATE 0 with EXTTEMPLATE 1): 16
/// template pixels, four of them fixed and twelve adaptive (figure 3(b)). Context bits are numbered by the nominal place of each
/// pixel, the same numbering generic template 0 uses, so a region whose adaptive pixels are all nominal decodes exactly as
/// template 0 does and takes its fast path.
/// </summary>
internal static class Jbig2ExtendedGenericRegion
{
    /// <summary>The contexts of the extended template: 16 pixels.</summary>
    internal const int ContextCount = 1 << 16;

    /// <summary>The context for typical prediction (figure 8), in this numbering.</summary>
    private const int TypicalContext = 0x9B25;

    /// <summary>The context bit of the pixel above and to the right; the pixel left of the current one takes bit 0.</summary>
    private const int AboveRightBit = 6;

    /// <summary>The context bit of the pixel above.</summary>
    private const int AboveBit = 7;

    /// <summary>The context bit of the pixel above and to the left.</summary>
    private const int AboveLeftBit = 8;

    /// <summary>Gets the context bit of each adaptive pixel A1 to A12: the bit of its nominal place (T.88 table 5).</summary>
    private static ReadOnlySpan<byte> AtBits => [0x01, 0x0D, 0x09, 0x0E, 0x0C, 0x05, 0x02, 0x03, 0x0B, 0x04, 0x0F, 0x0A];

    /// <summary>Decodes an arithmetic-coded generic region that uses the extended template into a white bitmap.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The <see cref="ContextCount"/> contexts.</param>
    /// <param name="typicalPrediction">Whether typical prediction is on, TPGDON.</param>
    /// <param name="at">The twelve adaptive template pixels.</param>
    /// <param name="target">The white bitmap receiving the region.</param>
    /// <param name="skip">The pixels to leave white without decoding, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the data ran out; the rows decoded so far are kept.</returns>
    internal static bool Decode(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, bool typicalPrediction, in Jbig2ExtendedAtPixels at, Jbig2Bitmap target, Jbig2Bitmap? skip)
    {
        if (skip is null && at == Jbig2ExtendedAtPixels.Nominal)
        {
            return Jbig2GenericRegion.Decode(ref decoder, contexts, new(0, typicalPrediction, Jbig2AtPixels.Template0), target, null);
        }

        var typical = 0;
        for (var y = 0; y < target.Height; y++)
        {
            decoder.CancellationToken.ThrowIfCancellationRequested();
            if (typicalPrediction)
            {
                if (decoder.IsComplete)
                {
                    return false;
                }

                typical ^= decoder.Decode(ref contexts[TypicalContext]);
                if (typical != 0)
                {
                    CopyAbove(target, y);
                    continue;
                }
            }

            if (!DecodeRow(ref decoder, contexts, at, target, skip, y))
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

    /// <summary>Decodes one row a pixel at a time.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="at">The adaptive template pixels.</param>
    /// <param name="target">The bitmap.</param>
    /// <param name="skip">The skip mask, or <see langword="null"/>.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="false"/> when the data ran out.</returns>
    private static bool DecodeRow(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, in Jbig2ExtendedAtPixels at, Jbig2Bitmap target, Jbig2Bitmap? skip, int y)
    {
        var row = target.Row(y);
        for (var x = 0; x < target.Width; x++)
        {
            if (skip is not null && skip.GetPixel(x, y) != 0)
            {
                continue;
            }

            if (decoder.IsComplete)
            {
                return false;
            }

            if (decoder.Decode(ref contexts[(int)Context(target, at, x, y)]) != 0)
            {
                Jbig2Bits.SetBlack(row, x);
            }
        }

        return true;
    }

    /// <summary>Gathers the 16 template pixels of a pixel into a context.</summary>
    /// <param name="target">The bitmap being decoded.</param>
    /// <param name="at">The adaptive template pixels.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The context.</returns>
    private static uint Context(Jbig2Bitmap target, in Jbig2ExtendedAtPixels at, int x, int y)
    {
        var context = (uint)target.GetPixel(x - 1, y);
        context |= (uint)target.GetPixel(x + 1, y - 1) << AboveRightBit;
        context |= (uint)target.GetPixel(x, y - 1) << AboveBit;
        context |= (uint)target.GetPixel(x - 1, y - 1) << AboveLeftBit;
        var bits = AtBits;
        for (var i = 0; i < Jbig2ExtendedAtPixels.Count; i++)
        {
            context |= (uint)target.GetPixel(x + at.X(i), y + at.Y(i)) << bits[i];
        }

        return context;
    }
}
