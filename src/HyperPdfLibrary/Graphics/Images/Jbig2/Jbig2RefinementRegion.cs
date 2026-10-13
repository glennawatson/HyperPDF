// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The generic refinement region decoding procedure (T.88 section 6.3): templates 0 and 1 with typical prediction. The
/// context is kept in rolling registers of the region row above and three reference rows, so each pixel shifts in only its new neighbours.
/// </summary>
internal static class Jbig2RefinementRegion
{
    /// <summary>The typical prediction context of template 0.</summary>
    private const int Template0Typical = 0x0010;

    /// <summary>The typical prediction context of template 1.</summary>
    private const int Template1Typical = 0x0008;

    /// <summary>The shift of the reference row's register in a template 0 context.</summary>
    private const int Template0Middle = 3;

    /// <summary>The shift of the reference row above's register in a template 0 context.</summary>
    private const int Template0Up = 6;

    /// <summary>The shift of the reference adaptive pixel in a template 0 context.</summary>
    private const int Template0ReferenceAt = 8;

    /// <summary>The shift of the pixel to the left in a template 0 context.</summary>
    private const int Template0Left = 9;

    /// <summary>The shift of the region row above's register in a template 0 context.</summary>
    private const int Template0Above = 10;

    /// <summary>The shift of the region adaptive pixel in a template 0 context.</summary>
    private const int Template0At = 12;

    /// <summary>The shift of the reference row's register in a template 1 context.</summary>
    private const int Template1Middle = 2;

    /// <summary>The shift of the reference row above's register in a template 1 context.</summary>
    private const int Template1Up = 5;

    /// <summary>The shift of the pixel to the left in a template 1 context.</summary>
    private const int Template1Left = 6;

    /// <summary>The shift of the region row above's register in a template 1 context.</summary>
    private const int Template1Above = 7;

    /// <summary>Decodes a refinement region into a white bitmap.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The <see cref="Jbig2RefinementParameters.ContextCount"/> contexts.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="target">The white bitmap receiving the region.</param>
    /// <returns><see langword="false"/> when the data ran out; the rows decoded so far are kept.</returns>
    internal static bool Decode(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, in Jbig2RefinementParameters parameters, Jbig2BitmapView reference, Jbig2Bitmap target)
    {
        var typical = 0;
        var typicalContext = parameters.Template == 0 ? Template0Typical : Template1Typical;
        for (var y = 0; y < target.Height; y++)
        {
            decoder.CancellationToken.ThrowIfCancellationRequested();
            if (parameters.TypicalPrediction)
            {
                if (decoder.IsComplete)
                {
                    return false;
                }

                typical ^= decoder.Decode(ref contexts[typicalContext]);
            }

            if (!DecodeRow(ref decoder, contexts, parameters, reference, target, y, typical != 0))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes one row.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="contexts">The contexts.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="target">The bitmap being decoded.</param>
    /// <param name="y">The row.</param>
    /// <param name="typical">Whether typical prediction may copy reference pixels on this row.</param>
    /// <returns><see langword="false"/> when the data ran out.</returns>
    private static bool DecodeRow(ref Jbig2ArithmeticDecoder decoder, Span<byte> contexts, in Jbig2RefinementParameters parameters, Jbig2BitmapView reference, Jbig2Bitmap target, int y, bool typical)
    {
        var row = target.Row(y);
        var state = new RowState(target, reference, parameters, y);
        for (var x = 0; x < target.Width; x++)
        {
            if (!typical || !IsTypical(reference, x, y, out var bit))
            {
                if (decoder.IsComplete)
                {
                    return false;
                }

                bit = decoder.Decode(ref contexts[(int)Context(ref state, target, reference, parameters, x, y)]);
            }

            if (bit != 0)
            {
                Jbig2Bits.SetBlack(row, x);
            }

            state.Advance(x, bit);
        }

        return true;
    }

    /// <summary>Builds the context of a pixel.</summary>
    /// <param name="state">The registers.</param>
    /// <param name="target">The bitmap being decoded.</param>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Context(ref RowState state, Jbig2Bitmap target, Jbig2BitmapView reference, in Jbig2RefinementParameters parameters, int x, int y)
    {
        if (parameters.Template != 0)
        {
            return state.Down | (state.Middle << Template1Middle) | (state.Up << Template1Up) | (state.Left << Template1Left) | (state.Above << Template1Above);
        }

        var at = parameters.At;
        var referenceAt = (uint)reference.GetPixel(x - parameters.ReferenceX + at.X2, y - parameters.ReferenceY + at.Y2);
        var regionAt = (uint)target.GetPixel(x + at.X1, y + at.Y1);
        return state.Down | (state.Middle << Template0Middle) | (state.Up << Template0Up) | (referenceAt << Template0ReferenceAt)
            | (state.Left << Template0Left) | (state.Above << Template0Above) | (regionAt << Template0At);
    }

    /// <summary>
    /// Determines whether typical prediction gives a pixel: the reference pixel and its eight neighbours all match. The
    /// neighbourhood is read without the reference offsets because both offsets are zero wherever TPGRON can be set: a
    /// refinement region segment has none (T.88 section 7.4.7.4) and a text region instance turns TPGRON off (table 12).
    /// </summary>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="bit">The predicted pixel.</param>
    /// <returns><see langword="true"/> when the pixel is predicted.</returns>
    private static bool IsTypical(Jbig2BitmapView reference, int x, int y, out int bit)
    {
        bit = reference.GetPixel(x, y);
        for (var dy = -1; dy <= 1; dy++)
        {
            var row = reference.Row(y + dy);
            for (var dx = -1; dx <= 1; dx++)
            {
                if (Jbig2Bits.Get(row, x + dx, reference.Width) != bit)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The rolling registers of one row.</summary>
    private ref struct RowState
    {
        /// <summary>The column offset of the pixel that enters a register as the decoder moves right.</summary>
        private const int Lookahead = 2;

        /// <summary>The mask of a 1-pixel register.</summary>
        private const uint OnePixel = 0x01;

        /// <summary>The mask of a 2-pixel register.</summary>
        private const uint TwoPixels = 0x03;

        /// <summary>The mask of a 3-pixel register.</summary>
        private const uint ThreePixels = 0x07;

        /// <summary>The region row above.</summary>
        private readonly ReadOnlySpan<byte> _above;

        /// <summary>The reference row above the reference row of the current row.</summary>
        private readonly ReadOnlySpan<byte> _referenceUp;

        /// <summary>The reference row of the current row.</summary>
        private readonly ReadOnlySpan<byte> _referenceMiddle;

        /// <summary>The reference row below the reference row of the current row.</summary>
        private readonly ReadOnlySpan<byte> _referenceDown;

        /// <summary>The region width.</summary>
        private readonly int _width;

        /// <summary>The reference width.</summary>
        private readonly int _referenceWidth;

        /// <summary>The reference column offset.</summary>
        private readonly int _dx;

        /// <summary>Whether template 0 is in use.</summary>
        private readonly bool _template0;

        /// <summary>Initializes a new instance of the <see cref="RowState"/> struct at the start of a row.</summary>
        /// <param name="target">The bitmap being decoded.</param>
        /// <param name="reference">The reference bitmap.</param>
        /// <param name="parameters">The region parameters.</param>
        /// <param name="y">The row.</param>
        internal RowState(Jbig2Bitmap target, Jbig2BitmapView reference, in Jbig2RefinementParameters parameters, int y)
        {
            _above = y > 0 ? target.Row(y - 1) : default;
            var referenceY = y - parameters.ReferenceY;
            _referenceUp = reference.Row(referenceY - 1);
            _referenceMiddle = reference.Row(referenceY);
            _referenceDown = reference.Row(referenceY + 1);
            _width = target.Width;
            _referenceWidth = reference.Width;
            _dx = parameters.ReferenceX;
            _template0 = parameters.Template == 0;

            // Template 0 starts with columns 0 and 1 of the row above, -dx to 1 - dx of the reference row above and
            // -1 - dx to 1 - dx of the reference rows; template 1 with -1 to 1, -dx alone, -1 - dx to 1 - dx and
            // -dx to 1 - dx.
            Above = Pixels(_above, _template0 ? 0 : -1, 1, _width);
            Up = Pixels(_referenceUp, -_dx, _template0 ? 1 - _dx : -_dx, _referenceWidth);
            Middle = Pixels(_referenceMiddle, -1 - _dx, 1 - _dx, _referenceWidth);
            Down = Pixels(_referenceDown, _template0 ? -1 - _dx : -_dx, 1 - _dx, _referenceWidth);
        }

        /// <summary>Gets the register of the region row above.</summary>
        internal uint Above { get; private set; }

        /// <summary>Gets the register of the pixel to the left.</summary>
        internal uint Left { get; private set; }

        /// <summary>Gets the register of the reference row above.</summary>
        internal uint Up { get; private set; }

        /// <summary>Gets the register of the reference row.</summary>
        internal uint Middle { get; private set; }

        /// <summary>Gets the register of the reference row below.</summary>
        internal uint Down { get; private set; }

        /// <summary>Moves the registers one pixel right.</summary>
        /// <param name="x">The column just decoded.</param>
        /// <param name="bit">The pixel just decoded.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Advance(int x, int bit)
        {
            var next = x - _dx + Lookahead;
            Left = (uint)bit;
            Above = Shift(Above, Jbig2Bits.Get(_above, x + Lookahead, _width), _template0 ? TwoPixels : ThreePixels);
            Up = Shift(Up, Jbig2Bits.Get(_referenceUp, _template0 ? next : next - 1, _referenceWidth), _template0 ? TwoPixels : OnePixel);
            Middle = Shift(Middle, Jbig2Bits.Get(_referenceMiddle, next, _referenceWidth), ThreePixels);
            Down = Shift(Down, Jbig2Bits.Get(_referenceDown, next, _referenceWidth), _template0 ? ThreePixels : TwoPixels);
        }

        /// <summary>Shifts a pixel into a register.</summary>
        /// <param name="register">The register.</param>
        /// <param name="pixel">The pixel.</param>
        /// <param name="mask">The register's mask.</param>
        /// <returns>The register with the pixel shifted in.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Shift(uint register, int pixel, uint mask) => ((register << 1) | (uint)pixel) & mask;

        /// <summary>Reads a run of pixels into a register, the last in bit 0.</summary>
        /// <param name="row">The row.</param>
        /// <param name="first">The first column.</param>
        /// <param name="last">The last column.</param>
        /// <param name="width">The row width.</param>
        /// <returns>The register.</returns>
        private static uint Pixels(ReadOnlySpan<byte> row, int first, int last, int width)
        {
            uint register = 0;
            for (var x = first; x <= last; x++)
            {
                register = (register << 1) | (uint)Jbig2Bits.Get(row, x, width);
            }

            return register;
        }
    }
}
