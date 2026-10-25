// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for <see cref="PdfCompactWriter"/>.</summary>
public sealed class CompactWriterTests
{
    /// <summary>The page count of the generated document.</summary>
    private const int PageCount = 5;

    /// <summary>The key that references a missing object.</summary>
    private const string DanglingKey = "Dangling";

    /// <summary>An object number no document has.</summary>
    private const int MissingObject = 9999;

    /// <summary>A header older than object streams.</summary>
    private const string OldHeader = "%PDF-1.3";

    /// <summary>The header object streams need.</summary>
    private const string ObjectStreamHeader = "%PDF-1.5";

    /// <summary>Concurrent first saves share a completed graph while keeping independent output buffers.</summary>
    /// <param name="objectStreams">Whether to pack objects.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConcurrentFirstSavesKeepIndependentOutputs(bool objectStreams)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        var options = new PdfCompactOptions(objectStreams, false);
        var pending = new Task<byte[]>[4];
        for (var i = 0; i < pending.Length; i++)
        {
            pending[i] = Task.Run(() => PdfCompactWriter.Save(document.Objects, options), CancellationToken.None);
        }

        var outputs = await Task.WhenAll(pending);
        foreach (var bytes in outputs)
        {
            await Assert.That(bytes).IsEquivalentTo(outputs[0]);
            using var reopened = PdfDocumentReader.Open(bytes, null);
            await Assert.That(reopened.PageCount).IsEqualTo(1);
            await Assert.That(PdfDocumentMetadata.GetInfo(reopened).Title).IsEqualTo(TestPdf.Title);
        }

        await Assert.That(ReferenceEquals(outputs[0], outputs[1])).IsFalse();
    }

    /// <summary>Completed save graphs follow direct edits, committed edits, undo, redo and rollback.</summary>
    /// <param name="objectStreams">Whether to pack objects.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CachedGraphFollowsEditsAndHistory(bool objectStreams)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        var options = new PdfCompactOptions(objectStreams, false);
        _ = PdfCompactWriter.Save(document.Objects, options);
        var info = document.Objects.Trailer.GetRaw(KnownName.Info).AsReference();
        StoreEditing.Replace(document.Objects, info, PdfValue.FromDictionary(new(document.Objects)));
        using var replaced = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, options), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(replaced).Title).IsNull();

        const string changed = "Changed";
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = changed });
        using var edited = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, options), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(edited).Title).IsEqualTo(changed);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        using var undone = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, options), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(undone).Title).IsNull();
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        using var redone = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, options), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(redone).Title).IsEqualTo(changed);

        using (var transaction = PdfDocumentEditing.BeginEdit(document, "Abandoned"))
        {
            PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Rolled back" });
            _ = PdfCompactWriter.Save(document.Objects, options);
        }

        using var rolledBack = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, options), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(rolledBack).Title).IsEqualTo(changed);
    }

    /// <summary>A compacted document keeps its pages and content, in both layouts, and every object resolves.</summary>
    /// <param name="document">The document name.</param>
    /// <param name="useObjectStreams">Whether to write object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("plain", true)]
    [Arguments("plain", false)]
    [Arguments("compressed", true)]
    [Arguments("compressed", false)]
    [Arguments("attachment", true)]
    [Arguments("attachment", false)]
    [Arguments("form", true)]
    [Arguments("form", false)]
    public async Task CompactKeepsPagesAndContent(string document, bool useObjectStreams)
    {
        var original = Create(document);
        var expected = Contents(original);
        var compacted = Compact(original, new(useObjectStreams, false));
        var actual = Contents(compacted);
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i]).IsEquivalentTo(expected[i]);
        }

        await Assert.That(Missing(compacted)).IsEqualTo(0);
        await Assert.That(Encoding.Latin1.GetString(compacted).Contains("/ObjStm", StringComparison.Ordinal)).IsEqualTo(useObjectStreams);
    }

    /// <summary>Object streams raise the header version to 1.5; the classic layout keeps it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderVersionFollowsLayout()
    {
        var original = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(TestPdf.Create(1)).Replace("%PDF-1.7", OldHeader, StringComparison.Ordinal));
        await Assert.That(Encoding.ASCII.GetString(Compact(original, PdfCompactOptions.Default), 0, ObjectStreamHeader.Length)).IsEqualTo(ObjectStreamHeader);
        await Assert.That(Encoding.ASCII.GetString(Compact(original, PdfCompactOptions.Classic), 0, OldHeader.Length)).IsEqualTo(OldHeader);
    }

    /// <summary>Unreachable objects are dropped and the rest renumbered densely, with the catalog first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsUnreachableObjects()
    {
        using var store = StoreOpening.Open(TestPdf.Create(PageCount), null);
        _ = StoreEditing.Add(store, PdfValue.FromString("orphan"u8.ToArray()));
        var compacted = PdfCompactWriter.Save(store, PdfCompactOptions.Classic);
        using var reopened = StoreOpening.Open(compacted, null);
        await Assert.That(reopened.Size).IsLessThan(store.Size);
        await Assert.That(reopened.Trailer.GetRaw(KnownName.Root).AsReference().Number).IsEqualTo(1);
        await Assert.That(Encoding.Latin1.GetString(compacted)).DoesNotContain("orphan");
    }

    /// <summary>An encrypted document stays encrypted under its new numbers, or is decrypted on request.</summary>
    /// <param name="useObjectStreams">Whether to write object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task EncryptedDocumentsKeepOrDropEncryption(bool useObjectStreams)
    {
        var plain = TestPdf.Create(PageCount);
        var expected = Contents(plain);
        var encrypted = WritingTestDocuments.Encrypt(plain);
        var kept = Compact(encrypted, new(useObjectStreams, false));
        var removed = Compact(encrypted, new(useObjectStreams, true));
        await Assert.That(Contents(encrypted)[0]).IsEquivalentTo(expected[0]);
        await Assert.That(IsEncrypted(kept)).IsTrue();
        await Assert.That(IsEncrypted(removed)).IsFalse();
        await Assert.That(Contents(kept)[PageCount - 1]).IsEquivalentTo(expected[PageCount - 1]);
        await Assert.That(Contents(removed)[PageCount - 1]).IsEquivalentTo(expected[PageCount - 1]);
        await Assert.That(Encoding.Latin1.GetString(kept)).DoesNotContain(TestPdf.Author);
        await Assert.That(ReadAuthor(kept)).IsEqualTo(TestPdf.Author);
        await Assert.That(ReadAuthor(removed)).IsEqualTo(TestPdf.Author);
    }

    /// <summary>A key whose value references a missing object is kept and written as null, in both layouts.</summary>
    /// <param name="useObjectStreams">Whether to write object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DanglingReferenceIsWrittenAsNull(bool useObjectStreams)
    {
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var catalog = store.Catalog.Clone();
        catalog.Set(store.Names.Intern(DanglingKey), PdfValue.FromReference(new(MissingObject, 0)));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
        var compacted = PdfCompactWriter.Save(store, new(useObjectStreams, false));
        using var reopened = StoreOpening.Open(compacted, null);
        if (!useObjectStreams)
        {
            await Assert.That(Encoding.Latin1.GetString(compacted).Contains("/Dangling null", StringComparison.Ordinal)).IsTrue();
        }

        await Assert.That(reopened.Catalog.GetRaw(reopened.Names.Intern(DanglingKey)).IsNull).IsTrue();
        await Assert.That(Missing(compacted)).IsEqualTo(0);
    }

    /// <summary>Opens a file and reads the author from its information dictionary.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The author.</returns>
    private static string? ReadAuthor(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return store.Trailer.GetDictionary(KnownName.Info)?.GetText(KnownName.Author);
    }

    /// <summary>Creates a test document.</summary>
    /// <param name="document">The document name.</param>
    /// <returns>The file.</returns>
    private static byte[] Create(string document) => document switch
    {
        "compressed" => TestPdf.CreateCompressed(),
        "attachment" => TestPdf.CreateWithAttachment(),
        "form" => TestPdf.CreateForm(),
        _ => TestPdf.Create(PageCount),
    };

    /// <summary>Opens a file and compacts it.</summary>
    /// <param name="file">The file.</param>
    /// <param name="options">The layout.</param>
    /// <returns>The compacted file.</returns>
    private static byte[] Compact(byte[] file, in PdfCompactOptions options)
    {
        using var store = StoreOpening.Open(file, null);
        return PdfCompactWriter.Save(store, options);
    }

    /// <summary>Opens a file and reads its page contents.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The decoded content of each page.</returns>
    private static List<byte[]> Contents(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return WritingTestDocuments.PageContents(store);
    }

    /// <summary>Opens a file and counts objects that do not resolve.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The count.</returns>
    private static int Missing(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return WritingTestDocuments.CountMissing(store);
    }

    /// <summary>Opens a file and checks whether it is encrypted.</summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsEncrypted(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return store.Security is not null;
    }
}
