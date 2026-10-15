// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentMetadataEditing.SetMetadata"/> and the XMP editor.</summary>
public sealed class MetadataEditTests
{
    /// <summary>Pages in the plain test document.</summary>
    private const int Pages = 2;

    /// <summary>The year used for dates.</summary>
    private const int Year = 2026;

    /// <summary>The month used for dates.</summary>
    private const int Month = 3;

    /// <summary>The day used for dates.</summary>
    private const int Day = 4;

    /// <summary>The hour used for dates.</summary>
    private const int Hour = 5;

    /// <summary>The time zone offset used for dates, in hours.</summary>
    private const int OffsetHours = 10;

    /// <summary>Gets the date written.</summary>
    private static DateTimeOffset Date => new(Year, Month, Day, Hour, 0, 0, TimeSpan.FromHours(OffsetHours));

    /// <summary>Direct store edits refresh reads without changing another document's snapshots.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DirectStoreEditsRefreshSnapshots()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        using var other = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var original = PdfDocumentMetadata.GetInfo(document);
        var otherInfo = PdfDocumentMetadata.GetInfo(other);
        var added = StoreEditing.Add(document.Objects, PdfValue.FromInteger(Pages));
        var afterAdd = PdfDocumentMetadata.GetInfo(document);
        await Assert.That(ReferenceEquals(original, afterAdd)).IsFalse();

        var infoId = document.Objects.Trailer.GetRaw(KnownName.Info).AsReference();
        StoreEditing.Replace(document.Objects, infoId, PdfValue.FromDictionary(new(document.Objects)));
        var afterReplace = PdfDocumentMetadata.GetInfo(document);
        await Assert.That(afterReplace.Title).IsNull();
        StoreEditing.Delete(document.Objects, added);
        var afterDelete = PdfDocumentMetadata.GetInfo(document);
        await Assert.That(ReferenceEquals(afterReplace, afterDelete)).IsFalse();
        await Assert.That(PdfDocumentMetadata.GetInfo(other)).IsSameReferenceAs(otherInfo);
        await Assert.That(otherInfo.Title).IsEqualTo(TestPdf.Title);
    }

    /// <summary>Cached information follows metadata edits and undo without changing an earlier snapshot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CachedInformationFollowsEditAndUndo()
    {
        const string changedTitle = "Changed";
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var before = PdfDocumentMetadata.GetInfo(document);
        _ = PdfDocumentMetadata.GetResolvedInfo(document);
        _ = PdfDocumentMetadata.GetXmp(document);

        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = changedTitle });

        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(changedTitle);
        await Assert.That(PdfDocumentMetadata.GetResolvedInfo(document).Title).IsEqualTo(changedTitle);
        await Assert.That(before.Title).IsEqualTo(TestPdf.Title);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(TestPdf.Title);
        await Assert.That(PdfDocumentMetadata.GetResolvedInfo(document).Title).IsEqualTo(TestPdf.Title);
    }

    /// <summary>/Info and XMP are both updated; untouched XMP stays byte for byte and the packet stays well-formed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UpdatesInfoAndXmp()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "New <Title> & more", Producer = "New Producer", Creator = "Creator Tool", Modified = Date, Author = "Ana" });
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);

        var info = PdfDocumentMetadata.GetInfo(saved);
        await Assert.That(info.Title).IsEqualTo("New <Title> & more");
        await Assert.That(info.Producer).IsEqualTo("New Producer");
        await Assert.That(info.Modified).IsEqualTo(Date);
        var xmp = Xmp(saved);
        await Assert.That(XmpEditor.IsWellFormed(Encoding.UTF8.GetBytes(xmp))).IsTrue();
        await Assert.That(xmp).Contains("New &lt;Title&gt; &amp; more");
        await Assert.That(xmp).DoesNotContain(EditingTestDocuments.XmpTitle);
        await Assert.That(xmp).Contains("pdf:Producer=\"New Producer\"");
        await Assert.That(xmp).Contains(">Creator Tool</xmp:CreatorTool>");
        await Assert.That(xmp).Contains(">Ana</rdf:li></rdf:Seq></dc:creator>");
        await Assert.That(xmp).Contains(">2026-03-04T05:00:00+10:00</xmp:ModifyDate>");
        await Assert.That(xmp).Contains(EditingTestDocuments.XmpFormat);
        await Assert.That(xmp).StartsWith("<?xpacket begin=");
        await Assert.That(xmp).EndsWith("<?xpacket end=\"w\"?>");
    }

    /// <summary>An empty string removes an entry from /Info and from XMP.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyStringRemoves()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = string.Empty, Producer = string.Empty });

        var xmp = Xmp(document);
        await Assert.That(xmp).DoesNotContain("dc:title");
        await Assert.That(xmp).DoesNotContain(EditingTestDocuments.XmpProducer);
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsNull();
        await Assert.That(XmpEditor.IsWellFormed(Encoding.UTF8.GetBytes(xmp))).IsTrue();
    }

    /// <summary>A document with /Info gets it replaced; null properties keep their values; compact and encrypted saves keep them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExistingInfoKeepsUntouchedEntries()
    {
        using var document = PdfDocumentReader.Open(Tests.Writing.WritingTestDocuments.Encrypt(TestPdf.Create(Pages)), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Subject = "Subject", Keywords = "one, two" });
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);

        var info = PdfDocumentMetadata.GetInfo(compact);
        await Assert.That(compact.IsEncrypted).IsTrue();
        await Assert.That(info.Title).IsEqualTo(TestPdf.Title);
        await Assert.That(info.Author).IsEqualTo(TestPdf.Author);
        await Assert.That(info.Subject).IsEqualTo("Subject");
        await Assert.That(info.Keywords).IsEqualTo("one, two");
    }

    /// <summary>A packet that is not well-formed is left alone while /Info is still written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenXmpIsLeftAlone()
    {
        await Assert.That(XmpEditor.Apply("<x:xmpmeta><rdf:RDF>"u8, [new(XmpProperty.Title, "T")])).IsNull();
        await Assert.That(XmpEditor.Apply([], [new(XmpProperty.Title, "T")])).IsNull();
    }

    /// <summary>A property missing from the packet is added to the first description, declaring its namespace.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPropertyIsAdded()
    {
        var packet = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\"/></rdf:RDF></x:xmpmeta>"u8;
        var result = XmpEditor.Apply(packet, [new(XmpProperty.Keywords, "a & b")]);

        await Assert.That(result).IsNotNull();
        var text = Encoding.UTF8.GetString(result!);
        await Assert.That(text).Contains("<pdf:Keywords xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">a &amp; b</pdf:Keywords>");
        await Assert.That(XmpEditor.IsWellFormed(result!)).IsTrue();
    }

    /// <summary>Reads the catalog's XMP packet.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The packet text.</returns>
    private static string Xmp(PdfDocument document) =>
        Encoding.UTF8.GetString(document.Catalog.GetStream(KnownName.Metadata)!.DecodeToArray());
}
