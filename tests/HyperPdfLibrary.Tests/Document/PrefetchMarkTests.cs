// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>A load-ahead walk is remembered per kind, so a walk of one kind does not skip the walk of another.</summary>
public sealed class PrefetchMarkTests
{
    /// <summary>A cache generation.</summary>
    private const long Generation = 5;

    /// <summary>A mark knows only the kind of walk that was recorded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkKnowsOnlyTheKindRecorded()
    {
        var mark = new PdfPrefetchMark();
        mark.SetLoadedAt(PdfPrefetchKind.Page, Generation);

        await Assert.That(mark.IsLoadedAt(PdfPrefetchKind.Page, Generation)).IsTrue();
        await Assert.That(mark.IsLoadedAt(PdfPrefetchKind.Separation, Generation)).IsFalse();
        await Assert.That(mark.IsLoadedAt(PdfPrefetchKind.PageTree, Generation)).IsFalse();
        await Assert.That(mark.IsLoadedAt(PdfPrefetchKind.Outline, Generation)).IsFalse();
    }

    /// <summary>A mark made for a generation does not hold for another.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkHoldsOnlyForTheGenerationRecorded()
    {
        var mark = new PdfPrefetchMark();
        mark.SetLoadedAt(PdfPrefetchKind.Separation, Generation);

        await Assert.That(mark.IsLoadedAt(PdfPrefetchKind.Separation, Generation + 1)).IsFalse();
    }
}
