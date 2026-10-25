// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that the page sharpness choices reach the library's render options.</summary>
public sealed class RenderSharpnessTests
{
    /// <summary>A minimum line width the choices carry.</summary>
    private const int LineWidth = 2;

    /// <summary>A text edge value no choice has.</summary>
    private const TextEdgeChoice UnknownEdge = (TextEdgeChoice)99;

    /// <summary>A stroke adjustment value no choice has.</summary>
    private const StrokeAdjustmentChoice UnknownAdjustment = (StrokeAdjustmentChoice)(-1);

    /// <summary>A minimum line width below zero.</summary>
    private const int NegativeWidth = -4;

    /// <summary>The default choices map to the library's default quality.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultsMapToTheLibraryDefaults()
    {
        var quality = HyperPdfDocumentRenderSharpness.ToQuality(RenderSharpness.Default);

        await Assert.That(quality).IsEqualTo(PdfRenderQuality.Default);
    }

    /// <summary>Every choice reaches its library setting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryChoiceMapsToItsLibrarySetting()
    {
        var sharpness = new RenderSharpness
        {
            TextEdges = TextEdgeChoice.Sharp,
            SmoothGraphics = false,
            SmoothImages = false,
            StrokeAdjustment = StrokeAdjustmentChoice.FollowPage,
            MinimumLineWidth = LineWidth,
            AreaImageReduction = false,
        };

        var quality = HyperPdfDocumentRenderSharpness.ToQuality(sharpness);

        await Assert.That(quality.TextEdges).IsEqualTo(PdfTextEdges.Sharp);
        await Assert.That(quality.SmoothGraphics).IsFalse();
        await Assert.That(quality.SmoothImages).IsFalse();
        await Assert.That(quality.StrokeAdjustment).IsEqualTo(PdfStrokeAdjustment.Document);
        await Assert.That(quality.MinimumLineWidth).IsEqualTo((float)LineWidth);
        await Assert.That(quality.AreaImageReduction).IsFalse();
    }

    /// <summary>A stroke adjustment of Always and a sharp text edge map to their library values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlwaysAndSharpMapToTheirLibraryValues()
    {
        var quality = HyperPdfDocumentRenderSharpness.ToQuality(new() { TextEdges = TextEdgeChoice.Sharp, StrokeAdjustment = StrokeAdjustmentChoice.Always });

        await Assert.That(quality.TextEdges).IsEqualTo(PdfTextEdges.Sharp);
        await Assert.That(quality.StrokeAdjustment).IsEqualTo(PdfStrokeAdjustment.Always);
    }

    /// <summary>Values out of range are brought into range before they reach the library.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutOfRangeValuesAreBroughtIntoRange()
    {
        var sharpness = new RenderSharpness { TextEdges = UnknownEdge };
        var quality = HyperPdfDocumentRenderSharpness.ToQuality(sharpness with { MinimumLineWidth = NegativeWidth, StrokeAdjustment = UnknownAdjustment });

        await Assert.That(quality.TextEdges).IsEqualTo(PdfTextEdges.Smooth);
        await Assert.That(quality.MinimumLineWidth).IsEqualTo(0F);
        await Assert.That(quality.StrokeAdjustment).IsEqualTo(PdfStrokeAdjustment.Off);
    }

    /// <summary>Setting the choices on a document gives the renderer options that carry them, with a renderer made after the change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentRendererOptionsFollowTheChoices()
    {
        using var pair = new HyperPdfTestDocument(1);
        var service = DocumentFeature<IRenderSharpness>.Cast(pair.Document)!;
        var before = HyperPdfRendering.GetRenderer(pair.Document);
        var chosen = new RenderSharpness { TextEdges = TextEdgeChoice.Sharp, StrokeAdjustment = StrokeAdjustmentChoice.Always };

        await Assert.That(service.Sharpness).IsEqualTo(RenderSharpness.Default);
        service.Sharpness = chosen;
        var after = HyperPdfRendering.GetRenderer(pair.Document);
        var options = HyperPdfFormRuntime.CreateRenderOptions(pair.Document);

        await Assert.That(service.Sharpness).IsEqualTo(chosen);
        await Assert.That(ReferenceEquals(before, after)).IsFalse();
        await Assert.That(options.Quality.TextEdges).IsEqualTo(PdfTextEdges.Sharp);
        await Assert.That(options.Quality.StrokeAdjustment).IsEqualTo(PdfStrokeAdjustment.Always);
    }

    /// <summary>The same choices set again keep working and a missing value is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NullChoicesAreRefused()
    {
        using var pair = new HyperPdfTestDocument(1);
        var service = DocumentFeature<IRenderSharpness>.Cast(pair.Document)!;

        await Assert.That(() => service.Sharpness = null!).Throws<ArgumentNullException>();
    }
}
