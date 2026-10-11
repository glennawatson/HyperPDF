// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;
using SkiaSharp;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks the viewer's graphics render feature without requiring a window.</summary>
public sealed class HyperPdfGpuRendererTests
{
    /// <summary>The edge of the render target.</summary>
    private const int Edge = 128;

    /// <summary>The feature records off the graphics thread and replays on a caller-owned target.</summary>
    /// <param name="cancellationToken">Cancels page preparation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FeaturePreparesAndDraws(CancellationToken cancellationToken)
    {
        using var document = new HyperPdfDocument(PdfDocumentReader.Open(TestPdf.Create(1), null), "graphics-feature.pdf");
        var feature = document.GetFeature(typeof(IHyperPdfGpuRenderer));
        await Assert.That(feature).IsTypeOf<IHyperPdfGpuRenderer>();
        var renderer = (IHyperPdfGpuRenderer)feature!;
        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);
        var layout = new SKImageInfo(Edge, Edge, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var target = new SkiaSurfaceRenderTarget(SKSurface.Create(layout), layout);

        var prepared = await renderer.PrepareAsync(info, cancellationToken);
        var drawn = renderer.Render(info, target);
        using var snapshot = target.Snapshot();

        await Assert.That(prepared).IsTrue();
        await Assert.That(drawn).IsTrue();
        await Assert.That(snapshot.Width).IsEqualTo(Edge);
        await Assert.That(snapshot.Height).IsEqualTo(Edge);
    }
}
