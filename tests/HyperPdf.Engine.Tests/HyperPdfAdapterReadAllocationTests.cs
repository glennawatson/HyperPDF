// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Counts exact managed allocations in warmed adapter render, text and link reads.</summary>
[NotInParallel]
public sealed class HyperPdfAdapterReadAllocationTests
{
    /// <summary>The pages in the adapter read benchmark fixture.</summary>
    private const int Pages = 50;

    /// <summary>The page used for the measured workload.</summary>
    private const int PageIndex = Pages - 1;

    /// <summary>The rendered tile's edge.</summary>
    private const int Edge = 256;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The calls warming the renderer, reader caches and JIT paths.</summary>
    private const int Warmup = 256;

    /// <summary>The measured calls.</summary>
    private const int Measured = 1024;

    /// <summary>Repeated reads through the page-access guard allocate zero managed bytes once warm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmRenderTextAndLinksAllocateZeroBytes()
    {
        using var document = new HyperPdfDocument(SkiaTestDocuments.Open(TestPdf.Create(Pages)), "adapter-allocation.pdf");
        var pixels = new byte[Edge * Edge * PixelBytes];
        var expected = Read(document, pixels);
        for (var index = 0; index < Warmup; index++)
        {
            _ = Read(document, pixels);
        }

        var mismatches = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < Measured; index++)
        {
            if (Read(document, pixels) != expected)
            {
                mismatches++;
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(expected).IsGreaterThan(1);
        await Assert.That(mismatches).IsEqualTo(0);
        await Assert.That(allocated).IsEqualTo(0L);
    }

    /// <summary>Renders a warmed tile and reads cached text and links.</summary>
    /// <param name="document">The adapter.</param>
    /// <param name="pixels">The reusable target.</param>
    /// <returns>The render result and counts.</returns>
    /// <exception cref="InvalidOperationException">The tile cannot be rendered.</exception>
    private static int Read(HyperPdfDocument document, byte[] pixels)
    {
        var info = new PageRenderInfo(PageIndex, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);
        if (!HyperPdf.Engine.HyperPdfRendering.Render(document, info, new(pixels, Edge, Edge, Edge * PixelBytes)))
        {
            throw new InvalidOperationException("The allocation workload's tile could not be rendered.");
        }

        return 1 + HyperPdf.Engine.HyperPdfText.GetCharacterCount(document, PageIndex) + HyperPdf.Engine.HyperPdfNavigation.GetLinks(document, PageIndex).Count;
    }
}
