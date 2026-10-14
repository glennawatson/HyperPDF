// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The generic region encoding procedure: the inverse of T.88 section 6.2 for arithmetic coding with templates 0 to 3,
/// typical prediction and adaptive template pixels. Pixels are JBIG2 pixels: 1 is black.
/// </summary>
internal static class Jbig2GenericEncoder
{
    /// <summary>The mask of an 8-pixel window.</summary>
    private const uint WindowMask = 0xFF;

    /// <summary>The number of pixels the window reaches ahead of the pixel being coded.</summary>
    private const int LookAhead = 3;

    /// <summary>The number of rows above whose pixels form the context.</summary>
    private const int RowsAbove = 2;

    /// <summary>The number of generic template 2.</summary>
    private const int TemplateTwo = 2;

    /// <summary>A generic template's context layout, the same numbering the decoder uses (T.88 figures 3 to 6).</summary>
    private interface ITemplate
    {
        /// <summary>Gets the context used for typical prediction.</summary>
        static abstract int TypicalContext { get; }

        /// <summary>Gets the number of adaptive template pixels.</summary>
        static abstract int AtCount { get; }

        /// <summary>Builds the context when every adaptive pixel is in its nominal place.</summary>
        /// <param name="current">The coded pixels of the current row.</param>
        /// <param name="above">The window of the row above.</param>
        /// <param name="above2">The window of the row two above.</param>
        /// <returns>The context.</returns>
        static abstract uint Nominal(uint current, uint above, uint above2);

        /// <summary>Builds the context without the adaptive pixels.</summary>
        /// <param name="current">The coded pixels of the current row.</param>
        /// <param name="above">The window of the row above.</param>
        /// <param name="above2">The window of the row two above.</param>
        /// <returns>The context.</returns>
        static abstract uint Base(uint current, uint above, uint above2);

        /// <summary>Gets the context bit of an adaptive pixel.</summary>
        /// <param name="index">The adaptive pixel.</param>
        /// <returns>The bit position.</returns>
        static abstract int AtShift(int index);
    }

    /// <summary>Encodes a bitmap as an arithmetic-coded generic region.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The <see cref="Jbig2GenericParameters.ContextCount"/> contexts.</param>
    /// <param name="bitmap">The bitmap, 1 for black, with zero padding bits.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void Encode(ref Jbig2MqEncoder encoder, Span<byte> contexts, in Jbig2BitmapView bitmap, in Jbig2GenericParameters parameters, CancellationToken cancellationToken)
    {
        switch (parameters.Template)
        {
            case 0:
            {
                Encode<Template0>(ref encoder, contexts, bitmap, parameters, cancellationToken);
                break;
            }

            case 1:
            {
                Encode<Template1>(ref encoder, contexts, bitmap, parameters, cancellationToken);
                break;
            }

            case TemplateTwo:
            {
                Encode<Template2>(ref encoder, contexts, bitmap, parameters, cancellationToken);
                break;
            }

            default:
            {
                Encode<Template3>(ref encoder, contexts, bitmap, parameters, cancellationToken);
                break;
            }
        }
    }

    /// <summary>Encodes a bitmap with one template.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void Encode<TTemplate>(ref Jbig2MqEncoder encoder, Span<byte> contexts, in Jbig2BitmapView bitmap, in Jbig2GenericParameters parameters, CancellationToken cancellationToken)
        where TTemplate : struct, ITemplate
    {
        var nominal = parameters.At.IsNominal(parameters.Template);
        var previousTypical = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (parameters.TypicalPrediction && EncodeTypical<TTemplate>(ref encoder, contexts, bitmap, y, ref previousTypical))
            {
                continue;
            }

            if (nominal)
            {
                EncodeNominalRow<TTemplate>(ref encoder, contexts, bitmap, y);
            }
            else
            {
                EncodeGeneralRow<TTemplate>(ref encoder, contexts, bitmap, parameters.At, y);
            }
        }
    }

    /// <summary>Codes the typical prediction flag of a row (T.88 section 6.2.5.7).</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="y">The row.</param>
    /// <param name="previousTypical">Whether the previous row was typical; updated.</param>
    /// <returns><see langword="true"/> when the row equals the row above and need not be coded.</returns>
    private static bool EncodeTypical<TTemplate>(ref Jbig2MqEncoder encoder, Span<byte> contexts, in Jbig2BitmapView bitmap, int y, ref int previousTypical)
        where TTemplate : struct, ITemplate
    {
        var row = bitmap.Row(y);
        var above = bitmap.Row(y - 1);
        var typical = above.IsEmpty ? row.IndexOfAnyExcept((byte)0) < 0 : row.SequenceEqual(above);
        var flag = typical ? 1 : 0;
        encoder.Encode(ref contexts[TTemplate.TypicalContext], flag ^ previousTypical);
        previousTypical = flag;
        return typical;
    }

    /// <summary>Encodes a row whose adaptive pixels are all nominal.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="y">The row.</param>
    private static void EncodeNominalRow<TTemplate>(ref Jbig2MqEncoder encoder, Span<byte> contexts, in Jbig2BitmapView bitmap, int y)
        where TTemplate : struct, ITemplate
    {
        var width = bitmap.Width;
        var row = bitmap.Row(y);
        var above = bitmap.Row(y - 1);
        var above2 = bitmap.Row(y - RowsAbove);
        uint current = 0;
        var window = Seed(above, width);
        var window2 = Seed(above2, width);
        for (var x = 0; x < width; x++)
        {
            var bit = Jbig2Bits.Get(row, x, width);
            encoder.Encode(ref contexts[(int)TTemplate.Nominal(current, window, window2)], bit);
            current = (current << 1) | (uint)bit;
            window = ((window << 1) | (uint)Jbig2Bits.Get(above, x + LookAhead + 1, width)) & WindowMask;
            window2 = ((window2 << 1) | (uint)Jbig2Bits.Get(above2, x + LookAhead + 1, width)) & WindowMask;
        }
    }

    /// <summary>Encodes a row with adaptive pixels away from their nominal places.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="at">The adaptive template pixels.</param>
    /// <param name="y">The row.</param>
    private static void EncodeGeneralRow<TTemplate>(ref Jbig2MqEncoder encoder, Span<byte> contexts, in Jbig2BitmapView bitmap, in Jbig2AtPixels at, int y)
        where TTemplate : struct, ITemplate
    {
        var width = bitmap.Width;
        var row = bitmap.Row(y);
        var above = bitmap.Row(y - 1);
        var above2 = bitmap.Row(y - RowsAbove);
        uint current = 0;
        var window = Seed(above, width);
        var window2 = Seed(above2, width);
        for (var x = 0; x < width; x++)
        {
            var bit = Jbig2Bits.Get(row, x, width);
            var context = TTemplate.Base(current, window, window2) | AtContext<TTemplate>(bitmap, at, x, y);
            encoder.Encode(ref contexts[(int)context], bit);
            current = (current << 1) | (uint)bit;
            window = ((window << 1) | (uint)Jbig2Bits.Get(above, x + LookAhead + 1, width)) & WindowMask;
            window2 = ((window2 << 1) | (uint)Jbig2Bits.Get(above2, x + LookAhead + 1, width)) & WindowMask;
        }
    }

    /// <summary>Builds the window of a row for the first pixel: columns -4 to 3 with column 3 in bit 0.</summary>
    /// <param name="row">The row, or empty above the region.</param>
    /// <param name="width">The width in pixels.</param>
    /// <returns>The window.</returns>
    private static uint Seed(ReadOnlySpan<byte> row, int width)
    {
        uint window = 0;
        for (var x = 0; x <= LookAhead; x++)
        {
            window = (window << 1) | (uint)Jbig2Bits.Get(row, x, width);
        }

        return window;
    }

    /// <summary>Gets the context bits of the adaptive template pixels.</summary>
    /// <typeparam name="TTemplate">The template.</typeparam>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="at">The adaptive template pixels.</param>
    /// <param name="x">The column being coded.</param>
    /// <param name="y">The row being coded.</param>
    /// <returns>The bits, in their context positions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AtContext<TTemplate>(in Jbig2BitmapView bitmap, in Jbig2AtPixels at, int x, int y)
        where TTemplate : struct, ITemplate
    {
        uint context = 0;
        for (var i = 0; i < TTemplate.AtCount; i++)
        {
            context |= (uint)bitmap.GetPixel(x + at.X(i), y + at.Y(i)) << TTemplate.AtShift(i);
        }

        return context;
    }

    /// <summary>Template 0: 16 context bits with four adaptive pixels.</summary>
    private readonly struct Template0 : ITemplate
    {
        /// <summary>The mask of the four pixels left of the current one.</summary>
        private const uint CurrentMask = 0x0F;

        /// <summary>The mask of the seven nominal pixels of the row above.</summary>
        private const uint AboveNominalMask = 0x7F;

        /// <summary>The shift of the row above's pixels in a nominal context.</summary>
        private const int AboveNominalShift = 4;

        /// <summary>The mask of the five nominal pixels of the row two above.</summary>
        private const uint Above2NominalMask = 0x1F;

        /// <summary>The shift of the row two above's pixels in a nominal context.</summary>
        private const int Above2NominalShift = 11;

        /// <summary>The mask of the five fixed pixels of the row above.</summary>
        private const uint AboveMask = 0x1F;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 5;

        /// <summary>The mask of the three fixed pixels of the row two above.</summary>
        private const uint Above2Mask = 0x07;

        /// <summary>The window shift that drops two pixels from the row two above.</summary>
        private const int Above2Drop = 2;

        /// <summary>The shift of the row two above's fixed pixels.</summary>
        private const int Above2Shift = 12;

        /// <inheritdoc/>
        public static int TypicalContext => 0x9B25;

        /// <inheritdoc/>
        public static int AtCount => AtShifts.Length;

        /// <summary>Gets the context bit of each adaptive pixel.</summary>
        private static ReadOnlySpan<byte> AtShifts => [0x04, 0x0A, 0x0B, 0x0F];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Nominal(uint current, uint above, uint above2) =>
            (current & CurrentMask) | ((above & AboveNominalMask) << AboveNominalShift) | (((above2 >> 1) & Above2NominalMask) << Above2NominalShift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Base(uint current, uint above, uint above2) =>
            (current & CurrentMask) | (((above >> 1) & AboveMask) << AboveShift) | (((above2 >> Above2Drop) & Above2Mask) << Above2Shift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AtShift(int index) => AtShifts[index];
    }

    /// <summary>Template 1: 13 context bits with one adaptive pixel.</summary>
    private readonly struct Template1 : ITemplate
    {
        /// <summary>The mask of the three pixels left of the current one.</summary>
        private const uint CurrentMask = 0x07;

        /// <summary>The mask of the six nominal pixels of the row above.</summary>
        private const uint AboveNominalMask = 0x3F;

        /// <summary>The shift of the row above's pixels in a nominal context, and of the adaptive pixel.</summary>
        private const int AboveNominalShift = 3;

        /// <summary>The mask of the five fixed pixels of the row above.</summary>
        private const uint AboveMask = 0x1F;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 4;

        /// <summary>The mask of the four pixels of the row two above.</summary>
        private const uint Above2Mask = 0x0F;

        /// <summary>The shift of the row two above's pixels.</summary>
        private const int Above2Shift = 9;

        /// <inheritdoc/>
        public static int TypicalContext => 0x0795;

        /// <inheritdoc/>
        public static int AtCount => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Nominal(uint current, uint above, uint above2) =>
            (current & CurrentMask) | ((above & AboveNominalMask) << AboveNominalShift) | (((above2 >> 1) & Above2Mask) << Above2Shift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Base(uint current, uint above, uint above2) =>
            (current & CurrentMask) | (((above >> 1) & AboveMask) << AboveShift) | (((above2 >> 1) & Above2Mask) << Above2Shift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AtShift(int index) => AboveNominalShift;
    }

    /// <summary>Template 2: 10 context bits with one adaptive pixel.</summary>
    private readonly struct Template2 : ITemplate
    {
        /// <summary>The mask of the two pixels left of the current one.</summary>
        private const uint CurrentMask = 0x03;

        /// <summary>The mask of the five nominal pixels of the row above.</summary>
        private const uint AboveNominalMask = 0x1F;

        /// <summary>The shift of the row above's pixels in a nominal context, and of the adaptive pixel.</summary>
        private const int AboveNominalShift = 2;

        /// <summary>The mask of the four fixed pixels of the row above.</summary>
        private const uint AboveMask = 0x0F;

        /// <summary>The window shift that drops two pixels from a row.</summary>
        private const int Drop = 2;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 3;

        /// <summary>The mask of the three pixels of the row two above.</summary>
        private const uint Above2Mask = 0x07;

        /// <summary>The shift of the row two above's pixels.</summary>
        private const int Above2Shift = 7;

        /// <inheritdoc/>
        public static int TypicalContext => 0x00E5;

        /// <inheritdoc/>
        public static int AtCount => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Nominal(uint current, uint above, uint above2) =>
            (current & CurrentMask) | (((above >> 1) & AboveNominalMask) << AboveNominalShift) | (((above2 >> Drop) & Above2Mask) << Above2Shift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Base(uint current, uint above, uint above2) =>
            (current & CurrentMask) | (((above >> Drop) & AboveMask) << AboveShift) | (((above2 >> Drop) & Above2Mask) << Above2Shift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AtShift(int index) => AboveNominalShift;
    }

    /// <summary>Template 3: 10 context bits from the current row and the row above, with one adaptive pixel.</summary>
    private readonly struct Template3 : ITemplate
    {
        /// <summary>The mask of the four pixels left of the current one.</summary>
        private const uint CurrentMask = 0x0F;

        /// <summary>The mask of the six nominal pixels of the row above.</summary>
        private const uint AboveNominalMask = 0x3F;

        /// <summary>The shift of the row above's pixels in a nominal context, and of the adaptive pixel.</summary>
        private const int AboveNominalShift = 4;

        /// <summary>The mask of the five fixed pixels of the row above.</summary>
        private const uint AboveMask = 0x1F;

        /// <summary>The window shift that drops two pixels from the row above.</summary>
        private const int Drop = 2;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 5;

        /// <inheritdoc/>
        public static int TypicalContext => 0x0195;

        /// <inheritdoc/>
        public static int AtCount => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Nominal(uint current, uint above, uint above2) =>
            (current & CurrentMask) | (((above >> 1) & AboveNominalMask) << AboveNominalShift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Base(uint current, uint above, uint above2) =>
            (current & CurrentMask) | (((above >> Drop) & AboveMask) << AboveShift);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AtShift(int index) => AboveNominalShift;
    }
}
