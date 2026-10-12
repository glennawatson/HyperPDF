// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Ocr;

/// <summary>
/// Gives scanned pages a searchable text layer: renders a page without text at scanning resolution, in strips so a page
/// never needs a full colour bitmap, converts it to greyscale, recognises the words and writes them back as invisible
/// text. Pages that already have text are left alone. The pixel buffers come from the shared pool.
/// </summary>
public static class OcrRunner
{
    /// <summary>The scanning resolution in dots per inch.</summary>
    private const float ScanDpi = 300;

    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72;

    /// <summary>The longest page side rendered, in pixels, so huge pages do not exhaust memory.</summary>
    private const int MaxSide = 8000;

    /// <summary>The rows rendered at a time.</summary>
    private const int StripRows = 256;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The red weight of Rec. 601 luma, out of 256.</summary>
    private const int RedWeight = 77;

    /// <summary>The green weight of Rec. 601 luma, out of 256.</summary>
    private const int GreenWeight = 150;

    /// <summary>The blue weight of Rec. 601 luma, out of 256.</summary>
    private const int BlueWeight = 29;

    /// <summary>The shift dividing the weighted sum by 256.</summary>
    private const int WeightShift = 8;

    /// <summary>The green byte of a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The red byte of a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The pixels in one vector of 32 bit lanes.</summary>
    private const int LanePixels = 8;

    /// <summary>The first pixel of the third vector in a step.</summary>
    private const int ThirdLane = LanePixels * 2;

    /// <summary>The first pixel of the fourth vector in a step.</summary>
    private const int FourthLane = LanePixels * 3;

    /// <summary>The pixels converted per vector step.</summary>
    private const int BlockPixels = LanePixels * 4;

    /// <summary>The bit offset of green in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The bit offset of red in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>Masks one channel in each lane.</summary>
    private static readonly Vector256<uint> ChannelLanes = Vector256.Create(0xFFU);

    /// <summary>Recognises one page and writes its words onto it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">Writes the text layer, normally the same document.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">A reusable list for the words; cleared first.</param>
    /// <returns>What happened.</returns>
    public static OcrPageResult RecognizePage(IDocument document, ITextLayerWriter writer, IOcrEngine engine, int pageIndex, List<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var status = RecognizeWords(document, engine, pageIndex, words);
        return status == OcrPageStatus.Recognized ? WriteWords(writer, pageIndex, words) : new(pageIndex, status, 0);
    }

    /// <summary>Writes recognised words onto a page as invisible text.</summary>
    /// <param name="writer">Writes the text layer.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words.</param>
    /// <returns>What happened.</returns>
    public static OcrPageResult WriteWords(ITextLayerWriter writer, int pageIndex, List<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(words);
        var written = writer.AddTextLayer(pageIndex, CollectionsMarshal.AsSpan(words));
        return new(pageIndex, written > 0 ? OcrPageStatus.Recognized : OcrPageStatus.NoTextFound, written);
    }

    /// <summary>Gets the mean confidence of recognised words, from 0 to 100.</summary>
    /// <param name="words">The words.</param>
    /// <returns>The mean, or 0 when there are none.</returns>
    public static float AverageConfidence(List<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (words.Count == 0)
        {
            return 0;
        }

        var total = 0F;
        foreach (var word in CollectionsMarshal.AsSpan(words))
        {
            total += word.Confidence;
        }

        return total / words.Count;
    }

    /// <summary>Recognises one page's words without writing them, so they can be checked first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">A reusable list receiving the words; cleared first.</param>
    /// <returns><see cref="OcrPageStatus.Recognized"/> when words were found, otherwise why not.</returns>
    public static OcrPageStatus RecognizeWords(IDocument document, IOcrEngine engine, int pageIndex, List<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(words);
        words.Clear();
        if (!engine.IsAvailable)
        {
            return OcrPageStatus.Unavailable;
        }

        if (document.GetCharacterCount(pageIndex) > 0)
        {
            return OcrPageStatus.AlreadyHasText;
        }

        var size = document.GetPageSizes()[pageIndex];
        var scale = GetScale(size);
        var width = Math.Max(1, (int)MathF.Ceiling(size.Width * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(size.Height * scale));
        var grey = ArrayPool<byte>.Shared.Rent(width * height);
        try
        {
            if (!RenderGrey(document, pageIndex, scale, width, height, grey))
            {
                return OcrPageStatus.NotRendered;
            }

            engine.Recognize(grey.AsSpan(0, width * height), width, height, scale, words);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(grey);
        }

        return words.Count == 0 ? OcrPageStatus.NoTextFound : OcrPageStatus.Recognized;
    }

    /// <summary>Converts BGRA pixels to 8 bit luma, 32 pixels at a time where vectors are accelerated.</summary>
    /// <param name="bgra">The pixels, four bytes each.</param>
    /// <param name="grey">The destination, one byte per pixel.</param>
    public static void ToGrey(ReadOnlySpan<byte> bgra, Span<byte> grey)
    {
        var count = Math.Min(bgra.Length / BytesPerPixel, grey.Length);
        var i = 0;
        if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            var pixels = MemoryMarshal.Cast<byte, uint>(bgra);
            for (; i <= count - BlockPixels; i += BlockPixels)
            {
                var low = Vector256.Narrow(Luma(Vector256.Create(pixels.Slice(i, LanePixels))), Luma(Vector256.Create(pixels.Slice(i + LanePixels, LanePixels))));
                var high = Vector256.Narrow(Luma(Vector256.Create(pixels.Slice(i + ThirdLane, LanePixels))), Luma(Vector256.Create(pixels.Slice(i + FourthLane, LanePixels))));
                Vector256.Narrow(low, high).CopyTo(grey.Slice(i, BlockPixels));
            }
        }

        for (; i < count; i++)
        {
            var pixel = bgra.Slice(i * BytesPerPixel, BytesPerPixel);
            grey[i] = (byte)(((pixel[0] * BlueWeight) + (pixel[GreenOffset] * GreenWeight) + (pixel[RedOffset] * RedWeight)) >> WeightShift);
        }
    }

    /// <summary>Computes the luma of eight little endian BGRA pixels.</summary>
    /// <param name="pixels">The pixels as 0xAARRGGBB.</param>
    /// <returns>The luma of each, from 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> Luma(Vector256<uint> pixels) =>
        (((pixels & ChannelLanes) * BlueWeight)
            + (((pixels >> GreenShift) & ChannelLanes) * GreenWeight)
            + (((pixels >> RedShift) & ChannelLanes) * RedWeight)) >> WeightShift;

    /// <summary>Gets the render scale for a page: scanning resolution, reduced for very large pages.</summary>
    /// <param name="size">The page size in points.</param>
    /// <returns>Pixels per point.</returns>
    private static float GetScale(PageSize size)
    {
        const float scale = ScanDpi / PointsPerInch;
        var longest = MathF.Max(size.Width, size.Height) * scale;
        return longest > MaxSide ? scale * MaxSide / longest : scale;
    }

    /// <summary>Renders a page strip by strip into a greyscale buffer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="scale">Pixels per point.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="grey">The destination.</param>
    /// <returns><see langword="false"/> when the page could not be rendered.</returns>
    private static bool RenderGrey(IDocument document, int pageIndex, float scale, int width, int height, byte[] grey)
    {
        var stride = width * BytesPerPixel;
        var strip = ArrayPool<byte>.Shared.Rent(stride * StripRows);
        try
        {
            for (var top = 0; top < height; top += StripRows)
            {
                var rows = Math.Min(StripRows, height - top);
                var target = new RenderTarget(strip.AsSpan(0, stride * rows), width, rows, stride);
                if (!document.Render(new(pageIndex, scale, PageRotation.None, 0, top, RenderFlags.None), target))
                {
                    return false;
                }

                ToGrey(strip.AsSpan(0, stride * rows), grey.AsSpan(top * width, rows * width));
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(strip);
        }
    }
}
