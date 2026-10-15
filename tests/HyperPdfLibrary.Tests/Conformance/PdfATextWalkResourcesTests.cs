// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks the resource dictionaries a text walk records: each once, in the order first read.</summary>
public sealed class PdfATextWalkResourcesTests
{
    /// <summary>More dictionaries than the walk compares a new one against.</summary>
    private const int Many = 20;

    /// <summary>The number of dictionaries read by the second test.</summary>
    private const int Both = 2;

    /// <summary>Repeats and interleaved repeats are dropped and the first-read order is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DistinctResourcesKeepTheFirstReadOrder()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        var state = new PdfATextWalkState(new(document, PdfALevel.PdfA2U, CancellationToken.None));
        var dictionaries = new PdfDictionary[Many];
        for (var i = 0; i < Many; i++)
        {
            dictionaries[i] = new(null);
        }

        foreach (var dictionary in dictionaries)
        {
            state.AddResources(dictionary);
            state.AddResources(dictionary);
        }

        // Read every dictionary again, far enough apart that the recent-repeat check has forgotten them.
        foreach (var dictionary in dictionaries)
        {
            state.AddResources(dictionary);
        }

        var distinct = state.GetDistinctResources();

        await Assert.That(distinct.Count).IsEqualTo(Many);
        await Assert.That(distinct.Zip(dictionaries).All(static pair => ReferenceEquals(pair.First, pair.Second))).IsTrue();
    }

    /// <summary>A later call sees resources read after an earlier one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DistinctResourcesIncludeLaterReads()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        var state = new PdfATextWalkState(new(document, PdfALevel.PdfA2U, CancellationToken.None));
        var first = new PdfDictionary(null);
        var second = new PdfDictionary(null);

        state.AddResources(first);
        var before = state.GetDistinctResources();
        state.AddResources(second);
        var after = state.GetDistinctResources();

        await Assert.That(before.Count).IsEqualTo(1);
        await Assert.That(after.Count).IsEqualTo(Both);
        await Assert.That(after.Contains(first)).IsTrue();
        await Assert.That(after.Contains(second)).IsTrue();
    }
}
