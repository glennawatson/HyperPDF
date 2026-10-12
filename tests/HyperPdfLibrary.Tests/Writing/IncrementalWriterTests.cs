// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for <see cref="PdfIncrementalWriter"/>.</summary>
public sealed class IncrementalWriterTests
{
    /// <summary>The page count of the generated document.</summary>
    private const int PageCount = 3;

    /// <summary>The key added to the catalog.</summary>
    private const string MarkerKey = "HyperPdfMarker";

    /// <summary>The text stored in the added object.</summary>
    private const string MarkerText = "Saved (incrementally) \\ again";

    /// <summary>Gets the marker text's bytes.</summary>
    private static ReadOnlySpan<byte> MarkerBytes => "Saved (incrementally) \\ again"u8;

    /// <summary>An edit to a plain document is appended after the original bytes and reads back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClassicUpdateKeepsOriginalAndAddsObject()
    {
        var original = TestPdf.Create(PageCount);
        var saved = SaveWithMarker(original);
        var text = ReadMarker(saved, out var pages, out var missing);
        await Assert.That(saved.AsSpan(0, original.Length).SequenceEqual(original)).IsTrue();
        await Assert.That(text).IsEqualTo(MarkerText);
        await Assert.That(pages).IsEqualTo(PageCount);
        await Assert.That(missing).IsEqualTo(0);
        await Assert.That(Encoding.Latin1.GetString(saved, original.Length, saved.Length - original.Length)).Contains("/Prev ");
    }

    /// <summary>A document whose newest section is a stream gets a cross-reference stream update.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamUpdateUsesCrossReferenceStream()
    {
        var original = TestPdf.CreateCompressed();
        var saved = SaveWithMarker(original);
        var text = ReadMarker(saved, out var pages, out _);
        var update = Encoding.Latin1.GetString(saved, original.Length, saved.Length - original.Length);
        await Assert.That(saved.AsSpan(0, original.Length).SequenceEqual(original)).IsTrue();
        await Assert.That(text).IsEqualTo(MarkerText);
        await Assert.That(pages).IsEqualTo(1);
        await Assert.That(update).Contains("/Type /XRef");
        await Assert.That(update).DoesNotContain("trailer");
    }

    /// <summary>Saving the same edits twice gives the same bytes, and the stream overloads match the array.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAreDeterministicAcrossOverloads()
    {
        var original = TestPdf.Create(PageCount);
        using var store = StoreOpening.Open(original, null);
        AddMarker(store);
        var first = PdfIncrementalWriter.Save(store);
        var second = PdfIncrementalWriter.Save(store);
        await using var synchronous = new MemoryStream();
        PdfIncrementalWriter.Save(store, synchronous);
        await using var asynchronous = new MemoryStream();
        await PdfIncrementalWriter.SaveAsync(store, asynchronous, CancellationToken.None);
        await Assert.That(second).IsEquivalentTo(first);
        await Assert.That(synchronous.ToArray()).IsEquivalentTo(first);
        await Assert.That(asynchronous.ToArray()).IsEquivalentTo(first);
    }

    /// <summary>An edit to an encrypted document is encrypted with the document's key and reads back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedUpdateReadsBack()
    {
        var original = WritingTestDocuments.Encrypt(TestPdf.Create(PageCount));
        var saved = SaveWithMarker(original);
        var text = ReadMarker(saved, out var pages, out _);
        var update = Encoding.Latin1.GetString(saved, original.Length, saved.Length - original.Length);
        await Assert.That(text).IsEqualTo(MarkerText);
        await Assert.That(pages).IsEqualTo(PageCount);
        await Assert.That(update).DoesNotContain("incrementally");
    }

    /// <summary>A save without edits gives back the original bytes from every overload, for classic and stream sections (#178).</summary>
    /// <param name="compressed">Whether the document uses a cross-reference stream.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UneditedSaveCopiesTheOriginalBytes(bool compressed)
    {
        var original = compressed ? TestPdf.CreateCompressed() : TestPdf.Create(PageCount);
        using var store = StoreOpening.Open(original, null);
        await using var synchronous = new MemoryStream();
        PdfIncrementalWriter.Save(store, synchronous);
        await using var asynchronous = new MemoryStream();
        await PdfIncrementalWriter.SaveAsync(store, asynchronous, CancellationToken.None);
        await Assert.That(PdfIncrementalWriter.Save(store)).IsEquivalentTo(original);
        await Assert.That(synchronous.ToArray()).IsEquivalentTo(original);
        await Assert.That(asynchronous.ToArray()).IsEquivalentTo(original);
    }

    /// <summary>An update that only changes the trailer writes a classic section with a subsection, which readers accept without repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrailerOnlyUpdateWritesASubsection()
    {
        var original = TestPdf.Create(PageCount);
        using var store = StoreOpening.Open(original, null);
        var transaction = StoreTransactions.BeginTransaction(store, "Trailer");
        transaction.SetTrailerEntry(store.Names.Intern(MarkerKey), PdfValue.FromInteger(PageCount));
        transaction.Commit();
        var saved = PdfIncrementalWriter.Save(store);
        var update = Encoding.Latin1.GetString(saved, original.Length, saved.Length - original.Length);
        using var reopened = StoreOpening.Open(saved, null);
        await Assert.That(update).Contains("xref\n0 1\n0000000000 65535 f");
        await Assert.That(reopened.WasRepaired).IsFalse();
        await Assert.That(reopened.Trailer.Get(reopened.Names.Intern(MarkerKey)).AsInteger()).IsEqualTo(PageCount);
    }

    /// <summary>Opens a document, adds the marker and saves incrementally.</summary>
    /// <param name="original">The original file.</param>
    /// <returns>The saved file.</returns>
    private static byte[] SaveWithMarker(byte[] original)
    {
        using var store = StoreOpening.Open(original, null);
        AddMarker(store);
        return PdfIncrementalWriter.Save(store);
    }

    /// <summary>Adds an object holding the marker text and points a copy of the catalog at it.</summary>
    /// <param name="store">The document.</param>
    private static void AddMarker(PdfObjectStore store)
    {
        var marker = new PdfDictionary(store);
        marker.Set(KnownName.Title, PdfValue.FromString(MarkerBytes.ToArray()));
        var markerId = StoreEditing.Add(store, PdfValue.FromDictionary(marker));
        var catalog = store.Catalog.Clone();
        catalog.Set(store.Names.Intern(MarkerKey), PdfValue.FromReference(markerId));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
    }

    /// <summary>Reopens a saved file and reads the marker.</summary>
    /// <param name="saved">The saved file.</param>
    /// <param name="pages">The page count.</param>
    /// <param name="missing">The number of objects that do not resolve.</param>
    /// <returns>The marker text, or <see langword="null"/>.</returns>
    private static string? ReadMarker(byte[] saved, out int pages, out int missing)
    {
        using var reopened = StoreOpening.Open(saved, null);
        pages = WritingTestDocuments.PageContents(reopened).Count;
        missing = WritingTestDocuments.CountMissing(reopened);
        return reopened.Catalog.GetDictionary(reopened.Names.Intern(MarkerKey))?.GetText(KnownName.Title);
    }
}
