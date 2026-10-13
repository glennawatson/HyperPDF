// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Bounds that keep damaged or hostile JBIG2 data from exhausting memory or time. T.88 stores sizes and counts in
/// 32-bit fields, so the standard alone does not bound memory; each limit below is our own, with the reason beside it.
/// </summary>
internal static class Jbig2Limits
{
    /// <summary>
    /// The widest or tallest region, symbol or pattern decoded: 32,768 pixels. Our limit, chosen because a 1-bit page
    /// of that width is 4 KiB a row, which a row buffer holds comfortably, and it covers an A0 sheet scanned at 600 dpi
    /// (about 19,900 by 28,100 pixels) with room to spare.
    /// </summary>
    internal const int MaxImageSide = 1 << 15;

    /// <summary>The most pixels in one region, symbol or collective bitmap, which is the shared image pixel budget.</summary>
    internal const long MaxRegionPixels = ImageHeader.MaxPixels;

    /// <summary>
    /// The most symbols a symbol dictionary may define: 2,097,152 (2^21). Our limit, chosen because each symbol needs a
    /// bitmap record of about 32 bytes, so the records alone stay near 64 MiB.
    /// </summary>
    internal const uint MaxNewSymbols = 1U << 21;

    /// <summary>
    /// The most symbols a symbol dictionary may export: 4,194,304 (2^22). Our limit, chosen because an export is at most
    /// the imported symbols plus the new ones (T.88 section 6.5.10), and each is one 8-byte reference.
    /// </summary>
    internal const uint MaxExportSymbols = 1U << 22;

    /// <summary>
    /// The highest pattern index (GRAYMAX) a pattern dictionary may declare. Our limit, derived from
    /// <see cref="MaxImageSide"/>: the collective bitmap holds GRAYMAX + 1 patterns at least one pixel wide each.
    /// </summary>
    internal const uint MaxPatternIndex = MaxImageSide - 1;

    /// <summary>
    /// The most text region instances per byte of segment data: 64. Our limit, chosen because Huffman coding needs at
    /// least one bit an instance (8 a byte), and arithmetic coding of a very predictable page can reach several times
    /// that, so we allow eight times the Huffman bound. The work budget below still bounds the time spent.
    /// </summary>
    internal const int InstancesPerByte = 64;

    /// <summary>The most pixels decoded or composited for one stream, which bounds the time spent on hostile data (twice the pixel budget).</summary>
    internal const long MaxWork = ImageHeader.MaxPixels * 2;

    /// <summary>The most bytes of symbol and pattern bitmaps one dictionary may hold: one eighth of the pixel budget, as 1-bit pixels.</summary>
    internal const long MaxStoreBytes = ImageHeader.MaxPixels / 8;

    /// <summary>Determines whether a region size can be decoded.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns><see langword="true"/> when both sides are positive, within <see cref="MaxImageSide"/>, and the area is within <see cref="MaxRegionPixels"/>.</returns>
    internal static bool IsValidSize(long width, long height) =>
        width is > 0 and <= MaxImageSide && height is > 0 and <= MaxImageSide && width * height <= MaxRegionPixels;
}
