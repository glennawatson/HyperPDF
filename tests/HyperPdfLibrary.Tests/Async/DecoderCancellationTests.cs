// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Tests.Graphics.Jpeg;
using HyperPdfLibrary.Tests.Graphics.Jpx;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>Checks that image decoders stop on explicit tokens and finish normally on live ones.</summary>
[NotInParallel]
public sealed class DecoderCancellationTests
{
    /// <summary>The components of the RGB sample.</summary>
    private const int RgbComponents = 3;

    /// <summary>A JBIG2 decode with a cancelled token throws at the first segment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Jbig2StopsOnCancel()
    {
        var sample = Jbig2Samples.TextArith;

        await Assert.That(Jbig2Cancelled(sample, true)).IsTrue();
        await Assert.That(Jbig2Cancelled(sample, false)).IsFalse();
    }

    /// <summary>A JPEG decode with a cancelled token throws inside the scan.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegStopsOnCancel()
    {
        await Assert.That(JpegCancelled(true)).IsTrue();
        await Assert.That(JpegCancelled(false)).IsFalse();
    }

    /// <summary>A JPEG 2000 decode with a cancelled token throws at the first tile or code-block.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpxStopsOnCancel() => await Assert.That(JpxCancelled(true)).IsTrue();

    /// <summary>Runs a JBIG2 decode under a token.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="cancelled">Whether the token is cancelled.</param>
    /// <returns><see langword="true"/> when the decode threw OperationCanceledException.</returns>
    private static bool Jbig2Cancelled(Jbig2Sample sample, bool cancelled)
    {
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        try
        {
            _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows, new(cancelled));
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Runs a JPEG decode under a token.</summary>
    /// <param name="cancelled">Whether the token is cancelled.</param>
    /// <returns><see langword="true"/> when the decode threw OperationCanceledException.</returns>
    private static bool JpegCancelled(bool cancelled)
    {
        var output = new byte[JpegFixtures.ProgressiveRgbWidth * JpegFixtures.ProgressiveRgbHeight * RgbComponents];
        try
        {
            _ = JpegDecoder.TryDecode(JpegFixtures.ProgressiveRgb, true, output, new(cancelled));
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Runs a JPEG 2000 decode under a token.</summary>
    /// <param name="cancelled">Whether the token is cancelled.</param>
    /// <returns><see langword="true"/> when the decode threw OperationCanceledException.</returns>
    private static bool JpxCancelled(bool cancelled)
    {
        try
        {
            _ = JpxDecoderTests.DecodePlanes(Convert.FromBase64String(JpxFixtures.Reversible), new(cancelled));
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }
}
