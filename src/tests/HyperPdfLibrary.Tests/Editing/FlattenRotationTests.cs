// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that flattening a rotated page looks the same, and that the rotation can be turned into the content.</summary>
public sealed class FlattenRotationTests
{
    /// <summary>The link's rectangle before the rotation is baked.</summary>
    private static readonly PdfRectangle OriginalLinkRect = new(60, 10, 100, 30);

    /// <summary>A flatten of a rotated page leaves the page looking the same.</summary>
    /// <param name="rotation">The page's /Rotate.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(90)]
    [Arguments(180)]
    [Arguments(270)]
    public async Task RotatedPageLooksTheSame(int rotation)
    {
        var file = FlattenTestDocuments.Rotated(rotation);
        var before = FlattenTestDocuments.Render(file, PdfRenderFlags.None);
        using var document = PdfDocumentReader.Open(file, null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var after = FlattenTestDocuments.Render(FlattenTestDocuments.SaveCompact(document), PdfRenderFlags.None);

        await Assert.That(PdfDocumentPages.GetPage(document, 0).Rotation).IsEqualTo(rotation);
        await Assert.That(FlattenTestDocuments.CountColour(after, FlattenTestDocuments.Blue)).IsEqualTo(FlattenTestDocuments.BluePixels);
        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));
    }

    /// <summary>Baking the rotation ends with an upright page that looks the same, and the annotations that stay move with it.</summary>
    /// <param name="rotation">The page's /Rotate.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(90)]
    [Arguments(180)]
    [Arguments(270)]
    public async Task BakedRotationLooksTheSame(int rotation)
    {
        var file = FlattenTestDocuments.Rotated(rotation);
        var before = FlattenTestDocuments.Render(file, PdfRenderFlags.None);
        using var document = PdfDocumentReader.Open(file, null);
        var original = PdfDocumentPages.GetPage(document, 0);
        var report = PdfDocumentFlattening.Flatten(document, [0], new PdfFlattenOptions { Targets = PdfFlattenTargets.MarkupOnly, BakeRotation = true });
        var saved = FlattenTestDocuments.SaveCompact(document);
        var after = FlattenTestDocuments.Render(saved, PdfRenderFlags.None);
        var page = PdfDocumentPages.GetPage(document, 0);
        var link = page.Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;

        await Assert.That(report.RotationsBaked).IsEqualTo(1);
        await Assert.That(page.Rotation).IsEqualTo(0);
        await Assert.That(page.Width).IsEqualTo(original.Width);
        await Assert.That(page.Height).IsEqualTo(original.Height);
        await Assert.That(page.CropBox.Left).IsEqualTo(0);
        await Assert.That(page.CropBox.Bottom).IsEqualTo(0);
        await Assert.That(link.IsName(KnownName.Subtype, KnownName.Link)).IsTrue();
        await Assert.That(link.TryGetRectangle(KnownName.Rect, out var rect)).IsTrue();
        await Assert.That(FlattenTestDocuments.CountColour(after, FlattenTestDocuments.Blue)).IsEqualTo(FlattenTestDocuments.BluePixels);
        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));
        await Assert.That(page.ToViewerRectangle(rect)).IsEqualTo(original.ToViewerRectangle(OriginalLinkRect));
    }

    /// <summary>A page that is not rotated is left alone when rotation is baked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UprightPageIsNotChangedByBaking()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.Rotated(0), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], new PdfFlattenOptions { Targets = PdfFlattenTargets.WidgetsOnly, BakeRotation = true });

        await Assert.That(report.RotationsBaked).IsEqualTo(0);
        await Assert.That(report.PagesChanged).IsEqualTo(0);
    }
}
