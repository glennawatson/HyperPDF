// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdf.Ocr.Layout;

/// <summary>A page reduced to ink and paper: one byte for each pixel, 1 where there is ink.</summary>
[DebuggerDisplay("InkMask: {Width} by {Height}")]
public sealed class InkMask
{
    /// <summary>The number of grey levels.</summary>
    private const int Levels = 256;

    /// <summary>The ink byte.</summary>
    private const byte Ink = 1;

    /// <summary>The number of pixels counted together.</summary>
    private const int CountLanes = 4;

    /// <summary>The place of the third pixel counted together.</summary>
    private const int ThirdLane = 2;

    /// <summary>The place of the fourth pixel counted together.</summary>
    private const int FourthLane = 3;

    /// <summary>The pixels, row after row, 1 for ink. The array is kept between pages and may be longer than the page.</summary>
    private byte[] _pixels = [];

    /// <summary>Initializes a new instance of the <see cref="InkMask"/> class, empty.</summary>
    internal InkMask()
    {
    }

    /// <summary>Gets a value indicating whether the ink is darker than the paper, which is how a page is usually printed.</summary>
    public bool InkIsDark { get; private set; }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Gets the pixels, row after row, 1 for ink.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels.AsSpan(0, Width * Height);

    /// <summary>
    /// Separates ink from paper. The level between the two is the one that best splits the page's grey levels in two,
    /// and the side with fewer pixels is the ink, so light text on a dark page works as well as dark text on a light one.
    /// The mask is the scratch memory's own, so it is valid until the next page is built.
    /// </summary>
    /// <param name="grey">The pixels of the page, one byte each.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="scratch">The working memory that owns the mask.</param>
    /// <returns>The mask.</returns>
    public static InkMask Build(ReadOnlySpan<byte> grey, int width, int height, RecognitionScratch scratch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(grey.Length, width * height);
        ArgumentNullException.ThrowIfNull(scratch);
        Span<int> histogram = stackalloc int[Levels];
        var count = width * height;
        Count(grey[..count], histogram);
        var threshold = OtsuLevel(histogram, count);
        var dark = 0;
        for (var level = 0; level <= threshold; level++)
        {
            dark += histogram[level];
        }

        var mask = scratch.Ink;
        mask.Fill(grey[..count], width, height, (byte)threshold, dark <= count - dark);
        return mask;
    }

    /// <summary>Finds the box around the ink in an area.</summary>
    /// <param name="area">The area to search, in pixels; the parts outside the page are ignored.</param>
    /// <param name="extent">Receives the box around the ink.</param>
    /// <returns><see langword="true"/> when the area has any ink.</returns>
    public bool TryGetExtent(TextLine area, out TextLine extent)
    {
        var left = Math.Max(0, area.Left);
        var right = Math.Min(Width - 1, area.Right);
        var top = Math.Max(0, area.Top);
        var bottom = Math.Min(Height - 1, area.Bottom);
        var found = new TextLine(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        for (var y = top; y <= bottom; y++)
        {
            var row = _pixels.AsSpan((y * Width) + left, Math.Max(0, right - left + 1));
            var first = row.IndexOf(Ink);
            if (first < 0)
            {
                continue;
            }

            found = new(
                Math.Min(found.Left, left + first),
                Math.Min(found.Top, y),
                Math.Max(found.Right, left + row.LastIndexOf(Ink)),
                y);
        }

        extent = found;
        return found.Left != int.MaxValue;
    }

    /// <summary>Marks the pixels of a page as ink or paper.</summary>
    /// <param name="grey">The pixels of the page.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="threshold">The highest level of the darker side.</param>
    /// <param name="darkIsInk">Whether the darker side is the ink.</param>
    internal void Fill(ReadOnlySpan<byte> grey, int width, int height, byte threshold, bool darkIsInk)
    {
        if (_pixels.Length < grey.Length)
        {
            _pixels = new byte[grey.Length];
        }

        Width = width;
        Height = height;
        InkIsDark = darkIsInk;
        Threshold(grey, _pixels, threshold, darkIsInk);
    }

    /// <summary>
    /// Counts the pixels at each grey level. Neighbouring pixels are often the same level, so counting them into one
    /// table makes each wait for the last to be stored; four tables count four pixels at once.
    /// </summary>
    /// <param name="grey">The pixels.</param>
    /// <param name="histogram">Receives the count at each level.</param>
    private static void Count(ReadOnlySpan<byte> grey, Span<int> histogram)
    {
        Span<int> second = stackalloc int[Levels];
        Span<int> third = stackalloc int[Levels];
        Span<int> fourth = stackalloc int[Levels];
        var i = 0;
        for (; i + CountLanes <= grey.Length; i += CountLanes)
        {
            histogram[grey[i]]++;
            second[grey[i + 1]]++;
            third[grey[i + ThirdLane]]++;
            fourth[grey[i + FourthLane]]++;
        }

        for (; i < grey.Length; i++)
        {
            histogram[grey[i]]++;
        }

        for (var level = 0; level < Levels; level++)
        {
            histogram[level] += second[level] + third[level] + fourth[level];
        }
    }

    /// <summary>Marks each pixel as ink or paper, a vector of pixels at a time.</summary>
    /// <param name="grey">The pixels.</param>
    /// <param name="mask">Receives 1 for ink and 0 for paper.</param>
    /// <param name="threshold">The highest level of the darker side.</param>
    /// <param name="darkIsInk">Whether the darker side is the ink.</param>
    private static void Threshold(ReadOnlySpan<byte> grey, Span<byte> mask, byte threshold, bool darkIsInk)
    {
        var width = Vector<byte>.Count;
        var i = 0;
        if (grey.Length >= width)
        {
            var limit = Vector.Create(threshold);
            var one = Vector.Create(Ink);
            for (; i + width <= grey.Length; i += width)
            {
                var levels = Vector.Create(grey[i..]);
                var marks = darkIsInk ? Vector.LessThanOrEqual(levels, limit) : Vector.GreaterThan(levels, limit);
                (marks & one).CopyTo(mask[i..]);
            }
        }

        for (; i < grey.Length; i++)
        {
            mask[i] = (grey[i] <= threshold) == darkIsInk ? Ink : (byte)0;
        }
    }

    /// <summary>Finds the grey level that best splits a histogram in two, by maximising the variance between the two sides.</summary>
    /// <param name="histogram">The number of pixels at each level.</param>
    /// <param name="total">The number of pixels.</param>
    /// <returns>The highest level of the lower side.</returns>
    private static int OtsuLevel(ReadOnlySpan<int> histogram, int total)
    {
        double sumAll = 0;
        for (var level = 0; level < histogram.Length; level++)
        {
            sumAll += (double)level * histogram[level];
        }

        double sumBelow = 0;
        var below = 0;
        var best = 0;
        var bestVariance = -1.0;
        for (var level = 0; level < histogram.Length; level++)
        {
            below += histogram[level];
            if (below == 0)
            {
                continue;
            }

            var above = total - below;
            if (above == 0)
            {
                break;
            }

            sumBelow += (double)level * histogram[level];
            var difference = (sumBelow / below) - ((sumAll - sumBelow) / above);
            var variance = (double)below * above * difference * difference;
            if (variance <= bestVariance)
            {
                continue;
            }

            bestVariance = variance;
            best = level;
        }

        return best;
    }
}
