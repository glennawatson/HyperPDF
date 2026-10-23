// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Checks that a warmed JPEG encode reuses its working arrays, so it allocates only bookkeeping.</summary>
[NotInParallel]
public sealed class JpegEncoderAllocationTests
{
    /// <summary>The picture side in pixels; the working arrays are far larger than the small-object heap limit.</summary>
    private const int Side = 600;

    /// <summary>The JPEG quality.</summary>
    private const int Quality = 85;

    /// <summary>The encodes made before measuring.</summary>
    private const int Warmup = 3;

    /// <summary>The measured encodes.</summary>
    private const int Runs = 40;

    /// <summary>The most managed bytes a warmed encode may allocate; the arrays alone are over a megabyte.</summary>
    private const long MaxBytes = 16 * 1024;

    /// <summary>The starting capacity of the output buffer.</summary>
    private const int Capacity = 1 << 20;

    /// <summary>A warmed sequential, progressive and CMYK encode allocates only bookkeeping.</summary>
    /// <param name="format">The pixel format.</param>
    /// <param name="progressive">Whether the file is progressive.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpegPixelFormat.Rgb24, false)]
    [Arguments(PdfJpegPixelFormat.Rgb24, true)]
    [Arguments(PdfJpegPixelFormat.Gray8, false)]
    [Arguments(PdfJpegPixelFormat.Cmyk32, false)]
    public async Task WarmEncodeAllocatesOnlyBookkeeping(PdfJpegPixelFormat format, bool progressive)
    {
        var pixels = JpegEncodeSamples.Pixels(format, Side, Side);
        var request = new JpegEncodeRequest(Side, Side, format, new() { Quality = Quality, Progressive = progressive });
        var scratch = new ScratchPools();
        for (var i = 0; i < Warmup; i++)
        {
            _ = Encode(pixels, request, scratch);
        }

        // Small buffers still use ArrayPool, whose Gen2 trimming can cause a rent miss; the smallest run distinguishes
        // that miss from allocation repeated on every encode.
        var smallest = long.MaxValue;
        for (var i = 0; i < Runs && smallest >= MaxBytes; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            _ = Encode(pixels, request, scratch);
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        await Assert.That(smallest).IsLessThan(MaxBytes);
    }

    /// <summary>Encodes into a pooled buffer.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="request">The request.</param>
    /// <param name="scratch">The working array cache.</param>
    /// <returns>The encoded size.</returns>
    private static int Encode(byte[] pixels, in JpegEncodeRequest request, ScratchPools scratch)
    {
        var output = new PooledBuffer(Capacity);
        try
        {
            JpegEncoder.Encode(pixels, request, ref output, scratch, CancellationToken.None);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
