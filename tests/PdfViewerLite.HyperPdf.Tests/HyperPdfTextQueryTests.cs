// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks text queries remain independent across document lifetimes.</summary>
public sealed class HyperPdfTextQueryTests
{
    /// <summary>Closing one document leaves another document's text queries usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextQueriesKeepSeparateDocumentsUsable()
    {
        using var first = new HyperPdfTestDocument(TestPdf.CreateTagged());
        using var second = new HyperPdfTestDocument(TestPdf.CreateTagged());
        var firstBounds = new List<PageRect>();
        var secondBounds = new List<PageRect>();
        first.Document.GetTextBounds(0, 0, 1, firstBounds);
        second.Document.GetTextBounds(0, 0, 1, secondBounds);

        var firstMatches = new List<TextMatch>();
        var secondMatches = new List<TextMatch>();
        first.Document.Find(0, TestPdf.TaggedHeading, default, firstMatches);
        second.Document.Find(0, TestPdf.TaggedHeading, default, secondMatches);

        await Assert.That(firstBounds.Count).IsGreaterThan(0);
        await Assert.That(secondBounds.Count).IsEqualTo(firstBounds.Count);
        await Assert.That(firstMatches.Count).IsGreaterThan(0);
        await Assert.That(secondMatches.Count).IsEqualTo(firstMatches.Count);
        first.Document.Dispose();
        firstBounds.Clear();
        firstMatches.Clear();
        first.Document.GetTextBounds(0, 0, 1, firstBounds);
        first.Document.Find(0, TestPdf.TaggedHeading, default, firstMatches);
        await Assert.That(firstBounds).IsEmpty();
        await Assert.That(firstMatches).IsEmpty();

        secondBounds.Clear();
        secondMatches.Clear();
        second.Document.GetTextBounds(0, 0, 1, secondBounds);
        second.Document.Find(0, TestPdf.TaggedHeading, default, secondMatches);
        await Assert.That(secondBounds.Count).IsGreaterThan(0);
        await Assert.That(secondMatches.Count).IsGreaterThan(0);
    }
}
