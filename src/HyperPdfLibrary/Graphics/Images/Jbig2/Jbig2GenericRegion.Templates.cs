// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <content>
/// The four generic templates (T.88 figures 3 to 6). Each builds a context from the decoded pixels before the current one
/// (<c>current</c>, the pixel just left in bit 0) and 8-pixel windows of the rows one and two above (<c>above</c> and
/// <c>above2</c>, covering columns x - 4 to x + 3 with x + 3 in bit 0). T.88 section 6.2.5.3 forms the context from the template pixels but any one-to-one
/// numbering of them decodes identically, so the numbering below is our choice: the current row in the low bits, then the
/// row above, then the row two above. The typical-prediction contexts are the same pixel patterns (figures 8 to 11) in that numbering.
/// </content>
internal static partial class Jbig2GenericRegion
{
    /// <summary>A generic template's context layout.</summary>
    private interface ITemplate
    {
        /// <summary>Gets the context used for typical prediction.</summary>
        static abstract int TypicalContext { get; }

        /// <summary>Gets the number of adaptive template pixels.</summary>
        static abstract int AtCount { get; }

        /// <summary>Builds the context when every adaptive pixel is in its nominal place.</summary>
        /// <param name="current">The decoded pixels of the current row.</param>
        /// <param name="above">The window of the row above.</param>
        /// <param name="above2">The window of the row two above.</param>
        /// <returns>The context.</returns>
        static abstract uint Nominal(uint current, uint above, uint above2);

        /// <summary>Builds the context without the adaptive pixels.</summary>
        /// <param name="current">The decoded pixels of the current row.</param>
        /// <param name="above">The window of the row above.</param>
        /// <param name="above2">The window of the row two above.</param>
        /// <returns>The context.</returns>
        static abstract uint Base(uint current, uint above, uint above2);

        /// <summary>Gets the context bit of an adaptive pixel.</summary>
        /// <param name="index">The adaptive pixel.</param>
        /// <returns>The bit position.</returns>
        static abstract int AtShift(int index);
    }

    /// <summary>Template 0: 16 context bits with four adaptive pixels.</summary>
    private readonly struct Template0 : ITemplate
    {
        /// <summary>The mask of the four pixels left of the current one.</summary>
        private const uint CurrentMask = 0x0F;

        /// <summary>The mask of the seven nominal pixels of the row above, x - 3 to x + 3.</summary>
        private const uint AboveNominalMask = 0x7F;

        /// <summary>The shift of the row above's pixels in a nominal context.</summary>
        private const int AboveNominalShift = 4;

        /// <summary>The mask of the five nominal pixels of the row two above, x - 2 to x + 2.</summary>
        private const uint Above2NominalMask = 0x1F;

        /// <summary>The shift of the row two above's pixels in a nominal context.</summary>
        private const int Above2NominalShift = 11;

        /// <summary>The mask of the five fixed pixels of the row above, x - 2 to x + 2.</summary>
        private const uint AboveMask = 0x1F;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 5;

        /// <summary>The mask of the three fixed pixels of the row two above, x - 1 to x + 1.</summary>
        private const uint Above2Mask = 0x07;

        /// <summary>The window shift that drops x + 2 and x + 3 from the row two above.</summary>
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

        /// <summary>The mask of the six nominal pixels of the row above, x - 2 to x + 3.</summary>
        private const uint AboveNominalMask = 0x3F;

        /// <summary>The shift of the row above's pixels in a nominal context.</summary>
        private const int AboveNominalShift = 3;

        /// <summary>The mask of the five fixed pixels of the row above, x - 2 to x + 2.</summary>
        private const uint AboveMask = 0x1F;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 4;

        /// <summary>The mask of the four pixels of the row two above, x - 1 to x + 2.</summary>
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

        /// <summary>The mask of the five nominal pixels of the row above, x - 2 to x + 2.</summary>
        private const uint AboveNominalMask = 0x1F;

        /// <summary>The shift of the row above's pixels in a nominal context, and of the adaptive pixel.</summary>
        private const int AboveNominalShift = 2;

        /// <summary>The mask of the four fixed pixels of the row above, x - 2 to x + 1.</summary>
        private const uint AboveMask = 0x0F;

        /// <summary>The window shift that drops x + 2 and x + 3 from a row.</summary>
        private const int Drop = 2;

        /// <summary>The shift of the row above's fixed pixels.</summary>
        private const int AboveShift = 3;

        /// <summary>The mask of the three pixels of the row two above, x - 1 to x + 1.</summary>
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

        /// <summary>The mask of the six nominal pixels of the row above, x - 3 to x + 2.</summary>
        private const uint AboveNominalMask = 0x3F;

        /// <summary>The shift of the row above's pixels in a nominal context, and of the adaptive pixel.</summary>
        private const int AboveNominalShift = 4;

        /// <summary>The mask of the five fixed pixels of the row above, x - 3 to x + 1.</summary>
        private const uint AboveMask = 0x1F;

        /// <summary>The window shift that drops x + 2 and x + 3 from the row above.</summary>
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
