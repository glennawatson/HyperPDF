// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks the viewer engine's bounded scratch cache across open tabs.</summary>
public sealed class ScratchCacheOwnershipTests
{
    /// <summary>One megabyte uses the bounded scratch pool.</summary>
    private const int Large = 1024 * 1024;

    /// <summary>One viewer engine shares one cache across its documents and owns it after they close.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultEngineSharesScratchCacheAcrossDocuments()
    {
        var bytes = TestPdf.Create(1);
        var engine = new HyperPdfEngine();
        ScratchPools shared;
        using (var second = new HyperPdfTestDocument(bytes, engine))
        {
            using (var first = new HyperPdfTestDocument(bytes, engine))
            {
                shared = first.Document.Document.Objects.ScratchPools;
                await Assert.That(ReferenceEquals(shared, second.Document.Document.Objects.ScratchPools)).IsTrue();
                await Assert.That(shared.Budget).IsEqualTo(ScratchPools.DefaultBudget);
                var pool = shared.Get<byte>();
                pool.Return(pool.Rent(Large));
            }

            await Assert.That(shared.RetainedBytes).IsGreaterThan(0);
        }

        await Assert.That(shared.RetainedBytes).IsGreaterThan(0);
        using var separate = new HyperPdfTestDocument(bytes, new HyperPdfEngine());
        await Assert.That(ReferenceEquals(shared, separate.Document.Document.Objects.ScratchPools)).IsFalse();
        shared.Trim();
    }
}
