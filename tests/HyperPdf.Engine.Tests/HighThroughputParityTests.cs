// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Tests.Graphics.Jpeg;
using HyperPdfLibrary.Tests.Graphics.Jpx;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// High-throughput JPEG 2000 (ITU-T Rec. T.814) images from the test encoder, drawn one sample per pixel by both
/// engines. PDFium decodes HTJ2K with its own decoder, an independent one, so both engines must give back the
/// encoded samples exactly: that checks the test encoder against PDFium's decoder and the managed decoder against both.
/// </summary>
public sealed class HighThroughputParityTests
{
    /// <summary>The image width.</summary>
    private const int Width = 97;

    /// <summary>The image height.</summary>
    private const int Height = 71;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of red in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The seed of the sample generator.</summary>
    private const uint Seed = 2026;

    /// <summary>One past the largest 8-bit sample.</summary>
    private const int SampleRange = 256;

    /// <summary>The weight of the horizontal gradient.</summary>
    private const int GradientX = 3;

    /// <summary>The weight of the vertical gradient.</summary>
    private const int GradientY = 2;

    /// <summary>The weight of the component in the gradient.</summary>
    private const int GradientComponent = 80;

    /// <summary>The spread of the noise added to the gradient.</summary>
    private const int Noise = 48;

    /// <summary>The layers of the layered test.</summary>
    private const int ThreeLayers = 3;

    /// <summary>The tile width of the tiled test.</summary>
    private const int TileWidth = 48;

    /// <summary>The tile height of the tiled test.</summary>
    private const int TileHeight = 35;

    /// <summary>The code-block exponent of the small-block test.</summary>
    private const int SmallBlocks = 3;

    /// <summary>Gray and RGB HT images match the encoded samples in both engines, with the cleanup pass alone or with the refinement passes.</summary>
    /// <param name="components">1 for gray, 3 for RGB.</param>
    /// <param name="refine">Whether the refinement passes are used.</param>
    /// <param name="causal">Whether the vertically causal mode is on.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, false, false)]
    [Arguments(1, true, false)]
    [Arguments(1, true, true)]
    [Arguments(Rgb, false, false)]
    [Arguments(Rgb, true, false)]
    public async Task HighThroughputImagesMatchPdfium(int components, bool refine, bool causal)
    {
        var style = JpxBlockStyle.HighThroughput | (causal ? JpxBlockStyle.VerticallyCausal : JpxBlockStyle.None);
        var options = new JpxTestOptions { Width = Width, Height = Height, Components = components, Transform = components == Rgb, Style = style, HtRefinement = refine };

        var (pdfium, hyper) = Compare(options);

        await Assert.That(pdfium).IsEqualTo(0);
        await Assert.That(hyper).IsEqualTo(0);
    }

    /// <summary>Small code-blocks over several layers and tiles of cleanup-only HT data match in both engines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayeredHighThroughputImagesMatchPdfium()
    {
        var options = new JpxTestOptions { Width = Width, Height = Height, Components = Rgb, Transform = true, Style = JpxBlockStyle.HighThroughput };
        options = options with { Layers = ThreeLayers, BlockExponent = SmallBlocks, TileWidth = TileWidth, TileHeight = TileHeight };

        var (pdfium, hyper) = Compare(options);

        await Assert.That(pdfium).IsEqualTo(0);
        await Assert.That(hyper).IsEqualTo(0);
    }

    /// <summary>Encodes an image, draws it with both engines and counts the samples each gets wrong.</summary>
    /// <param name="options">The encoder options.</param>
    /// <returns>The wrong samples from PDFium and from HyperPDF.</returns>
    private static SampleErrors Compare(JpxTestOptions options)
    {
        var planes = Planes(options);
        var codestream = JpxTestEncoder.Encode(options, planes);
        var space = options.Components == Rgb ? "DeviceRGB" : "DeviceGray";
        var pdf = new RenderTestPdf(Width, Height) { Content = string.Create(CultureInfo.InvariantCulture, $"q {Width} 0 0 {Height} 0 0 cm /Im Do Q") };
        var entries = string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width {Width} /Height {Height} /ColorSpace /{space}");
        var image = pdf.AddStream($"{entries} /BitsPerComponent 8 /Filter /JPXDecode", codestream);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        var bytes = pdf.ToBytes();
        using var input = new HyperPdfTestDocument(bytes);
        var reference = PdfiumReferenceRendering.RenderPage(bytes, 0, 1);
        return new(Errors(reference.Pixels.ToArray(), planes), Errors(Render(input.Document), planes));
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

    /// <summary>Counts the pixels whose channels differ from the encoded samples.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="planes">The encoded samples: gray, or red, green and blue.</param>
    /// <returns>The wrong samples.</returns>
    private static int Errors(byte[] pixels, int[][] planes)
    {
        var errors = 0;
        for (var i = 0; i < Width * Height; i++)
        {
            for (var c = 0; c < Rgb; c++)
            {
                var expected = planes[planes.Length == 1 ? 0 : c][i];
                errors += pixels[(i * BytesPerPixel) + RedByte - c] == expected ? 0 : 1;
            }
        }

        return errors;
    }

    /// <summary>Makes deterministic samples: a gradient with noise.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The planes.</returns>
    private static int[][] Planes(JpxTestOptions options)
    {
        var random = new JpegTestRandom(Seed);
        var planes = new int[options.Components][];
        for (var c = 0; c < planes.Length; c++)
        {
            planes[c] = new int[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    planes[c][(y * Width) + x] = ((x * GradientX) + (y * GradientY) + (c * GradientComponent) + random.Next(0, Noise)) % SampleRange;
                }
            }
        }

        return planes;
    }
}
