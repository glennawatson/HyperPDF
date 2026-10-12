// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Detection;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Tests.Forms;

/// <summary>Tests finding places to write on a flat form from its pixels, drawn here at two pixels per point.</summary>
[NotInParallel]
public sealed class FormRegionDetectorTests
{
    /// <summary>Pixels per point.</summary>
    private const float Scale = 2;

    /// <summary>The page width in points.</summary>
    private const int PageWidth = 400;

    /// <summary>The page height in points.</summary>
    private const int PageHeight = 500;

    /// <summary>How close edges must be, in points.</summary>
    private const float Tolerance = 2;

    /// <summary>The boxes of each comb.</summary>
    private const int CombCells = 6;

    /// <summary>The places the test form has: a line, a box, a comb box and a ticked comb.</summary>
    private const int Places = 4;

    /// <summary>The luminance of pure green.</summary>
    private const byte GreenLuma = 149;

    /// <summary>The combs drawn.</summary>
    private const int TwoCombs = 2;

    /// <summary>How far the ticks rise above their line, in points.</summary>
    private const float TickRise = 5;

    /// <summary>How close places on a scanned page must be, in points.</summary>
    private const float ScanTolerance = 4;

    /// <summary>The seed of the scanner grain, so the test is repeatable.</summary>
    private const uint ScanSeed = 0x9E3779B9;

    /// <summary>The first xorshift step.</summary>
    private const int XorLeft = 13;

    /// <summary>The second xorshift step.</summary>
    private const int XorRight = 17;

    /// <summary>The third xorshift step.</summary>
    private const int XorLast = 5;

    /// <summary>The grain levels, from minus <see cref="Grain"/> to plus it.</summary>
    private const uint GrainSteps = (Grain * 2) + 1;

    /// <summary>The grey of faded ink.</summary>
    private const byte FadedInk = 90;

    /// <summary>The grey of off-white paper.</summary>
    private const byte PaperTone = 228;

    /// <summary>How far grain moves a pixel's grey either way.</summary>
    private const int Grain = 24;

    /// <summary>The pixels averaged by the blur.</summary>
    private const int BlurArea = 9;

    /// <summary>Half a turn in degrees.</summary>
    private const float HalfTurnDegrees = 180;

    /// <summary>A line to write on.</summary>
    private static readonly PageRect Line = new(50, 100, 200, 1);

    /// <summary>A box to write in.</summary>
    private static readonly PageRect Box = new(50, 150, 200, 25);

    /// <summary>A row of six character boxes.</summary>
    private static readonly PageRect CombBox = new(50, 220, 120, 20);

    /// <summary>A line with seven ticks making six boxes.</summary>
    private static readonly PageRect TickedLine = new(50, 300, 120, 1);

    /// <summary>A box with printed text inside.</summary>
    private static readonly PageRect Label = new(270, 150, 100, 25);

    /// <summary>The printed text in the label box.</summary>
    private static readonly PageRect LabelText = new(280, 158, 60, 9);

    /// <summary>The text sitting on the underlined line.</summary>
    private static readonly PageRect TextOnLine = new(50, 412, 120, 7);

    /// <summary>A thick bar, which is a shape, not a line.</summary>
    private static readonly PageRect Bar = new(50, 360, 200, 10);

    /// <summary>A line with text sitting on it.</summary>
    private static readonly PageRect UnderlinedText = new(50, 420, 150, 1);

    /// <summary>Gets the image width in pixels.</summary>
    private static int Width => (int)(PageWidth * Scale);

    /// <summary>Gets the image height in pixels.</summary>
    private static int Height => (int)(PageHeight * Scale);

    /// <summary>Finds the line, box, comb box and ticked comb, and leaves out the label, the bar and the underlined text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsPlacesToWrite()
    {
        var regions = Detect(DrawForm());

        var line = regions.Single(static r => r.Kind == FormRegionKind.Underline);
        var box = regions.Single(static r => r.Kind == FormRegionKind.Box);
        var combs = regions.Where(static r => r.Kind == FormRegionKind.Comb).ToList();

        await Assert.That(regions.Count).IsEqualTo(Places);
        await Assert.That(line.Bounds.Left).IsEqualTo(Line.Left).Within(Tolerance);
        await Assert.That(line.Bounds.Bottom).IsEqualTo(Line.Top).Within(Tolerance);
        await Assert.That(line.Bounds.Width).IsEqualTo(Line.Width).Within(Tolerance);
        await Assert.That(line.Bounds.Height).IsGreaterThan(Tolerance * Tolerance);
        await Assert.That(box.Bounds.Left).IsEqualTo(Box.Left).Within(Tolerance);
        await Assert.That(box.Bounds.Top).IsEqualTo(Box.Top).Within(Tolerance);
        await Assert.That(box.Bounds.Width).IsEqualTo(Box.Width).Within(Tolerance);
        await Assert.That(box.Bounds.Height).IsEqualTo(Box.Height).Within(Tolerance);
        await Assert.That(combs.Count).IsEqualTo(TwoCombs);
        await Assert.That(combs[0].Cells).IsEqualTo(CombCells);
        await Assert.That(combs[0].Bounds.Width).IsEqualTo(CombBox.Width).Within(Tolerance);
        await Assert.That(combs[1].Cells).IsEqualTo(CombCells);
        await Assert.That(combs[1].Bounds.Bottom).IsEqualTo(TickedLine.Top).Within(Tolerance);
    }

    /// <summary>The vector and scalar scans find the same runs, column counts and places.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VectorAndScalarScansAgree()
    {
        var image = DrawForm();
        var vector = Detect(image);
        var runs = new List<DarkRun>();
        DarkPixels.FindRuns(image.AsSpan(0, Width), 1, 1, 0, runs);
        var row = image.AsSpan((int)(Box.Top * Scale) * Width, Width);
        var counts = new ushort[Width];
        DarkPixels.CountColumns(row, byte.MaxValue, counts);
        var scalarCounts = new ushort[Width];
        var scalarRuns = new List<DarkRun>();
        DarkPixels.UseVectors = false;
        List<FormRegion> scalar;
        try
        {
            scalar = Detect(image);
            DarkPixels.CountColumns(row, byte.MaxValue, scalarCounts);
            DarkPixels.FindRuns(row, byte.MaxValue, 1, 0, scalarRuns);
        }
        finally
        {
            DarkPixels.UseVectors = true;
        }

        await Assert.That(scalar).IsEquivalentTo(vector);
        await Assert.That(runs.Count).IsEqualTo(0);
        await Assert.That(counts).IsEquivalentTo(scalarCounts);
        DarkRun edge = new(0, (int)(Box.Left * Scale), (int)((Box.Right + 1) * Scale));
        await Assert.That(scalarRuns[0]).IsEqualTo(edge);
    }

    /// <summary>A blank page, an empty image and a bad scale find nothing; luminance follows the eye's weights.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlesEdgeCases()
    {
        var blank = Detect(White());
        var output = new List<FormRegion>();
        new FormRegionDetector().Detect([], (Width, Height, Width), Scale, output);
        new FormRegionDetector().Detect(White(), (Width, Height, Width), 0, output);
        var luma = new byte[2];
        DarkPixels.ToLuminance([0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF], luma);

        await Assert.That(blank.Count).IsEqualTo(0);
        await Assert.That(output.Count).IsEqualTo(0);
        await Assert.That(luma[0]).IsEqualTo(byte.MaxValue);
        await Assert.That(luma[1]).IsEqualTo(GreenLuma);
    }

    /// <summary>
    /// Finds the same places on a page that looks scanned: off-white paper, faded grey ink, grain and a soft focus,
    /// level and set up to a degree crooked.
    /// </summary>
    /// <param name="degrees">How far the page is turned, in degrees.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0F)]
    [Arguments(0.5F)]
    [Arguments(1F)]
    public async Task FindsPlacesOnAScannedPage(float degrees)
    {
        var regions = Detect(Scanned(DrawForm(), degrees));

        // The turn itself moves the box's far end down, so places may sit that much lower than on a level page.
        var tolerance = ScanTolerance + (Box.Right * MathF.Tan(degrees * MathF.PI / HalfTurnDegrees));

        var line = regions.Find(static r => r.Kind == FormRegionKind.Underline);
        var box = regions.Find(static r => r.Kind == FormRegionKind.Box);
        var combs = regions.FindAll(static r => r.Kind == FormRegionKind.Comb);

        await Assert.That(line.Bounds.Width).IsEqualTo(Line.Width).Within(tolerance);
        await Assert.That(box.Bounds.Left).IsEqualTo(Box.Left).Within(tolerance);
        await Assert.That(box.Bounds.Top).IsEqualTo(Box.Top).Within(tolerance);
        await Assert.That(box.Bounds.Width).IsEqualTo(Box.Width).Within(tolerance);
        await Assert.That(combs.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(combs[0].Cells).IsEqualTo(CombCells);
    }

    /// <summary>Makes a clean drawing look scanned: paper tone, faded ink, grain, blur and optionally a skew.</summary>
    /// <param name="image">The clean image, black on white.</param>
    /// <param name="degrees">How far to turn the page, by shearing rows, in degrees.</param>
    /// <returns>The scanned-looking image.</returns>
    private static byte[] Scanned(byte[] image, float degrees)
    {
        var toned = new byte[image.Length];
        var state = ScanSeed;
        for (var i = 0; i < image.Length; i++)
        {
            // A small repeatable xorshift gives the grain, so the test needs no random source.
            state ^= state << XorLeft;
            state ^= state >> XorRight;
            state ^= state << XorLast;
            var level = image[i] == 0 ? FadedInk : PaperTone;
            toned[i] = (byte)Math.Clamp(level + (int)(state % GrainSteps) - Grain, 0, byte.MaxValue);
        }

        var blurred = new byte[image.Length];
        for (var y = 1; y < Height - 1; y++)
        {
            for (var x = 1; x < Width - 1; x++)
            {
                var sum = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        sum += toned[((y + dy) * Width) + x + dx];
                    }
                }

                blurred[(y * Width) + x] = (byte)(sum / BlurArea);
            }
        }

        return degrees > 0 ? Skew(blurred, degrees) : blurred;
    }

    /// <summary>Turns an image slightly by moving each column down in proportion to how far right it is.</summary>
    /// <param name="image">The image.</param>
    /// <param name="degrees">How far to turn it, in degrees.</param>
    /// <returns>The turned image.</returns>
    private static byte[] Skew(byte[] image, float degrees)
    {
        var turned = new byte[image.Length];
        Array.Fill(turned, PaperTone);
        var slope = MathF.Tan(degrees * MathF.PI / HalfTurnDegrees);
        for (var x = 0; x < Width; x++)
        {
            var shift = (int)MathF.Round(x * slope);
            for (var y = 0; y + shift < Height; y++)
            {
                turned[((y + shift) * Width) + x] = image[(y * Width) + x];
            }
        }

        return turned;
    }

    /// <summary>Detects the places on an image.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The places.</returns>
    private static List<FormRegion> Detect(byte[] image)
    {
        var output = new List<FormRegion>();
        new FormRegionDetector().Detect(image, (Width, Height, Width), Scale, output);
        return output;
    }

    /// <summary>Draws the test form.</summary>
    /// <returns>The image's luminance.</returns>
    private static byte[] DrawForm()
    {
        var image = White();
        Fill(image, Line);
        Outline(image, Box);
        Outline(image, CombBox);
        for (var i = 1; i < CombCells; i++)
        {
            Fill(image, new(CombBox.Left + (i * CombBox.Width / CombCells), CombBox.Top, 1, CombBox.Height));
        }

        Fill(image, TickedLine);
        for (var i = 0; i <= CombCells; i++)
        {
            Fill(image, new(TickedLine.Left + (i * TickedLine.Width / CombCells), TickedLine.Top - TickRise, 1, TickRise));
        }

        Outline(image, Label);
        Fill(image, LabelText);
        Fill(image, Bar);
        Fill(image, UnderlinedText);
        Fill(image, TextOnLine);
        return image;
    }

    /// <summary>Makes a white image.</summary>
    /// <returns>The image.</returns>
    private static byte[] White()
    {
        var image = new byte[Width * Height];
        Array.Fill(image, byte.MaxValue);
        return image;
    }

    /// <summary>Draws a rectangle's outline one point thick.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rect">The rectangle in points.</param>
    private static void Outline(byte[] image, PageRect rect)
    {
        Fill(image, rect with { Height = 1 });
        Fill(image, rect with { Top = rect.Bottom, Height = 1 });
        Fill(image, rect with { Width = 1 });
        Fill(image, rect with { Left = rect.Right, Width = 1, Height = rect.Height + 1 });
    }

    /// <summary>Fills a rectangle black.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rect">The rectangle in points.</param>
    private static void Fill(byte[] image, PageRect rect)
    {
        for (var y = (int)(rect.Top * Scale); y < (int)(rect.Bottom * Scale); y++)
        {
            image.AsSpan((y * Width) + (int)(rect.Left * Scale), (int)(rect.Width * Scale)).Clear();
        }
    }
}
