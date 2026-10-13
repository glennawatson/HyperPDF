// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Tests.Graphics;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Renders pages with CMYK ICC images in PDFium (Little CMS) and HyperPDF and compares the pixels. The profiles come from
/// <see cref="IccCmykFixture"/>, so the test needs no profile installed on the machine and runs on every platform.
/// </summary>
public sealed class IccParityTests
{
    /// <summary>The side of the test image in pixels.</summary>
    private const int ImageSize = 128;

    /// <summary>The side of one patch of the test image, which holds a full cyan and magenta ramp.</summary>
    private const int PatchSize = 64;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The bytes in a CMYK pixel.</summary>
    private const int CmykBytes = 4;

    /// <summary>The largest mean absolute difference per channel, from 0 to 255, that the issue allows.</summary>
    private const double MeanThreshold = 3.0;

    /// <summary>The per-channel difference above which a pixel counts as different.</summary>
    private const int PixelTolerance = 8;

    /// <summary>The largest share of pixels that may differ by more than <see cref="PixelTolerance"/>.</summary>
    private const double DifferingShare = 0.01;

    /// <summary>The largest sample value.</summary>
    private const double MaxSample = 255.0;

    /// <summary>The steps of black ink between patches, as a fraction of full ink.</summary>
    private const double BlackStep = 1.0 / 3.0;

    /// <summary>The index of the yellow channel in a CMYK pixel, which also counts the patches in a row.</summary>
    private const int YellowChannel = 2;

    /// <summary>The index of the black channel in a CMYK pixel.</summary>
    private const int BlackChannel = 3;

    /// <summary>The page index of the only page.</summary>
    private const int FirstPage = 0;

    /// <summary>A CMYK image coloured through the 'mft2' fixture matches PDFium within the issue's limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykImageThroughMft2ProfileMatchesPdfium()
    {
        var (mean, share) = Compare(IccCmykFixture.BuildMft2(), "OFF");

        await Assert.That(mean).IsLessThanOrEqualTo(MeanThreshold);
        await Assert.That(share).IsLessThanOrEqualTo(DifferingShare);
    }

    /// <summary>A CMYK image coloured through the v4 'mAB ' fixture matches PDFium within the issue's limits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykImageThroughMabProfileMatchesPdfium()
    {
        var (mean, share) = Compare(IccCmykFixture.BuildMab(), "ON");

        await Assert.That(mean).IsLessThanOrEqualTo(MeanThreshold);
        await Assert.That(share).IsLessThanOrEqualTo(DifferingShare);
    }

    /// <summary>Builds the test image: ramps of cyan and magenta with yellow along the diagonal, in patches of rising black.</summary>
    /// <returns>The CMYK samples.</returns>
    private static byte[] BuildImage()
    {
        var data = new byte[ImageSize * ImageSize * CmykBytes];
        for (var y = 0; y < ImageSize; y++)
        {
            for (var x = 0; x < ImageSize; x++)
            {
                var px = x % PatchSize;
                var py = y % PatchSize;
                var patch = (x / PatchSize) + (YellowChannel * (y / PatchSize));
                var offset = ((y * ImageSize) + x) * CmykBytes;
                data[offset] = Sample(px / (double)(PatchSize - 1));
                data[offset + 1] = Sample(py / (double)(PatchSize - 1));
                data[offset + YellowChannel] = Sample((px + py) / (double)(YellowChannel * (PatchSize - 1)));
                data[offset + BlackChannel] = Sample(patch * BlackStep);
            }
        }

        return data;
    }

    /// <summary>Converts a fraction of full ink to a sample.</summary>
    /// <param name="fraction">The fraction, from 0 to 1.</param>
    /// <returns>The sample.</returns>
    private static byte Sample(double fraction) => (byte)Math.Round(Math.Min(fraction, 1.0) * MaxSample);

    /// <summary>Builds a page with the image coloured through a profile.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <param name="compensation">The /UseBlackPtComp value to match what Little CMS does for the profile version.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] BuildPdf(byte[] profile, string compensation)
    {
        var pdf = new RenderTestPdf(ImageSize, ImageSize);
        var icc = pdf.AddStream("/N 4 /Alternate /DeviceCMYK", profile);
        var image = pdf.AddStream(
            string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width {ImageSize} /Height {ImageSize} /ColorSpace [/ICCBased {icc} 0 R] /BitsPerComponent 8"),
            BuildImage());

        // PDFium always converts with the perceptual intent, and Little CMS compensates black points for v4 profiles only,
        // so the page asks for the same. ISO 32000-2 8.6.5.8 and 8.6.5.9 let a page choose both; the default would read the
        // relative colorimetric table.
        pdf.Content = string.Create(CultureInfo.InvariantCulture, $"/Perceptual ri /Plain gs q {ImageSize} 0 0 {ImageSize} 0 0 cm /Im Do Q");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /Plain << /UseBlackPtComp /{compensation} >> >> /XObject << /Im {image} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Renders the page with both engines and compares the pixels.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <param name="compensation">The /UseBlackPtComp value.</param>
    /// <returns>The mean absolute difference per channel and the share of pixels whose channels differ by more than the tolerance.</returns>
    private static ComparisonResult Compare(byte[] profile, string compensation)
    {
        using var input = new HyperPdfTestDocument(BuildPdf(profile, compensation));
        var info = new PageRenderInfo(FirstPage, 1, PageRotation.None, 0, 0, RenderFlags.None);
        var expected = PdfiumReferenceRendering.Render(input.Pdf, info).Pixels.ToArray();
        var actual = Render(input.Document, info);
        long total = 0;
        var differing = 0;
        for (var i = 0; i < expected.Length; i += BytesPerPixel)
        {
            var worst = 0;
            for (var c = 0; c < BytesPerPixel - 1; c++)
            {
                var difference = Math.Abs(expected[i + c] - actual[i + c]);
                total += difference;
                worst = Math.Max(worst, difference);
            }

            if (worst > PixelTolerance)
            {
                differing++;
            }
        }

        var pixelCount = expected.Length / (double)BytesPerPixel;
        return new(total / (pixelCount * (BytesPerPixel - 1)), differing / pixelCount);
    }

    /// <summary>Renders the page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The tile request.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(IDocument document, in PageRenderInfo info)
    {
        var pixels = new byte[ImageSize * ImageSize * BytesPerPixel];
        var rendered = document.Render(info, new(pixels, ImageSize, ImageSize, ImageSize * BytesPerPixel));
        return rendered ? pixels : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>The result of comparing two renderings.</summary>
    /// <param name="Mean">The mean absolute difference per channel, from 0 to 255.</param>
    /// <param name="Share">The share of pixels whose largest channel difference is above the tolerance.</param>
    private readonly record struct ComparisonResult(double Mean, double Share);
}
