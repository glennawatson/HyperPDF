// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The generic refinement region encoding procedure (the inverse of T.88 section 6.3) with template 0 or 1, without typical
/// prediction. A bitmap is coded against a reference bitmap placed at an offset; pixels are 1 for black.
/// </summary>
internal static class Jbig2RefinementEncoder
{
    /// <summary>The refinement template the encoder writes.</summary>
    internal const int Template = 0;

    /// <summary>The number of contexts of template 0.</summary>
    internal const int ContextCount = 1 << 13;

    /// <summary>The shift of the reference pixels one row up in a template 0 context.</summary>
    private const int UpShift = 6;

    /// <summary>The shift of the middle reference pixels in a template 0 context.</summary>
    private const int MiddleShift = 3;

    /// <summary>The shift of the adaptive reference pixel in a template 0 context.</summary>
    private const int ReferenceAtShift = 8;

    /// <summary>The shift of the pixel to the left in a template 0 context.</summary>
    private const int LeftShift = 9;

    /// <summary>The shift of the pixels in the row above in a template 0 context.</summary>
    private const int AboveShift = 10;

    /// <summary>The shift of the adaptive pixel of the bitmap being coded in a template 0 context.</summary>
    private const int RegionAtShift = 12;

    /// <summary>Gets the nominal adaptive pixels the segment writers declare for template 0.</summary>
    internal static Jbig2AtPixels NominalAt => Jbig2AtPixels.Refinement;

    /// <summary>Writes the four adaptive pixel offsets that follow the flags of a segment using template 0 (T.88 section 7.4.7.3).</summary>
    /// <param name="output">The stream.</param>
    internal static void WriteAdaptivePixels(ref PooledBuffer output)
    {
        var at = NominalAt;
        output.WriteByte((byte)(sbyte)at.X1);
        output.WriteByte((byte)(sbyte)at.Y1);
        output.WriteByte((byte)(sbyte)at.X2);
        output.WriteByte((byte)(sbyte)at.Y2);
    }

    /// <summary>Encodes a bitmap as a refinement of a reference bitmap.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="contexts">The <see cref="ContextCount"/> contexts.</param>
    /// <param name="target">The bitmap to code, 1 for black.</param>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="offset">The reference's offset: target pixel (x, y) corresponds to reference pixel (x - X, y - Y).</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void Encode(ref Jbig2MqEncoder encoder, Span<byte> contexts, in Jbig2BitmapView target, in Jbig2BitmapView reference, Jbig2Point offset, CancellationToken cancellationToken)
    {
        var at = NominalAt;
        for (var y = 0; y < target.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < target.Width; x++)
            {
                var context = Context(target, reference, offset, at, x, y);
                encoder.Encode(ref contexts[(int)context], target.GetPixel(x, y));
            }
        }
    }

    /// <summary>Forms the template 0 context of a pixel (T.88 figure 12).</summary>
    /// <param name="target">The bitmap being coded.</param>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="offset">The reference's offset.</param>
    /// <param name="at">The adaptive pixels.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The context.</returns>
    private static uint Context(in Jbig2BitmapView target, in Jbig2BitmapView reference, Jbig2Point offset, in Jbig2AtPixels at, int x, int y)
    {
        var rx = x - offset.X;
        var ry = y - offset.Y;
        var down = Triple(reference, rx, ry + 1);
        var middle = Triple(reference, rx, ry);

        // The row above the pixel is on the target's own bitmap; the others come from the reference.
        var up = ((uint)reference.GetPixel(rx, ry - 1) << 1) | (uint)reference.GetPixel(rx + 1, ry - 1);
        var above = ((uint)target.GetPixel(x, y - 1) << 1) | (uint)target.GetPixel(x + 1, y - 1);
        var left = (uint)target.GetPixel(x - 1, y);
        var referenceAt = (uint)reference.GetPixel(rx + at.X2, ry + at.Y2);
        var regionAt = (uint)target.GetPixel(x + at.X1, y + at.Y1);
        return down | (middle << MiddleShift) | (up << UpShift) | (referenceAt << ReferenceAtShift)
            | (left << LeftShift) | (above << AboveShift) | (regionAt << RegionAtShift);
    }

    /// <summary>Gets the three reference pixels of a row around a column, leftmost in the top bit.</summary>
    /// <param name="reference">The reference bitmap.</param>
    /// <param name="x">The middle column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The three pixels.</returns>
    private static uint Triple(in Jbig2BitmapView reference, int x, int y) =>
        ((uint)reference.GetPixel(x - 1, y) << 2) | ((uint)reference.GetPixel(x, y) << 1) | (uint)reference.GetPixel(x + 1, y);
}
