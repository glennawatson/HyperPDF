// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>A JPEG saved at a higher quality than the one asked for is written again; one at or below it is left alone.</summary>
[NotInParallel]
public sealed class JpegRewriteTests
{
    /// <summary>The side of the test photo in pixels.</summary>
    private const int Side = 240;

    /// <summary>The size the photo is drawn at, in points; about 100 ppi, so it is not downsampled.</summary>
    private const int DrawPoints = 170;

    /// <summary>The components of the photo.</summary>
    private const int Rgb = 3;

    /// <summary>The quality of the high quality photo.</summary>
    private const int HighQuality = 95;

    /// <summary>The quality of the low quality photo.</summary>
    private const int LowQuality = 60;

    /// <summary>The most the estimate may differ from the quality the encoder was given.</summary>
    private const int EstimateTolerance = 4;

    /// <summary>The entries of a JPEG-coded RGB image.</summary>
    private const string JpegEntries = "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode";

    /// <summary>Four bytes that do not start a JPEG.</summary>
    private static readonly byte[] NotJpeg = [0x01, 0x02, 0x03, 0x04];

    /// <summary>The quality estimate follows the quality the encoder was given.</summary>
    /// <param name="quality">The encoder's quality.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(30)]
    [Arguments(50)]
    [Arguments(80)]
    [Arguments(95)]
    public async Task EstimateFollowsEncoderQuality(int quality)
    {
        var jpeg = Encode(quality);

        await Assert.That(Math.Abs(JpegQualityEstimate.Estimate(jpeg) - quality)).IsLessThanOrEqualTo(EstimateTolerance);
    }

    /// <summary>Data that is not a JPEG has no estimate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonJpegHasNoEstimate()
    {
        await Assert.That(JpegQualityEstimate.Estimate(NotJpeg)).IsEqualTo(-1);
        await Assert.That(JpegQualityEstimate.Estimate([])).IsEqualTo(-1);
    }

    /// <summary>A high quality JPEG that is not downsampled is written again at the lower quality, smaller.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HighQualityJpegIsWrittenAgainSmaller()
    {
        var source = Page(Encode(HighQuality));

        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced);
        var image = FirstImage(result.Bytes);

        await Assert.That(result.Bytes.Length).IsLessThan(source.Length);
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(Side);
        await Assert.That(JpegQualityEstimate.Estimate(image.CopyRawData())).IsLessThanOrEqualTo(PdfOptimizeOptions.Balanced.JpegQuality + EstimateTolerance);
    }

    /// <summary>A JPEG already at or below the quality asked for is left as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LowQualityJpegIsLeftAlone()
    {
        var source = Page(Encode(LowQuality));

        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced);

        await Assert.That(JpegQualityEstimate.Estimate(FirstImage(result.Bytes).CopyRawData())).IsEqualTo(JpegQualityEstimate.Estimate(FirstImage(source).CopyRawData()));
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>With lossy changes off, a high quality JPEG is left as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepQualityLeavesJpegAlone()
    {
        var source = Page(Encode(HighQuality));

        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);

        await Assert.That(JpegQualityEstimate.Estimate(FirstImage(result.Bytes).CopyRawData())).IsEqualTo(JpegQualityEstimate.Estimate(FirstImage(source).CopyRawData()));
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>Encodes the test photo.</summary>
    /// <param name="quality">The JPEG quality.</param>
    /// <returns>The JPEG file.</returns>
    private static byte[] Encode(int quality)
    {
        var samples = OptimizerSamples.PhotoPixels(Side, Side);
        var pixels = default(PooledBuffer);
        var jpeg = default(PooledBuffer);
        try
        {
            ImagePixels.FromSamples(samples, Rgb, Side * Side, ref pixels);
            ImagePixels.EncodeJpeg(pixels.WrittenSpan, new(Side, Side, Rgb), new() { Quality = quality }, ref jpeg, new(), CancellationToken.None);
            return jpeg.ToArray();
        }
        finally
        {
            pixels.Dispose();
            jpeg.Dispose();
        }
    }

    /// <summary>Builds a page that draws a JPEG.</summary>
    /// <param name="jpeg">The JPEG file.</param>
    /// <returns>The PDF.</returns>
    private static byte[] Page(byte[] jpeg) =>
        OptimizerSamples.ImagePage(OptimizerSamples.ImageObject(jpeg, Side, Side, JpegEntries), DrawPoints);

    /// <summary>Gets the first page's /Im1.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The image stream.</returns>
    private static PdfStream FirstImage(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        return PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im1"u8))!;
    }
}
