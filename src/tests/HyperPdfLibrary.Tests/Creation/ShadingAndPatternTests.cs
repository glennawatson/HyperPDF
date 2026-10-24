// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Shadings and patterns added as reusable resources, and pages painted with them.</summary>
public sealed class ShadingAndPatternTests
{
    /// <summary>The width of the test page.</summary>
    private const float PageWidth = 100F;

    /// <summary>The height of the test page.</summary>
    private const float PageHeight = 50F;

    /// <summary>Half the width of the test page.</summary>
    private const float HalfWidth = PageWidth / 2;

    /// <summary>Half the height of the test page.</summary>
    private const float HalfHeight = PageHeight / 2;

    /// <summary>The size of a tile in the tiling test.</summary>
    private const float TileSize = 10F;

    /// <summary>The distance from the page edge of the pixels the render tests read.</summary>
    private const int EdgeInset = 3;

    /// <summary>The most a colour channel may differ from the value expected.</summary>
    private const int ChannelTolerance = 24;

    /// <summary>The level above which a channel counts as fully on.</summary>
    private const int Bright = 220;

    /// <summary>The level below which a channel counts as off.</summary>
    private const int Dark = 50;

    /// <summary>The function type of an exponential function.</summary>
    private const int ExponentialType = 2;

    /// <summary>The function type of a stitching function.</summary>
    private const int StitchingType = 3;

    /// <summary>The shading type of an axial shading.</summary>
    private const int AxialType = 2;

    /// <summary>The pattern type of a tiling pattern.</summary>
    private const int TilingType = 1;

    /// <summary>The pattern type of a shading pattern.</summary>
    private const int ShadingPatternType = 2;

    /// <summary>The numbers in an axial shading's coordinates.</summary>
    private const int AxialCoordinates = 4;

    /// <summary>The numbers in a matrix.</summary>
    private const int MatrixEntries = 6;

    /// <summary>The pieces of the three colour gradient.</summary>
    private const int ThreeColorPieces = 2;

    /// <summary>The pieces of the gradient whose stops do not reach the ends.</summary>
    private const int InsetPieces = 3;

    /// <summary>The offset of the middle stop.</summary>
    private const float Middle = 0.5F;

    /// <summary>The offset of the inner first stop.</summary>
    private const float Quarter = 0.25F;

    /// <summary>The offset of the inner last stop.</summary>
    private const float ThreeQuarters = 0.75F;

    /// <summary>The offset of an out of order stop.</summary>
    private const float Late = 0.8F;

    /// <summary>The offset of the stop that follows it.</summary>
    private const float Early = 0.2F;

    /// <summary>The translation of the shading pattern's matrix.</summary>
    private const float Shift = 5F;

    /// <summary>The radius of the radial shading's outer circle.</summary>
    private const float OuterRadius = 20F;

    /// <summary>Gets two stops from red to blue.</summary>
    private static PdfGradientStop[] RedToBlue { get; } = [new(0, 1, 0, 0), new(1, 0, 0, 1)];

    /// <summary>Gets three stops from red through green to blue.</summary>
    private static PdfGradientStop[] ThreeColors { get; } = [new(0, 1, 0, 0), new(Middle, 0, 1, 0), new(1, 0, 0, 1)];

    /// <summary>Gets two stops that do not reach the ends of the gradient.</summary>
    private static PdfGradientStop[] Inset { get; } = [new(Quarter, 1, 0, 0), new(ThreeQuarters, 0, 0, 1)];

    /// <summary>Gets stops with a hard edge in the middle.</summary>
    private static PdfGradientStop[] HardEdge { get; } = [new(0, 1, 0, 0), new(Middle, 1, 0, 0), new(Middle, 0, 0, 1), new(1, 0, 0, 1)];

    /// <summary>Two stops that span the gradient make one exponential function.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoStopsMakeAnExponentialFunction()
    {
        var sink = new ListSink();

        var id = PdfShadingWriter.AddAxial(sink, new(0, 0), new(PageWidth, 0), RedToBlue, true, true);

        var shading = sink.GetDictionary(id);
        var function = shading.GetDictionary(KnownName.Function)!;
        await Assert.That(shading.GetInt32(KnownName.ShadingType)).IsEqualTo(AxialType);
        await Assert.That(function.GetInt32(KnownName.FunctionType)).IsEqualTo(ExponentialType);
        await Assert.That(shading.GetArray(KnownName.Coords)!.Count).IsEqualTo(AxialCoordinates);
        await Assert.That(shading.GetArray(KnownName.Extend)!.Get(0).AsBoolean()).IsTrue();
    }

    /// <summary>Three stops, and stops that do not reach the ends, make a stitching function that covers the whole domain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MoreStopsMakeAStitchingFunction()
    {
        var sink = new ListSink();

        var three = sink.GetDictionary(PdfShadingWriter.AddRadial(sink, new(0, 0, 0), new(0, 0, 1), ThreeColors, false, false)).GetDictionary(KnownName.Function)!;
        var padded = sink.GetDictionary(PdfShadingWriter.AddAxial(sink, new(0, 0), new(1, 0), Inset, false, false)).GetDictionary(KnownName.Function)!;

        await Assert.That(three.GetInt32(KnownName.FunctionType)).IsEqualTo(StitchingType);
        await Assert.That(three.GetArray(KnownName.Functions)!.Count).IsEqualTo(ThreeColorPieces);
        await Assert.That(three.GetArray(KnownName.Bounds)!.Count).IsEqualTo(1);
        await Assert.That(padded.GetArray(KnownName.Functions)!.Count).IsEqualTo(InsetPieces);
        await Assert.That(padded.GetArray(KnownName.Bounds)!.Count).IsEqualTo(InsetPieces - 1);
        await Assert.That(padded.GetArray(KnownName.Encode)!.Count).IsEqualTo(InsetPieces * ThreeColorPieces);
    }

    /// <summary>A hard stop, two stops at one position, makes no piece of zero width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HardStopsLeaveNoEmptyPiece()
    {
        var sink = new ListSink();

        var function = sink.GetDictionary(PdfShadingWriter.AddAxial(sink, new(0, 0), new(1, 0), HardEdge, false, false)).GetDictionary(KnownName.Function)!;

        var bounds = function.GetArray(KnownName.Bounds)!;
        await Assert.That(function.GetArray(KnownName.Functions)!.Count).IsEqualTo(bounds.Count + 1);
        await Assert.That(bounds.GetSingle(0)).IsEqualTo(Middle);
    }

    /// <summary>Gradients that are out of order, too short or have a negative radius are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadGradientsAreRefused()
    {
        var sink = new ListSink();
        var outOfOrder = false;
        var tooShort = false;
        var negative = false;
        try
        {
            _ = PdfShadingWriter.AddAxial(sink, new(0, 0), new(1, 0), [new(Late, 1, 0, 0), new(Early, 0, 0, 1)], false, false);
        }
        catch (ArgumentException)
        {
            outOfOrder = true;
        }

        try
        {
            _ = PdfShadingWriter.AddAxial(sink, new(0, 0), new(1, 0), [new(0, 1, 0, 0)], false, false);
        }
        catch (ArgumentException)
        {
            tooShort = true;
        }

        try
        {
            _ = PdfShadingWriter.AddRadial(sink, new(0, 0, -1), new(0, 0, 1), RedToBlue, false, false);
        }
        catch (ArgumentOutOfRangeException)
        {
            negative = true;
        }

        await Assert.That(outOfOrder).IsTrue();
        await Assert.That(tooShort).IsTrue();
        await Assert.That(negative).IsTrue();
    }

    /// <summary>Shading and tiling patterns carry the entries a reader needs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternsCarryTheirEntries()
    {
        var sink = new ListSink();
        var shading = PdfShadingWriter.AddAxial(sink, new(0, 0), new(1, 0), RedToBlue, false, false);
        var resources = new PdfPageResources(sink);

        var shadingPattern = PdfPatternWriter.AddShadingPattern(sink, shading, [1, 0, 0, 1, Shift, 0]);
        var tilingPattern = PdfPatternWriter.AddTilingPattern(sink, new(0, 0, TileSize, TileSize), TileSize, TileSize, "0 0 5 5 re f\n"u8, resources, []);

        var shaded = sink.GetDictionary(shadingPattern);
        var tiled = sink.GetStream(tilingPattern).Dictionary;
        await Assert.That(shaded.GetInt32(KnownName.PatternType)).IsEqualTo(ShadingPatternType);
        await Assert.That(shaded.GetRaw(KnownName.Shading).AsReference()).IsEqualTo(shading);
        await Assert.That(shaded.GetArray(KnownName.Matrix)!.Count).IsEqualTo(MatrixEntries);
        await Assert.That(tiled.GetInt32(KnownName.PatternType)).IsEqualTo(TilingType);
        await Assert.That(tiled.GetInt32(KnownName.PaintType)).IsEqualTo(1);
        await Assert.That(tiled.GetSingle(KnownName.XStep)).IsEqualTo(TileSize);
    }

    /// <summary>The same object added twice to a page's resources keeps one name, and different objects get different names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResourcesNameEachObjectOnce()
    {
        var sink = new ListSink();
        var first = sink.Add(PdfValue.FromInteger(0));
        var second = sink.Add(PdfValue.FromInteger(1));
        var resources = new PdfPageResources(sink);

        var one = resources.AddShading(first);
        var again = resources.AddShading(first);
        var other = resources.AddShading(second);
        var image = resources.AddImage(first);
        var form = resources.AddForm(second);
        var dictionary = resources.ToDictionary();

        await Assert.That(again).IsEqualTo(one);
        await Assert.That(other).IsNotEqualTo(one);
        await Assert.That(dictionary.GetDictionary(KnownName.Shading)!.Count).IsEqualTo(ThreeColorPieces);
        await Assert.That(dictionary.GetDictionary(KnownName.XObject)!.Count).IsEqualTo(ThreeColorPieces);
        await Assert.That(image.Value).IsNotEqualTo(form.Value);
        await Assert.That(resources.IsEmpty).IsFalse();
    }

    /// <summary>A page painted with a red to blue axial shading is red on the left, blue on the right and mixed in the middle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialShadingRenders()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var shading = PdfShadingWriter.AddAxial(creator.Sink, new(0, 0), new(PageWidth, 0), RedToBlue, true, true);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.FillRectangleWithShading(shading, 0, 0, PageWidth, PageHeight);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        var pdf = creator.ToArray();
        CreatedFiles.Keep("axial-shading.pdf", pdf);
        var image = OptimizerTestKit.Render(pdf);

        var left = CreationTestKit.PixelAt(image, EdgeInset, (int)HalfHeight);
        var middle = CreationTestKit.PixelAt(image, (int)HalfWidth, (int)HalfHeight);
        var right = CreationTestKit.PixelAt(image, (int)PageWidth - EdgeInset, (int)HalfHeight);
        await Assert.That(left.Red).IsGreaterThan((byte)Bright);
        await Assert.That(left.Blue).IsLessThan((byte)Dark);
        await Assert.That(right.Blue).IsGreaterThan((byte)Bright);
        await Assert.That(right.Red).IsLessThan((byte)Dark);
        await Assert.That(Math.Abs(middle.Red - middle.Blue)).IsLessThan(ChannelTolerance);
        await Assert.That(middle.Red).IsGreaterThan((byte)Dark);
    }

    /// <summary>A shading pattern and a tiling pattern both paint through the pattern colour space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternsRender()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var shading = PdfShadingWriter.AddAxial(creator.Sink, new(0, 0), new(HalfWidth, 0), [new(0, 0, 1, 0), new(1, 0, 1, 0)], true, true);
        var shadingPattern = PdfPatternWriter.AddShadingPattern(creator.Sink, shading, []);
        var tilePattern = AddRedTiles(creator);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.FillRectangleWithPattern(shadingPattern, 0, 0, HalfWidth, PageHeight);
            canvas.FillRectangleWithPattern(tilePattern, HalfWidth, 0, HalfWidth, PageHeight);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        var pdf = creator.ToArray();
        CreatedFiles.Keep("patterns.pdf", pdf);
        var image = OptimizerTestKit.Render(pdf);

        var green = CreationTestKit.PixelAt(image, (int)TileSize, (int)TileSize);
        var red = CreationTestKit.PixelAt(image, (int)(HalfWidth + TileSize), (int)HalfHeight);
        await Assert.That(green.Green).IsGreaterThan((byte)Bright);
        await Assert.That(red.Red).IsGreaterThan((byte)Bright);
        await Assert.That(red.Green).IsLessThan((byte)Dark);
    }

    /// <summary>A page with a radial shading passes <c>qpdf --check</c>.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingPagePassesQpdf(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var shading = PdfShadingWriter.AddRadial(creator.Sink, new(HalfWidth, HalfHeight, 0), new(HalfWidth, HalfHeight, OuterRadius), ThreeColors, false, true);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.FillRectangleWithShading(shading, 0, 0, PageWidth, PageHeight);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        await CreationTestKit.CheckWithQpdfAsync("shading-page", creator.ToArray(), cancellationToken);
    }

    /// <summary>Adds a tiling pattern of red squares.</summary>
    /// <param name="creator">The document.</param>
    /// <returns>The pattern's object.</returns>
    private static PdfObjectId AddRedTiles(PdfDocumentCreator creator)
    {
        var content = default(PdfContentBuilder);
        try
        {
            content.SetFillRgb(1, 0, 0);
            content.Rectangle(0, 0, TileSize, TileSize);
            content.Fill();
            return PdfPatternWriter.AddTilingPattern(creator.Sink, new(0, 0, TileSize, TileSize), TileSize, TileSize, content.WrittenSpan, new(creator.Sink), []);
        }
        finally
        {
            content.Dispose();
        }
    }
}
