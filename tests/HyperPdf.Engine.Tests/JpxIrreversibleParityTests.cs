// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Tests.Graphics.Jpx;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// The corpus 9/7 (irreversible) JPEG 2000 image drawn one sample per pixel by both engines. HyperPDF scales the
/// wavelet output and the 8-bit samples with its own fixed-point rules (T.800 annexes E to G), so it may round a sample
/// differently from PDFium. The measured difference is recorded below.
/// </summary>
public sealed class JpxIrreversibleParityTests
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The colour channels compared per pixel.</summary>
    private const int ColourChannels = 3;

    /// <summary>
    /// The largest difference, in sample levels, measured between the engines on the corpus image: 113 of its 1,828,170 samples
    /// (610 x 999 x 3) differ, each by one level. The cause was not isolated; it follows the change to
    /// the 9/7 high-pass scale and the 8-bit rounding written from the standard.
    /// </summary>
    private const int MeasuredMaxDifference = 1;

    /// <summary>The corpus 9/7 image differs from PDFium's rendering by at most one level per sample.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusIrreversibleStaysWithinMeasuredDifference()
    {
        const int width = JpxFixtures.IrreversibleWidth;
        const int height = JpxFixtures.IrreversibleHeight;
        var pdf = new RenderTestPdf(width, height) { Content = string.Create(CultureInfo.InvariantCulture, $"q {width} 0 0 {height} 0 0 cm /Im Do Q") };
        var entries = string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB");
        var image = pdf.AddStream($"{entries} /BitsPerComponent 8 /Filter /JPXDecode", Convert.FromBase64String(JpxFixtures.Irreversible));
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        var bytes = pdf.ToBytes();
        using var input = new HyperPdfTestDocument(bytes);
        var reference = PdfiumReferenceRendering.RenderPage(bytes, 0, 1).Pixels.ToArray();
        var pixels = new byte[width * height * BytesPerPixel];
        var drawn = input.Document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));

        var maxDifference = 0;
        var differing = 0;
        for (var i = 0; i < width * height; i++)
        {
            for (var c = 0; c < ColourChannels; c++)
            {
                var difference = Math.Abs(pixels[(i * BytesPerPixel) + c] - reference[(i * BytesPerPixel) + c]);
                maxDifference = Math.Max(maxDifference, difference);
                differing += difference == 0 ? 0 : 1;
            }
        }

        await Assert.That(drawn).IsTrue();
        await Assert.That(maxDifference).IsLessThanOrEqualTo(MeasuredMaxDifference).Because($"{differing} samples differ, the largest by {maxDifference}");
    }
}
