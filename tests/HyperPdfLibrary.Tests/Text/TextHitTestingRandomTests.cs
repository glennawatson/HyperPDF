// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the single-pass hit test against the original two-pass search on random pages and points.</summary>
public sealed class TextHitTestingRandomTests
{
    /// <summary>The fixed seed, so a failure repeats.</summary>
    private const int Seed = 20_260_521;

    /// <summary>The number of pages checked.</summary>
    private const int PageCount = 12;

    /// <summary>The number of points tried on each page.</summary>
    private const int PointsPerPage = 400;

    /// <summary>The page width and height used for random boxes.</summary>
    private const float Extent = 600F;

    /// <summary>The largest box side.</summary>
    private const float MaxSide = 14F;

    /// <summary>The margin around the page where points may fall.</summary>
    private const float Margin = 80F;

    /// <summary>The largest tolerance.</summary>
    private const float MaxTolerance = 8F;

    /// <summary>The character code of the test glyph.</summary>
    private const int CharacterCode = (int)'A';

    /// <summary>The nominal font size of the test glyph.</summary>
    private const float FontSize = 10F;

    /// <summary>Every this many points have no horizontal tolerance.</summary>
    private const int NoToleranceXEvery = 3;

    /// <summary>Every this many points have no vertical tolerance.</summary>
    private const int NoToleranceYEvery = 4;

    /// <summary>Every this many characters is a generated one.</summary>
    private const int GeneratedEvery = 17;

    /// <summary>The page sizes, in characters, from below the index threshold to dense.</summary>
    private static readonly int[] PageSizes = [3, 90, 130, 700, 5000];

    /// <summary>Random pages and points give the same character as the original search.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RandomPointsMatchOriginalSearch()
    {
        var random = new Lcg(Seed);
        for (var pageNumber = 0; pageNumber < PageCount; pageNumber++)
        {
            var chars = CreateRandomPage(random, PageSizes[pageNumber % PageSizes.Length]);
            var blocks = TextHitTesting.BuildBlocks(chars);
            for (var p = 0; p < PointsPerPage; p++)
            {
                var point = new Vector2(Next(random, -Margin, Extent + Margin), Next(random, -Margin, Extent + Margin));
                var toleranceX = p % NoToleranceXEvery == 0 ? 0 : Next(random, -1, MaxTolerance);
                var toleranceY = p % NoToleranceYEvery == 0 ? 0 : Next(random, -1, MaxTolerance);
                var expected = Original(chars, point, toleranceX, toleranceY);
                await Assert.That(TextHitTesting.Find(chars, blocks, point, toleranceX, toleranceY)).IsEqualTo(expected);
            }
        }
    }

    /// <summary>Gets a random float in a range.</summary>
    /// <param name="random">The generator.</param>
    /// <param name="low">The lower end.</param>
    /// <param name="high">The upper end.</param>
    /// <returns>The value.</returns>
    private static float Next(Lcg random, float low, float high) => low + (random.NextSingle() * (high - low));

    /// <summary>Creates characters with random, overlapping boxes, some of them generated.</summary>
    /// <param name="random">The generator.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The characters.</returns>
    private static PdfTextChar[] CreateRandomPage(Lcg random, int count)
    {
        var chars = new PdfTextChar[count];
        for (var i = 0; i < count; i++)
        {
            var x = Next(random, 0, Extent);
            var y = Next(random, 0, Extent);
            var box = new PdfRectangle(x, y, x + Next(random, 0, MaxSide), y + Next(random, 0, MaxSide));
            var kind = i % GeneratedEvery == 0 ? PdfTextCharKind.Generated : PdfTextCharKind.Normal;
            chars[i] = new('A', kind, CharacterCode, new(x, y), box, box, FontSize, 0, false);
        }

        return chars;
    }

    /// <summary>Runs the original two-pass search: the first exact hit, otherwise the nearest character within reach.</summary>
    /// <param name="chars">The characters.</param>
    /// <param name="point">The point.</param>
    /// <param name="toleranceX">The horizontal tolerance.</param>
    /// <param name="toleranceY">The vertical tolerance.</param>
    /// <returns>The index, or -1.</returns>
    private static int Original(ReadOnlySpan<PdfTextChar> chars, Vector2 point, float toleranceX, float toleranceY)
    {
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i].Kind != PdfTextCharKind.Generated && TextGeometry.Contains(chars[i].Box, point))
            {
                return i;
            }
        }

        var reachX = Math.Max(toleranceX, 0);
        var reachY = Math.Max(toleranceY, 0);
        return reachX <= 0 && reachY <= 0 ? -1 : OriginalNearest(chars, point, reachX, reachY);
    }

    /// <summary>Runs the original nearest-character pass.</summary>
    /// <param name="chars">The characters.</param>
    /// <param name="point">The point.</param>
    /// <param name="reachX">The horizontal reach.</param>
    /// <param name="reachY">The vertical reach.</param>
    /// <returns>The index, or -1.</returns>
    private static int OriginalNearest(ReadOnlySpan<PdfTextChar> chars, Vector2 point, float reachX, float reachY)
    {
        var nearest = -1;
        var best = double.PositiveInfinity;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i].Kind == PdfTextCharKind.Generated)
            {
                continue;
            }

            var box = TextGeometry.Normalize(chars[i].Box);
            var dx = MathF.Max(MathF.Max(box.Left - point.X, point.X - box.Right), 0);
            var dy = MathF.Max(MathF.Max(box.Bottom - point.Y, point.Y - box.Top), 0);
            var squared = ((double)dx * dx) + ((double)dy * dy);
            if (!(dx <= reachX && dy <= reachY) || squared >= best)
            {
                continue;
            }

            best = squared;
            nearest = i;
        }

        return nearest;
    }

    /// <summary>A small repeatable generator, so a failure always repeats.</summary>
    private sealed class Lcg
    {
        /// <summary>The multiplier of the generator.</summary>
        private const ulong Multiplier = 6_364_136_223_846_793_005UL;

        /// <summary>The increment of the generator.</summary>
        private const ulong Increment = 1_442_695_040_888_963_407UL;

        /// <summary>The shift that keeps the high 24 bits of the state.</summary>
        private const int HighBitsShift = 40;

        /// <summary>The number of values in 24 bits.</summary>
        private const float Range = 16_777_216F;

        /// <summary>The state.</summary>
        private ulong _state;

        /// <summary>Initializes a new instance of the <see cref="Lcg"/> class.</summary>
        /// <param name="seed">The seed.</param>
        public Lcg(int seed) => _state = (ulong)seed;

        /// <summary>Gets the next value.</summary>
        /// <returns>A value from 0 up to but not including 1.</returns>
        public float NextSingle()
        {
            _state = unchecked((_state * Multiplier) + Increment);
            return (_state >> HighBitsShift) / Range;
        }
    }
}
