// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Tests.Graphics.Jpx;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Images from <see cref="PdfJpxEncoder"/> drawn one sample per pixel by PDFium and by HyperPDF. PDFium decodes JPEG 2000
/// with OpenJPEG, an independent decoder, so a lossless file must give back every sample in both engines, and a lossy file
/// must come out close to the original in both and nearly the same in each. That checks the encoder against a decoder it
/// shares no code with.
/// </summary>
public sealed class JpxEncoderParityTests
{
    /// <summary>The image width.</summary>
    private const int Width = 83;

    /// <summary>The image height.</summary>
    private const int Height = 61;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The bits of an 8-bit sample.</summary>
    private const int Eight = 8;

    /// <summary>The bits of a 16-bit sample.</summary>
    private const int Sixteen = 16;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of red in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The layers of the layered test.</summary>
    private const int ThreeLayers = 3;

    /// <summary>The tile side of the tiled test, which leaves partial tiles.</summary>
    private const int TileSide = 32;

    /// <summary>The code-block side of the small-block tests.</summary>
    private const int SmallBlock = 16;

    /// <summary>The quality of the lossy tests.</summary>
    private const int Quality = 90;

    /// <summary>The largest mean squared error a quality 90 file may have against the original, about 38 decibels.</summary>
    private const double LossyErrorLimit = 10;

    /// <summary>The largest difference, in levels, between the two engines on a lossy file.</summary>
    private const int EngineTolerance = 2;

    /// <summary>The largest 8-bit sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The largest 16-bit sample.</summary>
    private const int MaxWideSample = 65_535;

    /// <summary>Half of the largest 16-bit sample, for rounding.</summary>
    private const int HalfWideSample = 32_767;

    /// <summary>Lossless gray and RGB files give back every sample in both engines.</summary>
    /// <param name="components">1 for gray, 3 for RGB.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(Rgb)]
    public async Task LosslessFilesMatchPdfium(int components)
    {
        var image = JpxEncoderSamples.Gradient(Width, Height, components, Eight);

        var (pdfium, hyper) = Compare(image, new());

        await Assert.That(pdfium).IsEqualTo(0);
        await Assert.That(hyper).IsEqualTo(0);
    }

    /// <summary>Lossless files with tiles, layers, precincts, small code-blocks, every progression order and the mode switches match in both engines.</summary>
    /// <param name="order">The progression order.</param>
    /// <param name="style">The code-block mode switches.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpxProgression.LayerResolutionComponentPosition, PdfJpxBlockStyle.None)]
    [Arguments(PdfJpxProgression.ResolutionLayerComponentPosition, PdfJpxBlockStyle.Bypass)]
    [Arguments(PdfJpxProgression.ResolutionPositionComponentLayer, PdfJpxBlockStyle.TerminateAll)]
    [Arguments(PdfJpxProgression.PositionComponentResolutionLayer, PdfJpxBlockStyle.Reset | PdfJpxBlockStyle.SegmentationSymbols)]
    [Arguments(PdfJpxProgression.ComponentPositionResolutionLayer, PdfJpxBlockStyle.VerticallyCausal | PdfJpxBlockStyle.Bypass | PdfJpxBlockStyle.TerminateAll)]
    public async Task StructuredLosslessFilesMatchPdfium(PdfJpxProgression order, PdfJpxBlockStyle style)
    {
        var options = new PdfJpxOptions
        {
            Progression = order,
            BlockStyle = style,
            Layers = ThreeLayers,
            TileWidth = TileSide,
            TileHeight = TileSide,
            PrecinctSize = TileSide,
            CodeBlockWidth = SmallBlock,
            CodeBlockHeight = SmallBlock,
            StartOfPacketMarkers = true,
            EndOfPacketHeaderMarkers = true,
        };

        var (pdfium, hyper) = Compare(JpxEncoderSamples.Gradient(Width, Height, Rgb, Eight), options);

        await Assert.That(pdfium).IsEqualTo(0);
        await Assert.That(hyper).IsEqualTo(0);
    }

    /// <summary>Lossy files are close to the original in both engines and nearly equal in the two.</summary>
    /// <param name="components">1 for gray, 3 for RGB.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(Rgb)]
    public async Task LossyFilesMatchPdfium(int components)
    {
        var image = JpxEncoderSamples.Gradient(Width, Height, components, Eight);
        var pdf = Page(image, await PdfJpxEncoder.EncodeAsync(image, new() { Mode = PdfJpxMode.Lossy, Quality = Quality }, CancellationToken.None));
        using var input = new HyperPdfTestDocument(pdf);
        var reference = PdfiumReferenceRendering.RenderPage(pdf, 0, 1).Pixels.ToArray();
        var hyper = Render(input.Document);

        await Assert.That(MeanSquaredError(reference, image)).IsLessThan(LossyErrorLimit);
        await Assert.That(MeanSquaredError(hyper, image)).IsLessThan(LossyErrorLimit);
        await Assert.That(LargestDifference(reference, hyper)).IsLessThanOrEqualTo(EngineTolerance);
    }

    /// <summary>A 16-bit file draws as the same 8-bit picture in both engines, within a level.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SixteenBitFilesMatchPdfium()
    {
        var image = JpxEncoderSamples.Gradient(Width, Height, 1, Sixteen);
        var pdf = Page(image, PdfJpxEncoder.Encode(image));
        using var input = new HyperPdfTestDocument(pdf);
        var reference = PdfiumReferenceRendering.RenderPage(pdf, 0, 1).Pixels.ToArray();
        var hyper = Render(input.Document);
        var samples = JpxEncoderSamples.Component(image, 0);

        for (var i = 0; i < samples.Length; i++)
        {
            var expected = ((samples[i] * MaxSample) + HalfWideSample) / MaxWideSample;
            await Assert.That(Math.Abs(reference[i * BytesPerPixel] - expected)).IsLessThanOrEqualTo(1);
        }

        await Assert.That(LargestDifference(reference, hyper)).IsLessThanOrEqualTo(1);
    }

    /// <summary>Encodes an image, draws it with both engines and counts the samples each gets wrong.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">The encoder options.</param>
    /// <returns>The wrong samples from PDFium and from HyperPDF.</returns>
    private static SampleErrors Compare(PdfJpxImage image, PdfJpxOptions options)
    {
        var pdf = Page(image, PdfJpxEncoder.Encode(image, options, CancellationToken.None));
        using var input = new HyperPdfTestDocument(pdf);
        var reference = PdfiumReferenceRendering.RenderPage(pdf, 0, 1);
        return new(Errors(reference.Pixels.ToArray(), image), Errors(Render(input.Document), image));
    }

    /// <summary>Makes a PDF that draws the image at one point per pixel.</summary>
    /// <param name="image">The image, for its size and channels.</param>
    /// <param name="file">The JPX file.</param>
    /// <returns>The PDF.</returns>
    private static byte[] Page(PdfJpxImage image, byte[] file)
    {
        var space = image.Components == 1 ? "DeviceGray" : "DeviceRGB";
        var pdf = new RenderTestPdf(Width, Height) { Content = string.Create(CultureInfo.InvariantCulture, $"q {Width} 0 0 {Height} 0 0 cm /Im Do Q") };
        var entries = string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width {Width} /Height {Height} /ColorSpace /{space}");
        var stream = pdf.AddStream($"{entries} /BitsPerComponent 8 /Filter /JPXDecode", file);
        pdf.Resources = $"/XObject << /Im {stream} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Draws the page one point per pixel.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be drawn.</exception>
    private static byte[] Render(IDocument document)
    {
        var pixels = new byte[Width * Height * BytesPerPixel];
        var drawn = document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, Width, Height, Width * BytesPerPixel));
        return drawn ? pixels : throw new InvalidOperationException("The page could not be drawn.");
    }

    /// <summary>Counts the samples that differ from the image.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="image">The image.</param>
    /// <returns>The wrong samples.</returns>
    private static int Errors(byte[] pixels, PdfJpxImage image)
    {
        var errors = 0;
        for (var c = 0; c < image.Components; c++)
        {
            var samples = JpxEncoderSamples.Component(image, c);
            for (var i = 0; i < samples.Length; i++)
            {
                errors += pixels[(i * BytesPerPixel) + RedByte - c] == samples[i] ? 0 : 1;
            }
        }

        return errors;
    }

    /// <summary>Gets the mean squared error of the drawn samples against the image.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="image">The image.</param>
    /// <returns>The mean squared error.</returns>
    private static double MeanSquaredError(byte[] pixels, PdfJpxImage image)
    {
        var sum = 0.0;
        for (var c = 0; c < image.Components; c++)
        {
            var samples = JpxEncoderSamples.Component(image, c);
            for (var i = 0; i < samples.Length; i++)
            {
                var difference = pixels[(i * BytesPerPixel) + RedByte - c] - samples[i];
                sum += difference * difference;
            }
        }

        return sum / (Width * Height * image.Components);
    }

    /// <summary>Gets the largest difference between the colour channels of two renderings.</summary>
    /// <param name="first">The first BGRA pixels.</param>
    /// <param name="second">The second BGRA pixels.</param>
    /// <returns>The largest difference.</returns>
    private static int LargestDifference(byte[] first, byte[] second)
    {
        var largest = 0;
        for (var i = 0; i < Width * Height; i++)
        {
            for (var c = 0; c < Rgb; c++)
            {
                largest = Math.Max(largest, Math.Abs(first[(i * BytesPerPixel) + c] - second[(i * BytesPerPixel) + c]));
            }
        }

        return largest;
    }

    /// <summary>The samples each engine got wrong.</summary>
    /// <param name="Pdfium">PDFium's wrong samples.</param>
    /// <param name="Hyper">HyperPDF's wrong samples.</param>
    private readonly record struct SampleErrors(int Pdfium, int Hyper);
}
