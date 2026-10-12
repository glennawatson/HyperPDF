// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests that metadata edits keep a PDF/A claim valid: the PDF/A-4 /Info rules (#180) and the PDF/A-1 to 3 crosswalk (#154).</summary>
public sealed class PdfAMetadataEditTests
{
    /// <summary>The title the edits set.</summary>
    private const string NewTitle = "Edited title";

    /// <summary>The author placed in /Info only.</summary>
    private const string InfoAuthor = "Ana Author";

    /// <summary>Gets the modification date the edits set.</summary>
    private static DateTimeOffset Modified => DateTimeOffset.Parse("2026-10-12T10:00:00+11:00", CultureInfo.InvariantCulture);

    /// <summary>A PDF/A-4 title edit goes to XMP only and adds no /Info.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA4TitleEditWritesNoInfo()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A4), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = NewTitle, Author = InfoAuthor, Modified = Modified });
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        await Assert.That(saved.Objects.Trailer.ContainsKey(KnownName.Info)).IsFalse();
        await Assert.That(PdfDocumentMetadata.GetXmp(saved)?.Title).IsEqualTo(NewTitle);
        await Assert.That(PdfDocumentMetadata.GetXmp(saved)?.Creators).IsEquivalentTo([InfoAuthor]);
    }

    /// <summary>A PDF/A-4 file with /PieceInfo keeps an /Info that holds only the modification date.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA4WithPieceInfoKeepsOnlyModDate()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A4), null);
        var catalog = document.Catalog.Clone();
        catalog.Set(document.Objects.Names.Intern("PieceInfo"u8), PdfValue.FromDictionary(new(document.Objects)));
        var transaction = PdfDocumentEditing.BeginEdit(document, "PieceInfo");
        transaction.Replace(document.Objects.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
        transaction.Commit();
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = NewTitle, Modified = Modified });
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var info = saved.Objects.Trailer.GetDictionary(KnownName.Info);
        await Assert.That(info?.Count).IsEqualTo(1);
        await Assert.That(PdfDocumentMetadata.GetInfo(saved).Modified).IsEqualTo(Modified);
        await Assert.That(PdfDocumentMetadata.GetXmp(saved)?.Title).IsEqualTo(NewTitle);
    }

    /// <summary>A PDF/A-1 edit brings every /Info entry into the XMP, including ones the edit did not touch.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA1EditMatchesEveryInfoEntryInXmp()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        var info = new PdfDictionary(document.Objects);
        info.Set(KnownName.Author, PdfValue.FromString("Ana Author"u8.ToArray()));
        var transaction = PdfDocumentEditing.BeginEdit(document, "Info");
        transaction.SetTrailerEntry(KnownName.Info, PdfValue.FromReference(transaction.Add(PdfValue.FromDictionary(info))));
        transaction.Commit();
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = NewTitle });
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        var xmp = PdfDocumentMetadata.GetXmp(saved);
        await Assert.That(xmp?.Title).IsEqualTo(NewTitle);
        await Assert.That(xmp?.Creators).IsEquivalentTo([InfoAuthor]);
        await Assert.That(PdfDocumentMetadata.GetInfo(saved).Title).IsEqualTo(NewTitle);
    }

    /// <summary>An edit the XMP cannot take is refused for a claimed file, and nothing changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimedFileRefusesAnEditItsXmpCannotTake()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A2B), null);
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        var stream = document.Catalog.GetStream(KnownName.Metadata)!;
        var broken = Encoding.UTF8.GetString(stream.DecodeToArray(CancellationToken.None)).Replace("</x:xmpmeta>", "</x:broken>", StringComparison.Ordinal);
        StoreEditing.Replace(document.Objects, raw.AsReference(), PdfValue.FromStream(new(stream.Dictionary.Clone(), Encoding.UTF8.GetBytes(broken))));
        var before = StoreEditing.GetEditedObjects(document.Objects, out _).Length;
        var error = Assert.Throws<PdfException>(() => PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = NewTitle }));
        await Assert.That(error?.Message).Contains("PDF/A-2");
        await Assert.That(StoreEditing.GetEditedObjects(document.Objects, out _).Length).IsEqualTo(before);
        await Assert.That(document.Objects.Trailer.ContainsKey(KnownName.Info)).IsFalse();
    }

    /// <summary>A file without a claim is edited as before: /Info and XMP both change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileStillEditsInfo()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = NewTitle });
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(NewTitle);
    }

    /// <summary>The optimiser keeps a PDF/A-4 title in XMP and adds no /Info for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserLeavesPdfA4InfoAlone()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A4), null);
        await using var output = new MemoryStream();
        _ = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.KeepQuality);
        using var optimised = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(optimised.Objects.Trailer.ContainsKey(KnownName.Info)).IsFalse();
        await Assert.That(PdfDocumentMetadata.GetXmp(optimised)?.Title).IsEqualTo(PdfAFixtures.Title);
    }
}
