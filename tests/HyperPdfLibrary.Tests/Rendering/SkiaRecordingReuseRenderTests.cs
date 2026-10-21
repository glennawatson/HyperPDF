// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Checks that the recording shortcuts of the Skia device leave the picture's pixels unchanged: a kept dash effect, a pattern
/// shader cache that refills when full, and no recording of rectangles and images wholly outside the clip. Also bounds how far
/// the single-pass thin line may differ from the two-pass line.
/// </summary>
[NotInParallel]
public sealed class SkiaRecordingReuseRenderTests
{
    /// <summary>The page size in points and pixels.</summary>
    private const int Size = 100;

    /// <summary>The strokes and fills drawn by the repeating tests.</summary>
    private const int Repeats = 20;

    /// <summary>More distinct pattern matrices than the shader cache keeps.</summary>
    private const int DistinctMatrices = 100;

    /// <summary>The largest channel difference accepted between a shader drawn in slices and in one fill.</summary>
    private const int SliceTolerance = 2;

    /// <summary>The largest channel difference accepted between a one-pass and a two-pass thin translucent line.</summary>
    private const int ThinLineTolerance = 64;

    /// <summary>The offset that puts content far outside the page.</summary>
    private const int OffPage = 5000;

    /// <summary>The resources value that asks <c>Render</c> to add a small image named Im.</summary>
    private const string ImageResource = "image";

    /// <summary>The shading pattern used by the shader tests: red to blue across the page.</summary>
    private const string GradientPattern =
        "/Pattern << /P1 << /PatternType 2 /Shading << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] "
        + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >> >> >>";

    /// <summary>The distance between the dashed lines, in points.</summary>
    private const int RowPitch = 4;

    /// <summary>The height of the first dashed line, in points.</summary>
    private const int FirstRow = 2;

    /// <summary>The times the pattern slices are drawn by the repeat test.</summary>
    private const int Rounds = 2;

    /// <summary>The distance in pixels from a page corner of the pixel the edge test reads.</summary>
    private const int Inset = 5;

    /// <summary>The largest channel difference accepted for a pixel inside a solid fill.</summary>
    private const int SolidTolerance = 2;

    /// <summary>Setting the dash once for many strokes gives the pixels of setting it before every stroke.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DashSetOnceMatchesDashSetBeforeEveryStroke()
    {
        var once = new StringBuilder("0 G 2 w 1 J [5 3] 1 d\n");
        var each = new StringBuilder("0 G 2 w 1 J\n");
        for (var i = 0; i < Repeats; i++)
        {
            var line = string.Create(CultureInfo.InvariantCulture, $"5 {FirstRow + (i * RowPitch)}.5 m 95 {FirstRow + (i * RowPitch)}.5 l S\n");
            _ = once.Append(line);
            _ = each.Append("[5 3] 1 d ").Append(line);
        }

        var shared = Render(once.ToString(), string.Empty, PdfRenderQuality.Default);
        var rebuilt = Render(each.ToString(), string.Empty, PdfRenderQuality.Default);

        await Assert.That(MaxDifference(shared, rebuilt)).IsEqualTo(0);
    }

    /// <summary>A change of dash phase or scale between strokes is drawn with the new dash, not the kept one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedDashPhaseIsNotServedFromTheKeptEffect()
    {
        var first = Render("4 w [10 10] 0 d 0 50 m 100 50 l S", string.Empty, PdfRenderQuality.Default);
        var shifted = Render("4 w [10 10] 0 d 0 50 m 100 50 l S [10 10] 10 d 0 50 m 100 50 l S", string.Empty, PdfRenderQuality.Default);
        var fresh = Render("4 w [10 10] 10 d 0 50 m 100 50 l S", string.Empty, PdfRenderQuality.Default);

        await Assert.That(MaxDifference(fresh, first)).IsGreaterThan(0);

        // The second stroke fills the gaps of the first, so the page ends up solid where either stroke painted.
        await Assert.That(MaxDifference(shifted, first)).IsGreaterThan(0);
        await Assert.That(Ink(shifted)).IsGreaterThan(Ink(first));
    }

    /// <summary>Rectangles and images wholly outside the page leave the page as if they were not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentOutsideThePageDoesNotChangeThePage()
    {
        const string visible = "q 1 0 0 rg 20 20 30 30 re f Q q 40 0 0 40 50 50 cm /Im Do Q ";
        var outside = string.Create(
            CultureInfo.InvariantCulture,
            $"q 0 1 0 rg {OffPage} {OffPage} 30 30 re f Q q 0 0 1 rg -{OffPage} 10 30 30 re f Q q 40 0 0 40 {OffPage} 10 cm /Im Do Q q 40 0 0 40 -{OffPage} -{OffPage} cm /Im Do Q ");

        var plain = Render(visible, ImageResource, PdfRenderQuality.Default);
        var withOutside = Render(outside + visible, ImageResource, PdfRenderQuality.Default);

        await Assert.That(MaxDifference(withOutside, plain)).IsEqualTo(0);
        await Assert.That(Ink(plain)).IsGreaterThan(0L);
    }

    /// <summary>A rectangle or image that only partly lies on the page still draws the part that does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentStraddlingThePageEdgeStillDraws()
    {
        var image = Render("q 1 0 0 rg -20 -20 40 40 re f Q q 0 0 1 rg 80 80 40 40 re f Q q 60 0 0 60 70 -30 cm /Im Do Q", ImageResource, PdfRenderQuality.Default);

        await Assert.That(image.IsNear(Inset, Size - Inset, Rgb.Red255, SolidTolerance)).IsTrue().Because(image.Describe(Inset, Size - Inset));
        await Assert.That(image.IsNear(Size - Inset, Inset, Rgb.Blue255, SolidTolerance)).IsTrue().Because(image.Describe(Size - Inset, Inset));
        await Assert.That(Ink(image)).IsGreaterThan(0L);
    }

    /// <summary>A translucent, blended rectangle outside the page changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlendedRectangleOutsideThePageDoesNotChangeThePage()
    {
        const string resources = "/ExtGState << /Half << /ca 0.5 /BM /Multiply >> >>";
        const string visible = "/Half gs 1 0 0 rg 20 20 30 30 re f ";
        var outside = string.Create(CultureInfo.InvariantCulture, $"/Half gs 0 1 0 rg {OffPage} {OffPage} 30 30 re f ");

        var plain = Render(visible, resources, PdfRenderQuality.Default);
        var withOutside = Render(outside + visible, resources, PdfRenderQuality.Default);

        await Assert.That(MaxDifference(withOutside, plain)).IsEqualTo(0);
        await Assert.That(Ink(plain)).IsGreaterThan(0L);
    }

    /// <summary>A pattern filled at more distinct matrices than the cache holds draws the same pixels as one fill.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternFilledAtManyMatricesMatchesOneFill()
    {
        var slices = new StringBuilder("/Pattern cs /P1 scn\n");
        for (var i = 0; i < DistinctMatrices; i++)
        {
            _ = slices.Append(CultureInfo.InvariantCulture, $"q 1 0 0 1 {i} 0 cm 0 0 1 100 re f Q\n");
        }

        var sliced = Render(slices.ToString(), GradientPattern, PdfRenderQuality.Default);
        var whole = Render("/Pattern cs /P1 scn 0 0 100 100 re f", GradientPattern, PdfRenderQuality.Default);

        await Assert.That(MaxDifference(sliced, whole)).IsLessThanOrEqualTo(SliceTolerance);
    }

    /// <summary>A repeated key after the cache filled still draws the pattern correctly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternRepeatedAfterTheCacheFilledMatchesOneFill()
    {
        var slices = new StringBuilder("/Pattern cs /P1 scn\n");
        for (var round = 0; round < Rounds; round++)
        {
            for (var i = 0; i < DistinctMatrices; i++)
            {
                _ = slices.Append(CultureInfo.InvariantCulture, $"q 1 0 0 1 {i} 0 cm 0 0 1 100 re f Q\n");
            }
        }

        var sliced = Render(slices.ToString(), GradientPattern, PdfRenderQuality.Default);
        var whole = Render("/Pattern cs /P1 scn 0 0 100 100 re f", GradientPattern, PdfRenderQuality.Default);

        await Assert.That(MaxDifference(sliced, whole)).IsLessThanOrEqualTo(SliceTolerance);
    }

    /// <summary>
    /// With the page matrix known, a translucent line thinner than a pixel is drawn once, and stays within a bound of the
    /// line drawn as body plus hairline in a layer when the matrix is unknown.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThinTranslucentLineDrawnOnceStaysNearTheTwoPassLine()
    {
        const string content = "/Half gs 0 G 0.4 w 10 20.3 m 90 20.3 l S 0.2 w 10 40.6 m 90 55.1 l S";
        const string resources = "/ExtGState << /Half << /CA 0.5 >> >>";

        var twoPass = Render(content, resources, PdfRenderQuality.Default);
        var onePass = Render(content, resources, PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Document });

        await Assert.That(MaxDifference(onePass, twoPass)).IsLessThanOrEqualTo(ThinLineTolerance);
        await Assert.That(Ink(onePass)).IsGreaterThan(0L);
    }

    /// <summary>A line of a pixel or more is drawn the same with or without a known page matrix.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineOfAPixelOrMoreIsUnchangedByAKnownPageMatrix()
    {
        const string content = "0 G 1.5 w 10 20.3 m 90 20.3 l S 3 w 10 40.6 m 90 55.1 l S";

        var unknown = Render(content, string.Empty, PdfRenderQuality.Default);
        var known = Render(content, string.Empty, PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Document });

        await Assert.That(MaxDifference(known, unknown)).IsEqualTo(0);
    }

    /// <summary>Renders the first page of a document built from content and resources.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="resources">The resource entries; <see cref="ImageResource"/> adds the image named Im.</param>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, string resources, PdfRenderQuality quality)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        if (resources == ImageResource)
        {
            var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode", "00FFFF00>");
            resources = $"/XObject << /Im {image} 0 R >>";
        }

        pdf.Resources = resources;
        using var page = new RenderTestPage(pdf.ToBytes());
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel));
        return new(pixels, Size, Size);
    }

    /// <summary>Sums the darkness of the first channel over the page.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The ink.</returns>
    private static long Ink(RenderedImage image)
    {
        long total = 0;
        for (var i = 0; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            total += byte.MaxValue - image.Pixels[i];
        }

        return total;
    }

    /// <summary>Gets the largest difference in any channel between two images of one size.</summary>
    /// <param name="left">The first image.</param>
    /// <param name="right">The second image.</param>
    /// <returns>The largest channel difference.</returns>
    private static int MaxDifference(RenderedImage left, RenderedImage right)
    {
        var largest = 0;
        for (var i = 0; i < left.Pixels.Length; i++)
        {
            largest = Math.Max(largest, Math.Abs(left.Pixels[i] - right.Pixels[i]));
        }

        return largest;
    }
}
