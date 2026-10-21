// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks that the fit extents cache, shared row starts and layout reuse give the same results as computing everything again.</summary>
public sealed class LayoutResizeReuseTests
{
    /// <summary>The seed of the pseudo-random page sizes.</summary>
    private const uint Seed = 20_260_601;

    /// <summary>How many random documents each differential test tries.</summary>
    private const int Rounds = 300;

    /// <summary>The most pages in a random document.</summary>
    private const int MaxPages = 40;

    /// <summary>The smallest random page edge in points.</summary>
    private const int MinEdge = 100;

    /// <summary>The range of random page edges in points.</summary>
    private const int EdgeRange = 800;

    /// <summary>The number of rotations.</summary>
    private const int Rotations = 4;

    /// <summary>The number of page arrangements, zoom modes and scroll modes.</summary>
    private const int Modes = 3;

    /// <summary>The number of pages in a two page spread.</summary>
    private const double SpreadPages = 2.0;

    /// <summary>The number of sides a margin applies to.</summary>
    private const double Sides = 2.0;

    /// <summary>The smallest random viewport edge.</summary>
    private const int MinViewport = 300;

    /// <summary>The range of random viewport edges.</summary>
    private const int ViewportRange = 1500;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 12;

    /// <summary>The margin around the pages.</summary>
    private const double Margin = 16;

    /// <summary>The range of random scales.</summary>
    private const double ScaleRange = 3.0;

    /// <summary>The smallest random scale.</summary>
    private const double MinScale = 0.2;

    /// <summary>The viewport height of a height-only change.</summary>
    private const double TallerViewport = 1111;

    /// <summary>The first test viewport width.</summary>
    private const double ViewportWidth = 900;

    /// <summary>The first test viewport height.</summary>
    private const double ViewportHeight = 700;

    /// <summary>The width of a small test page.</summary>
    private const float SmallWidth = 400;

    /// <summary>The height of a small test page.</summary>
    private const float SmallHeight = 500;

    /// <summary>The width of a very wide test page.</summary>
    private const float WideWidth = 2000;

    /// <summary>A letter page.</summary>
    private static readonly PageSize Letter = new(612, 792);

    /// <summary>Verifies fit zoom from cached extents is bit for bit the zoom of the full walk.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitZoomFromExtentsEqualsFullWalk()
    {
        var random = new Sequence(Seed);
        for (var round = 0; round < Rounds; round++)
        {
            var sizes = CreateSizes(random);
            var parameters = new FitParameters(
                (PageRotation)random.Next(Rotations),
                (PageLayoutMode)random.Next(Modes),
                (ZoomMode)random.Next(Modes),
                MinViewport + random.Next(ViewportRange),
                MinViewport + random.Next(ViewportRange),
                Spacing,
                Margin);

            var expected = ReferenceFitZoom(sizes, parameters);
            var extents = ZoomCalculator.GetFitExtents(sizes, parameters.Rotation, parameters.Mode);

            await Assert.That(ZoomCalculator.GetFitZoom(sizes, parameters)).IsEqualTo(expected);
            await Assert.That(ZoomCalculator.GetFitZoom(extents, parameters)).IsEqualTo(expected);
        }
    }

    /// <summary>Verifies a layout built from an earlier one equals a layout built from scratch.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutFromPreviousEqualsFreshLayout()
    {
        var random = new Sequence(Seed);
        for (var round = 0; round < Rounds; round++)
        {
            var sizes = CreateSizes(random);
            var previous = DocumentLayout.Create(sizes, CreateOptions(random));
            var options = CreateOptions(random);

            await Assert.That(AreSame(DocumentLayout.Create(sizes, options), DocumentLayout.Create(sizes, options, previous))).IsTrue();
        }
    }

    /// <summary>Verifies a layout built from an earlier one with a different page count is still right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutFromPreviousWithOtherPageCountEqualsFreshLayout()
    {
        var random = new Sequence(Seed);
        for (var round = 0; round < Rounds; round++)
        {
            var previous = DocumentLayout.Create(CreateSizes(random), CreateOptions(random));
            var sizes = CreateSizes(random);
            var options = CreateOptions(random);

            await Assert.That(AreSame(DocumentLayout.Create(sizes, options), DocumentLayout.Create(sizes, options, previous))).IsTrue();
        }
    }

    /// <summary>Verifies that when a layout says it matches new options, computing the layout again changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchingLayoutEqualsFreshLayout()
    {
        var random = new Sequence(Seed);
        var matched = 0;
        for (var round = 0; round < Rounds; round++)
        {
            var sizes = CreateSizes(random);
            var options = CreateOptions(random);
            var layout = DocumentLayout.Create(sizes, options);
            var next = random.NextBool()
                ? options with { ViewportHeight = MinViewport + random.Next(ViewportRange) }
                : CreateOptions(random);

            if (!layout.Matches(next))
            {
                continue;
            }

            matched++;
            await Assert.That(HaveSameGeometry(layout, DocumentLayout.Create(sizes, next))).IsTrue();
        }

        await Assert.That(matched).IsGreaterThan(0);
    }

    /// <summary>Verifies a height-only change keeps a continuous layout but not a page by page one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeightChangeMatchesOnlyOutsidePageByPage()
    {
        PageSize[] sizes = [Letter, Letter, Letter];
        var continuous = new LayoutOptions(PageRotation.None, PageLayoutMode.Single, 1, Spacing, Margin, ViewportWidth) { ViewportHeight = ViewportHeight };
        var slotted = continuous with { PageByPage = true };

        await Assert.That(DocumentLayout.Create(sizes, continuous).Matches(continuous with { ViewportHeight = TallerViewport })).IsTrue();
        await Assert.That(DocumentLayout.Create(sizes, slotted).Matches(slotted with { ViewportHeight = TallerViewport })).IsFalse();
        await Assert.That(DocumentLayout.Create(sizes, continuous).Matches(continuous with { ViewportWidth = ViewportWidth + 1 })).IsFalse();
    }

    /// <summary>Verifies the cache returns the same measurements for the same pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheReusesExtentsForSamePages()
    {
        PageSize[] sizes = [Letter, new(SmallWidth, SmallHeight)];
        var cache = new PageFitExtentsCache();

        var first = cache.Get(sizes, PageRotation.None, PageLayoutMode.Single);

        await Assert.That(cache.Get(sizes, PageRotation.None, PageLayoutMode.Single)).IsSameReferenceAs(first);
    }

    /// <summary>Verifies the cache measures again for a rotation, an arrangement or a different page array.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheMeasuresAgainWhenInputsChange()
    {
        PageSize[] sizes = [Letter, new(SmallWidth, SmallHeight)];
        var cache = new PageFitExtentsCache();
        var upright = cache.Get(sizes, PageRotation.None, PageLayoutMode.Single);

        var turned = cache.Get(sizes, PageRotation.Rotate90, PageLayoutMode.Single);
        var spread = cache.Get(sizes, PageRotation.Rotate90, PageLayoutMode.Dual);
        var other = cache.Get([.. sizes], PageRotation.Rotate90, PageLayoutMode.Dual);

        await Assert.That(turned).IsNotSameReferenceAs(upright);
        await Assert.That(turned.WidestPage).IsEqualTo(Letter.Height);
        await Assert.That(spread.WidestSpread).IsEqualTo(Letter.Height + SmallHeight);
        await Assert.That(other).IsNotSameReferenceAs(spread);
        await Assert.That(other).IsEqualTo(spread);
    }

    /// <summary>Verifies invalidating the cache picks up a page that changed size in the same array.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheMeasuresAgainAfterInvalidate()
    {
        PageSize[] sizes = [Letter, new(SmallWidth, SmallHeight)];
        var cache = new PageFitExtentsCache();
        var before = cache.Get(sizes, PageRotation.None, PageLayoutMode.Single);

        sizes[1] = new(WideWidth, SmallHeight);
        await Assert.That(cache.Get(sizes, PageRotation.None, PageLayoutMode.Single)).IsSameReferenceAs(before);

        cache.Invalidate();
        var after = cache.Get(sizes, PageRotation.None, PageLayoutMode.Single);

        await Assert.That(after.WidestPage).IsEqualTo(WideWidth);
    }

    /// <summary>Verifies the cache measures again when pages are added to a new array of another length.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheMeasuresAgainForMorePages()
    {
        var cache = new PageFitExtentsCache();
        var before = cache.Get([Letter], PageRotation.None, PageLayoutMode.Single);

        var after = cache.Get([Letter, new(WideWidth, SmallHeight)], PageRotation.None, PageLayoutMode.Single);

        await Assert.That(after.PageCount).IsEqualTo(Modes - 1);
        await Assert.That(after.WidestPage).IsGreaterThan(before.WidestPage);
    }

    /// <summary>Creates random options covering every arrangement and scroll mode.</summary>
    /// <param name="random">The random source.</param>
    /// <returns>The options.</returns>
    private static LayoutOptions CreateOptions(Sequence random) =>
        new((PageRotation)random.Next(Rotations), (PageLayoutMode)random.Next(Modes), MinScale + (random.NextFraction() * ScaleRange), Spacing, Margin, MinViewport + random.Next(ViewportRange))
        {
            PageByPage = random.NextBool(),
            ViewportHeight = MinViewport + random.Next(ViewportRange),
            RightToLeft = random.NextBool(),
            Scroll = (ScrollMode)random.Next(Modes),
        };

    /// <summary>Creates random mixed page sizes.</summary>
    /// <param name="random">The random source.</param>
    /// <returns>The sizes.</returns>
    private static PageSize[] CreateSizes(Sequence random)
    {
        var sizes = new PageSize[1 + random.Next(MaxPages)];
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = new(MinEdge + random.Next(EdgeRange), MinEdge + random.Next(EdgeRange));
        }

        return sizes;
    }

    /// <summary>Determines whether two layouts hold exactly the same numbers.</summary>
    /// <param name="a">One layout.</param>
    /// <param name="b">The other layout.</param>
    /// <returns><see langword="true"/> when every extent, row and page rectangle is equal.</returns>
    private static bool AreSame(DocumentLayout a, DocumentLayout b) => a.Options == b.Options && HaveSameGeometry(a, b);

    /// <summary>Determines whether two layouts put every page, row and extent in exactly the same place, whatever options they record.</summary>
    /// <param name="a">One layout.</param>
    /// <param name="b">The other layout.</param>
    /// <returns><see langword="true"/> when every extent, row and page rectangle is equal.</returns>
    private static bool HaveSameGeometry(DocumentLayout a, DocumentLayout b)
    {
        if (a.PageCount != b.PageCount || a.RowCount != b.RowCount || !SameBits(a.ExtentWidth, b.ExtentWidth) || !SameBits(a.ExtentHeight, b.ExtentHeight))
        {
            return false;
        }

        for (var page = 0; page < a.PageCount; page++)
        {
            a.GetRowExtent(page, out var topA, out var bottomA);
            b.GetRowExtent(page, out var topB, out var bottomB);
            if (a.GetPageBounds(page) != b.GetPageBounds(page) || !SameBits(topA, topB) || !SameBits(bottomA, bottomB))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether two numbers have exactly the same bits, which is the standard for layouts that must not differ at all.</summary>
    /// <param name="a">One number.</param>
    /// <param name="b">The other number.</param>
    /// <returns><see langword="true"/> when the bits are equal.</returns>
    private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    /// <summary>The fit zoom as it was computed before the page measurements were cached: one walk over every row.</summary>
    /// <param name="sizes">The page sizes.</param>
    /// <param name="parameters">The viewport and arrangement.</param>
    /// <returns>The zoom.</returns>
    private static double ReferenceFitZoom(PageSize[] sizes, in FitParameters parameters)
    {
        if (parameters.ZoomMode == ZoomMode.Free || sizes.Length == 0 || parameters.ViewportWidth <= 0 || parameters.ViewportHeight <= 0)
        {
            return 1.0;
        }

        var widestPage = 0.0;
        var widestSpread = 0.0;
        var tallest = 0.0;
        var rows = PageRows.GetRowCount(sizes.Length, parameters.Mode);
        for (var row = 0; row < rows; row++)
        {
            PageRows.GetRowPages(row, sizes.Length, parameters.Mode, out var first, out var count);
            var width = 0.0;
            var height = 0.0;
            for (var i = first; i < first + count; i++)
            {
                var rotated = sizes[i].Rotate(parameters.Rotation);
                width += rotated.Width;
                height = Math.Max(height, rotated.Height);
            }

            tallest = Math.Max(tallest, height);
            if (PageRows.IsCoverRow(row, parameters.Mode))
            {
                widestSpread = Math.Max(widestSpread, width * SpreadPages);
            }
            else if (count == 1)
            {
                widestPage = Math.Max(widestPage, width);
            }
            else
            {
                widestSpread = Math.Max(widestSpread, width);
            }
        }

        var margins = Sides * parameters.Margin;
        var scale = Math.Min(
            Math.Max(1, parameters.ViewportWidth - margins) / widestPage,
            Math.Max(1, parameters.ViewportWidth - margins - parameters.Spacing) / widestSpread);
        if (parameters.ZoomMode == ZoomMode.FitPage)
        {
            scale = Math.Min(scale, Math.Max(1, parameters.ViewportHeight - margins) / tallest);
        }

        return ZoomCalculator.Clamp(scale / ZoomCalculator.PixelsPerPoint);
    }

    /// <summary>A repeatable pseudo-random sequence (xorshift32) that gives the same numbers on every platform. It is not for security.</summary>
    private sealed class Sequence
    {
        /// <summary>The first state of the pseudo-random generator, never zero.</summary>
        private const uint FallbackState = 1;

        /// <summary>The first xorshift shift.</summary>
        private const int ShiftA = 13;

        /// <summary>The second xorshift shift.</summary>
        private const int ShiftB = 17;

        /// <summary>The third xorshift shift.</summary>
        private const int ShiftC = 5;

        /// <summary>The number of values the generator gives for a fraction.</summary>
        private const int FractionSteps = 1_000_000;

        /// <summary>The state, never zero.</summary>
        private uint _state;

        /// <summary>Initializes a new instance of the <see cref="Sequence"/> class.</summary>
        /// <param name="seed">The seed; zero is replaced by one.</param>
        internal Sequence(uint seed) => _state = seed == 0 ? FallbackState : seed;

        /// <summary>Gets the next number below a limit.</summary>
        /// <param name="limit">The exclusive upper limit, above zero.</param>
        /// <returns>A number from 0 to <paramref name="limit"/> minus one.</returns>
        internal int Next(int limit)
        {
            _state ^= _state << ShiftA;
            _state ^= _state >> ShiftB;
            _state ^= _state << ShiftC;
            return (int)(_state % (uint)limit);
        }

        /// <summary>Gets the next true or false.</summary>
        /// <returns>A bool.</returns>
        internal bool NextBool() => Next(Modes - 1) == 0;

        /// <summary>Gets the next fraction from 0 up to but not including 1.</summary>
        /// <returns>The fraction.</returns>
        internal double NextFraction() => (double)Next(FractionSteps) / FractionSteps;
    }
}
