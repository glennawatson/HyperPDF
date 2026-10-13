// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that PDFium draws a page with edited layers the way HyperPDF does, in an incremental update and in a compact rewrite.</summary>
public sealed class LayerEditParityTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The page's width and height in points.</summary>
    private const int PageSize = 200;

    /// <summary>The object number of the red layer.</summary>
    private const int RedLayer = 5;

    /// <summary>The object number of the green layer.</summary>
    private const int GreenLayer = 6;

    /// <summary>The pixel column at the middle of the red square.</summary>
    private const int RedCentreX = 50;

    /// <summary>The pixel row at the middle of the red square.</summary>
    private const int RedCentreY = 50;

    /// <summary>The pixel column at the middle of the green square.</summary>
    private const int GreenCentreX = 150;

    /// <summary>The largest mean difference per channel accepted for squares drawn only with fills.</summary>
    private const double Threshold = 0.5;

    /// <summary>The value of a white channel.</summary>
    private const byte White = 255;

    /// <summary>The offset of the blue channel in a BGRA pixel.</summary>
    private const int BlueChannel = 0;

    /// <summary>Hiding the red layer and showing the green one is drawn the same way by PDFium and HyperPDF.</summary>
    /// <param name="compact">Whether the document is saved as a compact rewrite rather than an incremental update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PdfiumDrawsTheEditedLayers(bool compact)
    {
        using var edited = PdfDocumentReader.Open(CreateLayers(), null);
        PdfDocumentLayerEditing.SetLayerVisible(edited, RedLayer, false);
        PdfDocumentLayerEditing.SetLayerVisible(edited, GreenLayer, true);
        var bytes = compact ? PdfCompactWriter.Save(edited.Objects, PdfCompactOptions.Default) : PdfIncrementalWriter.Save(edited.Objects);

        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.None);
        var reference = PdfiumReferenceRendering.Render(bytes, info);
        var expected = reference.Pixels.ToArray();
        using var input = new HyperPdfTestDocument(bytes);
        var actual = new byte[expected.Length];
        await Assert.That(input.Document.Render(info, new(actual, reference.Width, reference.Height, reference.Width * BytesPerPixel))).IsTrue();

        await Assert.That(MeanDifference(expected, actual)).IsLessThan(Threshold);
        var red = ((RedCentreY * reference.Width) + RedCentreX) * BytesPerPixel;
        var green = ((RedCentreY * reference.Width) + GreenCentreX) * BytesPerPixel;
        await Assert.That(actual[red + BlueChannel]).IsEqualTo(White);
        await Assert.That(actual[green + BlueChannel]).IsNotEqualTo(White);
        await Assert.That(expected[red + BlueChannel]).IsEqualTo(White);
        await Assert.That(expected[green + BlueChannel]).IsNotEqualTo(White);
    }

    /// <summary>Builds a page with a red square on a layer that is on and a green square on a layer that is off.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateLayers()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize)
        {
            Content = "/OC /Red BDC 1 0 0 rg 10 110 80 80 re f EMC /OC /Green BDC 0 1 0 rg 110 110 80 80 re f EMC",
            Resources = "/Properties << /Red 5 0 R /Green 6 0 R >>",
        };
        _ = pdf.AddObject("<< /Type /OCG /Name (Red) >>");
        _ = pdf.AddObject("<< /Type /OCG /Name (Green) >>");
        pdf.CatalogEntries = "/OCProperties << /OCGs [5 0 R 6 0 R] /D << /OFF [6 0 R] /Order [5 0 R 6 0 R] >> >>";
        return pdf.ToBytes();
    }

    /// <summary>Computes the mean absolute difference per channel between two BGRA images.</summary>
    /// <param name="expected">The reference pixels.</param>
    /// <param name="actual">The pixels under test.</param>
    /// <returns>The mean difference, from 0 to 255.</returns>
    private static double MeanDifference(byte[] expected, byte[] actual)
    {
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}
