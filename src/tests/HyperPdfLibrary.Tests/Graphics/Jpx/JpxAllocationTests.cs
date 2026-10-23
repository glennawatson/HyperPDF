// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Checks that a warmed JPEG 2000 decode allocates its pixels and its parsed header, and nothing for the tile streams, strips or thread sharing.</summary>
[NotInParallel]
public sealed class JpxAllocationTests
{
    /// <summary>The bits of a sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The decodes made before measuring, so the cache fills and tiering settles.</summary>
    private const int Warmup = 3;

    /// <summary>The measured decodes.</summary>
    private const int Runs = 20;

    /// <summary>
    /// The most managed bytes besides the pixels that a warmed decode may allocate: the parsed header model is under
    /// 2 KB, while the tile streams, band rows and thread sharing the decoder used to rebuild cost over 100 KB.
    /// </summary>
    private const long MaxExtraBytes = 4 * 1024;

    /// <summary>A warmed decode of the 9/7 fixture allocates its pixels and a small header model.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmDecodeAllocatesPixelsAndHeaderOnly()
    {
        var data = Convert.FromBase64String(JpxFixtures.Irreversible);
        var header = new ImageHeader(JpxFixtures.IrreversibleWidth, JpxFixtures.IrreversibleHeight, SampleBits, false, false, PdfColorSpace.DeviceRgb, null, null);
        var scratch = new ScratchPools();
        for (var i = 0; i < Warmup; i++)
        {
            _ = JpxImageDecoder.Decode(header, data, 0, 0, scratch, CancellationToken.None);
        }

        var smallest = long.MaxValue;
        for (var i = 0; i < Runs; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            _ = JpxImageDecoder.Decode(header, data, 0, 0, scratch, CancellationToken.None);
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        var extra = smallest - ((long)JpxFixtures.IrreversibleWidth * JpxFixtures.IrreversibleHeight * BytesPerPixel);

        await Assert.That(extra).IsLessThan(MaxExtraBytes);
    }
}
