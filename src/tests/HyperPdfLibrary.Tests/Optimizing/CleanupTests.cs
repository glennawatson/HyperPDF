// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>The optional cleanup steps.</summary>
[NotInParallel]
public sealed class CleanupTests
{
    /// <summary>Every cleanup step removes its clutter and leaves the metadata and the drawn page alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesClutter()
    {
        var source = OptimizerSamples.Clutter();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality with { Cleanup = PdfCleanupItems.All });
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var names = document.Objects.Names;
        var page = PdfDocumentPages.GetPage(document, 0).Dictionary;
        var resources = PdfDocumentPages.GetPage(document, 0).Resources!;

        await Assert.That(page.ContainsKey(names.Intern("Thumb"u8))).IsFalse();
        await Assert.That(page.ContainsKey(PieceInfo(names))).IsFalse();
        await Assert.That(page.ContainsKey(KnownName.Annots)).IsFalse();
        await Assert.That(document.Catalog.ContainsKey(PieceInfo(names))).IsFalse();
        await Assert.That(resources.GetDictionary(KnownName.XObject)!.ContainsKey(names.Intern("Im1"u8))).IsTrue();
        await Assert.That(resources.GetDictionary(KnownName.XObject)!.ContainsKey(names.Intern("Im2"u8))).IsFalse();
        await Assert.That(resources.GetDictionary(KnownName.Font)!.Count).IsEqualTo(0);
        await Assert.That(PdfDocumentMetadata.GetXmp(document)?.Title).IsEqualTo("Clutter");
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>Without cleanup the thumbnail and private data stay.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanupIsOptIn()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.Clutter(), PdfOptimizeOptions.KeepQuality);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var names = document.Objects.Names;

        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary.ContainsKey(names.Intern("Thumb"u8))).IsTrue();
        await Assert.That(document.Catalog.ContainsKey(PieceInfo(names))).IsTrue();
    }

    /// <summary>Gets <c>/PieceInfo</c> in a document's name table.</summary>
    /// <param name="names">The name table.</param>
    /// <returns>The name.</returns>
    private static PdfName PieceInfo(PdfNameTable names) => names.Intern("PieceInfo"u8);
}
