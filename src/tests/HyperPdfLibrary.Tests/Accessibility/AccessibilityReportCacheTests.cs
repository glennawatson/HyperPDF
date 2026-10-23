// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>
/// Tests that the report keeps its font and XMP results with the document, gives the same findings again on an unchanged
/// document, and reads the document again after an edit.
/// </summary>
public sealed class AccessibilityReportCacheTests
{
    /// <summary>The first character code the test font defines.</summary>
    private const int FirstCode = 32;

    /// <summary>The number of character codes the test font defines, from the space to the letter z.</summary>
    private const int CodeCount = 91;

    /// <summary>The test font's object number in a <see cref="TaggedPdfBuilder"/> file.</summary>
    private const int FontObject = 4;

    /// <summary>The revision text an edit writes.</summary>
    private const string EditedRevision = "2024";

    /// <summary>Gets a ToUnicode map that gives the letter L the value U+0000 and every other code its own character.</summary>
    private static ReadOnlySpan<byte> InvalidMap => """
        /CIDInit /ProcSet findresource begin 12 dict begin begincmap
        /CMapName /Adobe-Identity-UCS def /CMapType 2 def
        1 begincodespacerange <00> <FF> endcodespacerange
        2 beginbfrange <20> <4B> <0020> <4D> <7A> <004D> endbfrange
        1 beginbfchar <4C> <0000> endbfchar
        endcmap CMapName currentdict /CMap defineresource pop end end
        """u8;

    /// <summary>Gets a ToUnicode map that gives every code from the space to the letter z its own character.</summary>
    private static ReadOnlySpan<byte> ValidMap => """
        /CIDInit /ProcSet findresource begin 12 dict begin begincmap
        /CMapName /Adobe-Identity-UCS def /CMapType 2 def
        1 begincodespacerange <00> <FF> endcodespacerange
        1 beginbfrange <20> <7A> <0020> endbfrange
        endcmap CMapName currentdict /CMap defineresource pop end end
        """u8;

    /// <summary>A second report on an unchanged document has the same findings as the first, font and metadata findings included.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondReportOnUnchangedDocumentHasTheSameFindings(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Xmp = null, Font = static pdf => Type3(pdf, string.Empty) }));

        var first = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var second = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(first.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(1);
        await Assert.That(first.GetCount(PdfAccessibilityCode.NoMetadata)).IsEqualTo(1);
        await Assert.That(second.Findings.SequenceEqual(first.Findings)).IsTrue();
        await Assert.That(second.Counts.SequenceEqual(first.Counts)).IsTrue();
        await Assert.That(second.Claim).IsEqualTo(first.Claim);
        await Assert.That(second.HasTitle).IsEqualTo(first.HasTitle);
    }

    /// <summary>A second report on an unchanged document that has no font or metadata findings stays free of them.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondReportOnCleanDocumentStaysClean(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var first = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var second = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(first.TotalCount).IsEqualTo(0);
        await Assert.That(second.TotalCount).IsEqualTo(0);
        await Assert.That(second.Claim.Part).IsEqualTo(1);
        await Assert.That(second.HasTitle).IsTrue();
    }

    /// <summary>The document retains its report snapshot until an edit changes its metadata.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResultsStayWithTheDocumentUntilAnEdit(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));
        var first = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var second = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(second).IsSameReferenceAs(first);

        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Edited" });

        var edited = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(edited).IsNotSameReferenceAs(first);
    }

    /// <summary>Removing a font's Unicode map adds the finding on the next report, and putting the font back removes it.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingTheUnicodeMapAddsTheFontFinding(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Font = static pdf => Type3(pdf, MapEntry(pdf, ValidMap)) }));
        var fontId = new PdfObjectId(FontObject, 0);
        var original = StoreReading.GetObject(document.Objects, fontId).AsDictionary()!;
        var withoutMap = original.Clone();
        _ = withoutMap.Remove(KnownName.ToUnicode);

        var before = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        Replace(document, fontId, PdfValue.FromDictionary(withoutMap));
        var removed = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        Replace(document, fontId, PdfValue.FromDictionary(original));
        var restored = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(before.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(0);
        await Assert.That(removed.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(1);
        await Assert.That(restored.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(0);
    }

    /// <summary>Adding a Unicode map to a font that had none removes the finding on the next report.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddingTheUnicodeMapRemovesTheFontFinding(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Font = static pdf => Type3(pdf, string.Empty) }));
        var fontId = new PdfObjectId(FontObject, 0);
        var mapId = StoreEditing.Add(document.Objects, PdfValue.FromStream(new(new(document.Objects), ValidMap.ToArray())));
        var withMap = StoreReading.GetObject(document.Objects, fontId).AsDictionary()!.Clone();
        withMap.Set(KnownName.ToUnicode, PdfValue.FromReference(mapId));

        var before = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        Replace(document, fontId, PdfValue.FromDictionary(withMap));
        var after = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(before.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(1);
        await Assert.That(after.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(0);
    }

    /// <summary>Editing the Unicode map stream itself changes the finding on the next report, in both directions.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditingTheUnicodeMapStreamChangesTheFontFinding(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Font = static pdf => Type3(pdf, MapEntry(pdf, ValidMap)) }));
        var font = StoreReading.GetObject(document.Objects, new(FontObject, 0)).AsDictionary()!;
        var mapId = font.GetRaw(KnownName.ToUnicode).AsReference();

        var before = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        Replace(document, mapId, PdfValue.FromStream(new(new(document.Objects), InvalidMap.ToArray())));
        var invalid = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        Replace(document, mapId, PdfValue.FromStream(new(new(document.Objects), ValidMap.ToArray())));
        var repaired = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(before.GetCount(PdfAccessibilityCode.FontUnicodeMapInvalid)).IsEqualTo(0);
        await Assert.That(invalid.GetCount(PdfAccessibilityCode.FontUnicodeMapInvalid)).IsEqualTo(1);
        await Assert.That(repaired.GetCount(PdfAccessibilityCode.FontUnicodeMapInvalid)).IsEqualTo(0);
    }

    /// <summary>Removing the title through the metadata edit changes the next report; setting it again restores it.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditingTheTitleChangesTheReport(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var before = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = string.Empty });
        var removed = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Another title" });
        var restored = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(before.HasTitle).IsTrue();
        await Assert.That(before.GetCount(PdfAccessibilityCode.NoTitle)).IsEqualTo(0);
        await Assert.That(removed.HasTitle).IsFalse();
        await Assert.That(removed.GetCount(PdfAccessibilityCode.NoTitle)).IsEqualTo(1);
        await Assert.That(restored.HasTitle).IsTrue();
        await Assert.That(restored.GetCount(PdfAccessibilityCode.NoTitle)).IsEqualTo(0);
    }

    /// <summary>Replacing the XMP packet changes the claimed part and revision on the next report.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditingTheClaimChangesTheReport(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Xmp = AccessibilityPdfs.Packet(1, true, "2014") }));
        var metadataId = document.Catalog.GetRaw(KnownName.Metadata).AsReference();

        var before = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        ReplacePacket(document, metadataId, AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, false, EditedRevision));
        var after = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(before.Claim.Part).IsEqualTo(1);
        await Assert.That(before.Claim.Revision).IsEqualTo("2014");
        await Assert.That(before.HasTitle).IsTrue();
        await Assert.That(after.Claim.Part).IsEqualTo(AccessibilityPdfs.PartTwo);
        await Assert.That(after.Claim.Revision).IsEqualTo(EditedRevision);
        await Assert.That(after.HasTitle).IsFalse();
        await Assert.That(after.GetCount(PdfAccessibilityCode.ClaimPart2NotPdf2)).IsEqualTo(1);
    }

    /// <summary>Removing the catalog's XMP entry changes the next report to no metadata, no claim and no title.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingTheMetadataChangesTheReport(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));
        var catalogId = document.Objects.Trailer.GetRaw(KnownName.Root).AsReference();
        var withoutMetadata = document.Catalog.Clone();
        _ = withoutMetadata.Remove(KnownName.Metadata);

        var before = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        Replace(document, catalogId, PdfValue.FromDictionary(withoutMetadata));
        var after = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(before.GetCount(PdfAccessibilityCode.NoMetadata)).IsEqualTo(0);
        await Assert.That(before.Claim.Part).IsEqualTo(1);
        await Assert.That(after.GetCount(PdfAccessibilityCode.NoMetadata)).IsEqualTo(1);
        await Assert.That(after.Claim.Part).IsNull();
        await Assert.That(after.HasTitle).IsFalse();
    }

    /// <summary>Replaces an object in its own committed edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The object to replace.</param>
    /// <param name="value">The new value.</param>
    private static void Replace(PdfDocument document, PdfObjectId id, PdfValue value)
    {
        using var transaction = PdfDocumentEditing.BeginEdit(document, "Replace");
        transaction.Replace(id, value);
        transaction.Commit();
    }

    /// <summary>Replaces the XMP packet stream, keeping its dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The packet's object.</param>
    /// <param name="packet">The new packet.</param>
    private static void ReplacePacket(PdfDocument document, PdfObjectId id, string packet)
    {
        var stream = StoreReading.GetObject(document.Objects, id).AsStream()!;
        Replace(document, id, PdfValue.FromStream(new(stream.Dictionary.Clone(), Encoding.UTF8.GetBytes(packet))));
    }

    /// <summary>Adds a ToUnicode map stream to the builder.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="map">The map's text.</param>
    /// <returns>The font entry that refers to the map.</returns>
    private static string MapEntry(TaggedPdfBuilder pdf, ReadOnlySpan<byte> map) =>
        string.Create(CultureInfo.InvariantCulture, $"/ToUnicode {pdf.Add(MiniPdf.Stream(string.Empty, Encoding.ASCII.GetString(map)))} 0 R");

    /// <summary>Writes a Type 3 font whose glyph names have no standard Unicode value, so only a ToUnicode map gives its codes a value.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="extra">Extra font entries, such as <c>/ToUnicode</c>.</param>
    /// <returns>The font dictionary.</returns>
    private static string Type3(TaggedPdfBuilder pdf, string extra)
    {
        var glyph = pdf.Add(MiniPdf.Stream(string.Empty, "1000 0 0 0 750 750 d1 0 0 750 750 re f"));
        var names = string.Concat(Enumerable.Repeat("/zz ", CodeCount));
        var widths = string.Concat(Enumerable.Repeat("1000 ", CodeCount));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 750 750] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /zz {glyph} 0 R >> "
            + $"/Encoding << /Type /Encoding /Differences [{FirstCode} {names}] >> /FirstChar {FirstCode} /LastChar {FirstCode + CodeCount - 1} /Widths [{widths}] {extra} >>");
    }
}
