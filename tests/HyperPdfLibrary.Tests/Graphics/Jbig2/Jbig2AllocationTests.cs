// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Checks that JBIG2 decoding allocates a small fixed amount per stream, not per pixel, symbol or instance.</summary>
[NotInParallel]
public sealed class Jbig2AllocationTests
{
    /// <summary>The decodes made before measuring, so pools fill and tiering settles.</summary>
    private const int Warmup = 3;

    /// <summary>The measured decodes.</summary>
    private const int Runs = 40;

    /// <summary>
    /// The most managed bytes one decode may allocate. A warmed decode allocates under 3 KB of per-segment bookkeeping;
    /// per-pixel or per-symbol allocation would cost megabytes on these pages.
    /// </summary>
    private const long MaxBytes = 16 * 1024;

    /// <summary>Real pages with thousands of symbols and instances, and millions of pixels, allocate only bookkeeping.</summary>
    /// <param name="name">The sample's name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress))]
    [Arguments(nameof(Jbig2Samples.RealGoogleBooks))]
    [Arguments(nameof(Jbig2Samples.RealInternetArchiveGeneric))]
    [Arguments(nameof(Jbig2Samples.SymbolsHuffmanRefinementAggregate))]
    [Arguments(nameof(Jbig2Samples.HalftoneArith))]
    public async Task DecodeAllocatesOnlyBookkeeping(string name)
    {
        var allocated = Measure(Jbig2Samples.Get(name));

        await Assert.That(allocated).IsLessThan(MaxBytes);
    }

    /// <summary>Measures the bytes one decode allocates after warm-up.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The bytes allocated.</returns>
    private static long Measure(Jbig2Sample sample)
    {
        // This fixture owns its bounded cache, so earlier decodes cannot affect its retained buffers.
        var scratchPools = new ScratchPools();
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        for (var i = 0; i < Warmup; i++)
        {
            _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows, scratchPools, CancellationToken.None);
        }

        // Small buffers still use ArrayPool, whose Gen2 trimming can cause a rent miss; the smallest run distinguishes
        // that miss from allocation repeated on every decode.
        var smallest = long.MaxValue;
        for (var i = 0; i < Runs && smallest >= MaxBytes; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows, scratchPools, CancellationToken.None);
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }
}
