// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The text region decoding procedure (T.88 section 6.4): symbol instances placed in strips, arithmetic or Huffman
/// coded, optionally refined. Coordinates are 64-bit and must fit a 32-bit integer when placed.
/// </summary>
internal static partial class Jbig2TextRegion
{
    /// <summary>Decodes an arithmetic-coded text region.</summary>
    /// <param name="decoder">The arithmetic decoder.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="symbols">The symbols the region uses.</param>
    /// <param name="integers">The integer and symbol ID contexts.</param>
    /// <param name="refinementContexts">The refinement contexts, or an empty span when instances are not refined.</param>
    /// <param name="region">The bitmap receiving the region, of the region's size.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    internal static bool DecodeArithmetic(
        ref Jbig2ArithmeticDecoder decoder,
        Jbig2TextRegionSettings settings,
        Jbig2SymbolSet symbols,
        Jbig2IntegerContexts integers,
        Span<byte> refinementContexts,
        Jbig2Bitmap region,
        Jbig2Workspace workspace)
    {
        var session = new Session(settings, symbols, refinementContexts, region, workspace);
        return session.DecodeArithmetic(ref decoder, integers);
    }

    /// <summary>Decodes a Huffman-coded text region.</summary>
    /// <param name="reader">The reader, at the coded instances.</param>
    /// <param name="settings">The region parameters, with every table set.</param>
    /// <param name="symbols">The symbols the region uses.</param>
    /// <param name="refinementContexts">The refinement contexts, or an empty span when instances are not refined.</param>
    /// <param name="region">The bitmap receiving the region, of the region's size.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <returns><see langword="false"/> when the data is damaged.</returns>
    internal static bool DecodeHuffman(ref Jbig2Reader reader, Jbig2TextRegionSettings settings, Jbig2SymbolSet symbols, Span<byte> refinementContexts, Jbig2Bitmap region, Jbig2Workspace workspace)
    {
        var session = new Session(settings, symbols, refinementContexts, region, workspace);
        return session.DecodeHuffman(ref reader);
    }

    /// <summary>
    /// Gets the S advance before an instance is placed: its width less one when right corners are used across, or its
    /// height less one when bottom corners are used down (T.88 section 6.4.5 steps 3 c vi to viii). The arithmetic is
    /// signed, so an empty instance moves the position back by one.
    /// </summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="width">The instance width.</param>
    /// <param name="height">The instance height.</param>
    /// <returns>The advance.</returns>
    private static long LeadingAdvance(Jbig2TextRegionSettings settings, int width, int height)
    {
        var right = settings.Corner is Jbig2Corner.TopRight or Jbig2Corner.BottomRight;
        var bottom = settings.Corner is Jbig2Corner.BottomLeft or Jbig2Corner.BottomRight;
        if (!settings.Transposed && right)
        {
            return width - 1L;
        }

        return settings.Transposed && bottom ? height - 1L : 0;
    }

    /// <summary>Gets where an instance goes and how far the S coordinate moves after it (T.88 6.4.5 step 3 c).</summary>
    /// <param name="settings">The region parameters.</param>
    /// <param name="s">The instance's coordinate along the strip.</param>
    /// <param name="t">The instance's coordinate across the strip.</param>
    /// <param name="width">The instance width.</param>
    /// <param name="height">The instance height.</param>
    /// <returns>The origin and the advance along the strip.</returns>
    private static Origin GetOrigin(Jbig2TextRegionSettings settings, long s, long t, int width, int height)
    {
        var across = width - 1L;
        var down = height - 1L;
        return !settings.Transposed
            ? settings.Corner switch
            {
                Jbig2Corner.TopLeft => new(s, t, across),
                Jbig2Corner.TopRight => new(s - width + 1, t, 0),
                Jbig2Corner.BottomLeft => new(s, t - height + 1, across),
                _ => new(s - width + 1, t - height + 1, 0),
            }
            : settings.Corner switch
        {
            Jbig2Corner.TopLeft => new(t, s, down),
            Jbig2Corner.TopRight => new(t - width + 1, s, down),
            Jbig2Corner.BottomLeft => new(t, s - height + 1, 0),
            _ => new(t - width + 1, s - height + 1, 0),
        };
    }

    /// <summary>Checks that a coordinate fits a 32-bit integer, the range of the standard's coordinates.</summary>
    /// <param name="value">The value.</param>
    /// <param name="result">The value as an integer.</param>
    /// <returns><see langword="false"/> when it does not fit.</returns>
    private static bool TryFitInt(long value, out int result)
    {
        result = (int)value;
        return value is >= int.MinValue and <= int.MaxValue;
    }

    /// <summary>One symbol instance before it is placed.</summary>
    /// <param name="Id">The symbol ID.</param>
    /// <param name="T">The T coordinate.</param>
    /// <param name="Refine">Whether the instance is refined.</param>
    [DebuggerDisplay("Instance: symbol {Id} at T {T}")]
    private readonly record struct Instance(long Id, int T, bool Refine);

    /// <summary>Where an instance goes in the region, and how far S moves after it.</summary>
    /// <param name="X">The column of the instance's left edge.</param>
    /// <param name="Y">The row of the instance's top edge.</param>
    /// <param name="Advance">The S advance after the instance.</param>
    [DebuggerDisplay("Origin: ({X}, {Y}) advance {Advance}")]
    private readonly record struct Origin(long X, long Y, long Advance);

    /// <summary>The size of a refined instance.</summary>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    [DebuggerDisplay("Size: {Width}x{Height}")]
    private readonly record struct Size(int Width, int Height);

    /// <summary>The refinement deltas of an instance (T.88 6.4.11).</summary>
    /// <param name="DeltaWidth">RDW.</param>
    /// <param name="DeltaHeight">RDH.</param>
    /// <param name="OffsetX">RDX.</param>
    /// <param name="OffsetY">RDY.</param>
    [DebuggerDisplay("Refinement: {DeltaWidth}, {DeltaHeight}, {OffsetX}, {OffsetY}")]
    private readonly record struct Refinement(int DeltaWidth, int DeltaHeight, int OffsetX, int OffsetY)
    {
        /// <summary>Gets the refined size and the refinement parameters.</summary>
        /// <param name="reference">The symbol being refined.</param>
        /// <param name="settings">The region parameters.</param>
        /// <param name="size">The refined size; zero when it is not a valid size.</param>
        /// <param name="parameters">The refinement parameters.</param>
        /// <returns><see langword="false"/> when a size or offset overflows.</returns>
        internal bool TryGetParameters(Jbig2BitmapView reference, Jbig2TextRegionSettings settings, out Size size, out Jbig2RefinementParameters parameters)
        {
            var width = (long)reference.Width + DeltaWidth;
            var height = (long)reference.Height + DeltaHeight;
            var dx = (long)(DeltaWidth >> 1) + OffsetX;
            var dy = (long)(DeltaHeight >> 1) + OffsetY;
            parameters = new(settings.RefinementTemplate, false, (int)dx, (int)dy, settings.RefinementAt);
            size = Jbig2Limits.IsValidSize(width, height) ? new((int)width, (int)height) : default;
            return width is >= 0 and <= uint.MaxValue && height is >= 0 and <= uint.MaxValue && TryFitInt(dx, out _) && TryFitInt(dy, out _);
        }
    }
}
