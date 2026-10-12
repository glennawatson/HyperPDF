// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for object deletion, ids, number formatting, trailers, header offsets, sizes and metadata in the writers.</summary>
public sealed class WriterFixTests
{
    /// <summary>The page count of the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>The /Size set on the trailer of the original document.</summary>
    private const int LargeSize = 1000;

    /// <summary>The value stored under the custom trailer key.</summary>
    private const int TrailerValue = 7;

    /// <summary>The custom trailer key.</summary>
    private const string TrailerKey = "HyperPdfTrailerKey";

    /// <summary>An object number above anything the document holds.</summary>
    private const int MissingObject = 9999;

    /// <summary>The edits in the snapshot test: one deletion and one addition.</summary>
    private const int SnapshotCount = 2;

    /// <summary>The length of the generated metadata, long enough for Flate to shrink it.</summary>
    private const int MetadataLength = 4096;

    /// <summary>The filler byte of the metadata.</summary>
    private const byte MetadataFill = (byte)'x';

    /// <summary>A real number too small for six decimals.</summary>
    private const double Tiny = 1e-7;

    /// <summary>A real number whose value is not exactly representable.</summary>
    private const double Tenth = 0.1;

    /// <summary>A number beyond the range of a long.</summary>
    private const double Huge = 1e300;

    /// <summary>A negative number that rounds to zero at six decimals.</summary>
    private const double NegativeTiny = -1e-9;

    /// <summary>The generation a freed object takes.</summary>
    private const int FreedGeneration = 1;

    /// <summary>Gets the text placed in front of the header.</summary>
    private static ReadOnlySpan<byte> Junk => "JUNK BEFORE THE HEADER\n"u8;

    /// <summary>A deleted object gets a free entry, stays gone after reopening, and reuses its number with the next generation.</summary>
    /// <param name="useStreams">Whether the document uses cross-reference streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeletedObjectStaysFreeAndReusesNumberWithNextGeneration(bool useStreams)
    {
        var original = useStreams ? TestPdf.CreateCompressed() : TestPdf.Create(1);
        var added = SaveWithNewObject(original, out var id);
        var deleted = SaveAfterDelete(added, id, out var freedGeneration);
        var update = Encoding.Latin1.GetString(deleted, added.Length, deleted.Length - added.Length);
        using var reopened = StoreOpening.Open(deleted, null);
        var reusedGeneration = StoreEditing.GetGeneration(reopened, id.Number);
        StoreEditing.Replace(reopened, id, PdfValue.FromInteger(TrailerValue));
        var reused = PdfIncrementalWriter.Save(reopened);
        using var final = StoreOpening.Open(reused, null);
        await Assert.That(freedGeneration).IsEqualTo(FreedGeneration);
        await Assert.That(StoreReading.GetObject(reopened, id).AsInteger()).IsEqualTo(TrailerValue);
        await Assert.That(reusedGeneration).IsEqualTo(FreedGeneration);
        await Assert.That(StoreReading.GetObject(final, id).AsInteger()).IsEqualTo(TrailerValue);
        await Assert.That(StoreEditing.GetGeneration(final, id.Number)).IsEqualTo(FreedGeneration);
        await Assert.That(Encoding.Latin1.GetString(reused, deleted.Length, reused.Length - deleted.Length)).Contains($"{id.Number} {FreedGeneration} obj");
        if (!useStreams)
        {
            await Assert.That(update).Contains($"{FreedGeneration:D5} f");
        }
    }

    /// <summary>A deleted object does not resolve in the reopened file even though an older section still holds it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletedObjectDoesNotResolveAfterReopening()
    {
        var added = SaveWithNewObject(TestPdf.Create(1), out var id);
        var deleted = SaveAfterDelete(added, id, out _);
        using var before = StoreOpening.Open(added, null);
        using var after = StoreOpening.Open(deleted, null);
        await Assert.That(StoreReading.GetObject(before, id).IsNull).IsFalse();
        await Assert.That(StoreReading.GetObject(after, id).IsNull).IsTrue();
        await Assert.That(after.WasRepaired).IsFalse();
    }

    /// <summary>The edit snapshot lists changed and deleted objects in order, with generations, in one step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditSnapshotListsEditsAndDeletionsInOrder()
    {
        var added = SaveWithNewObject(TestPdf.Create(1), out var id);
        using var store = StoreOpening.Open(added, null);
        var fresh = StoreEditing.Add(store, PdfValue.FromInteger(TrailerValue));
        StoreEditing.Delete(store, id);
        var snapshot = StoreEditing.GetEditedObjects(store, out var size);
        await Assert.That(snapshot.Length).IsEqualTo(SnapshotCount);
        await Assert.That(snapshot[0].Number).IsEqualTo(id.Number);
        await Assert.That(snapshot[0].Deleted).IsTrue();
        await Assert.That(snapshot[0].Generation).IsEqualTo(FreedGeneration);
        await Assert.That(snapshot[1].Number).IsEqualTo(fresh.Number);
        await Assert.That(snapshot[1].Deleted).IsFalse();
        await Assert.That(size).IsEqualTo(fresh.Number + 1);
    }

    /// <summary>An encrypted document with no first id part gets no synthesised id; a plain one does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdIsNotSynthesisedForEncryptedDocumentsWithoutOne()
    {
        var source = new PdfDictionary(null);
        var encrypted = new PdfDictionary(null);
        var plain = new PdfDictionary(null);
        PdfXrefWriter.SetFileId(encrypted, source, "content"u8, true);
        PdfXrefWriter.SetFileId(plain, source, "content"u8, false);
        await Assert.That(encrypted.ContainsKey(KnownName.ID)).IsFalse();
        await Assert.That(plain.ContainsKey(KnownName.ID)).IsTrue();
    }

    /// <summary>Saving without edits still gives each document its own second id part.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZeroEditSaveHashesTheOriginalFile()
    {
        var first = SecondId(TestPdf.Create(1));
        var second = SecondId(TestPdf.Create(PageCount));
        await Assert.That(first.Length).IsGreaterThan(0);
        await Assert.That(second).IsNotEquivalentTo(first);
    }

    /// <summary>Numbers beyond the long range, tiny negatives and negative zero format to valid text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HugeAndNegativeZeroNumbersFormatToText()
    {
        await Assert.That(FormatNumber(Huge, false)).IsEqualTo(long.MaxValue.ToString(CultureInfo.InvariantCulture));
        await Assert.That(FormatNumber(-Huge, false)).IsEqualTo((-long.MaxValue).ToString(CultureInfo.InvariantCulture));
        await Assert.That(FormatNumber(double.MaxValue, true)).IsEqualTo(long.MaxValue.ToString(CultureInfo.InvariantCulture));
        await Assert.That(FormatNumber(NegativeTiny, false)).IsEqualTo("0");
        await Assert.That(FormatNumber(-0.0, false)).IsEqualTo("0");
    }

    /// <summary>Precise formatting keeps small values, and six-decimal formatting keeps its limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreciseFormattingKeepsSmallValues()
    {
        await Assert.That(FormatNumber(Tiny, true)).IsEqualTo("0.0000001");
        await Assert.That(FormatNumber(Tiny, false)).IsEqualTo("0");
        await Assert.That(FormatNumber(Tenth, true)).IsEqualTo("0.1");
    }

    /// <summary>A real read from a file keeps its small digits when the object is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrittenRealsKeepSmallDigits()
    {
        await Assert.That(WriteReal(Tiny)).IsEqualTo("0.0000001");
        await Assert.That(WriteReal(Tenth)).IsEqualTo("0.1");
    }

    /// <summary>An incremental update keeps custom trailer keys and does not shrink /Size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UpdateKeepsTrailerKeysAndSize()
    {
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var key = store.Names.Intern(TrailerKey);
        store.Trailer.Set(key, PdfValue.FromInteger(TrailerValue));
        store.Trailer.Set(KnownName.Size, PdfValue.FromInteger(LargeSize));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(store.Catalog.Clone()));
        using var reopened = StoreOpening.Open(PdfIncrementalWriter.Save(store), null);
        await Assert.That(reopened.Trailer.GetInt32(reopened.Names.Intern(TrailerKey))).IsEqualTo(TrailerValue);
        await Assert.That(reopened.Trailer.GetInt32(KnownName.Size)).IsEqualTo(LargeSize);
    }

    /// <summary>Object numbers above the limit are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectNumbersAboveTheLimitAreRefused()
    {
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        await Assert.That(() => StoreEditing.Replace(store, new(int.MaxValue, 0), PdfValue.FromInteger(1))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => StoreEditing.Delete(store, new(int.MaxValue, 0))).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>The compact writer refuses a document whose /Root does not resolve.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactWriterRefusesUnresolvedRoot()
    {
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        store.Trailer.Set(KnownName.Root, PdfValue.FromReference(new(MissingObject, 0)));
        await Assert.That(() => PdfCompactWriter.Save(store, new(true, false))).Throws<PdfException>();
    }

    /// <summary>Junk before the header is read with header-relative offsets, and updates write header-relative offsets.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffsetsAreRelativeToTheHeader()
    {
        var junkLength = Junk.Length;
        byte[] file = [.. Junk, .. TestPdf.Create(PageCount)];
        using var store = StoreOpening.Open(file, null);
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(store.Catalog.Clone()));
        var saved = PdfIncrementalWriter.Save(store);
        var text = Encoding.Latin1.GetString(saved);
        var section = text.LastIndexOf("\nxref", StringComparison.Ordinal) + 1;
        using var reopened = StoreOpening.Open(saved, null);
        await Assert.That(store.HeaderOffset).IsEqualTo(junkLength);
        await Assert.That(store.WasRepaired).IsFalse();
        await Assert.That(StartXref(text)).IsEqualTo(section - junkLength);
        await Assert.That(reopened.WasRepaired).IsFalse();
        await Assert.That(WritingTestDocuments.PageContents(reopened).Count).IsEqualTo(PageCount);
        await Assert.That(WritingTestDocuments.CountMissing(reopened)).IsEqualTo(0);
    }

    /// <summary>The compact writer leaves a metadata stream uncompressed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactWriterKeepsMetadataPlain()
    {
        var data = new byte[MetadataLength];
        Array.Fill(data, MetadataFill);
        using var store = StoreOpening.Open(TestPdf.Create(1), null);
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Metadata));
        var id = StoreEditing.Add(store, PdfValue.FromStream(new(dictionary, data)));
        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.Metadata, PdfValue.FromReference(id));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
        using var reopened = StoreOpening.Open(PdfCompactWriter.Save(store, new(false, false)), null);
        var metadata = reopened.Catalog.GetStream(KnownName.Metadata);
        await Assert.That(metadata is not null).IsTrue();
        await Assert.That(metadata!.Dictionary.GetRaw(KnownName.Filter).IsNull).IsTrue();
        await Assert.That(metadata.RawLength).IsEqualTo(MetadataLength);
    }

    /// <summary>Adds an object holding an integer and saves incrementally.</summary>
    /// <param name="original">The original file.</param>
    /// <param name="id">The new object's id.</param>
    /// <returns>The saved file.</returns>
    private static byte[] SaveWithNewObject(byte[] original, out PdfObjectId id)
    {
        using var store = StoreOpening.Open(original, null);
        id = StoreEditing.Add(store, PdfValue.FromInteger(PageCount));
        return PdfIncrementalWriter.Save(store);
    }

    /// <summary>Deletes an object and saves incrementally.</summary>
    /// <param name="file">The file.</param>
    /// <param name="id">The object to delete.</param>
    /// <param name="generation">The generation the free entry takes.</param>
    /// <returns>The saved file.</returns>
    private static byte[] SaveAfterDelete(byte[] file, PdfObjectId id, out int generation)
    {
        using var store = StoreOpening.Open(file, null);
        StoreEditing.Delete(store, id);
        generation = StoreEditing.GetGeneration(store, id.Number);
        return PdfIncrementalWriter.Save(store);
    }

    /// <summary>Saves a document without edits and reads the second id part.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The second id part.</returns>
    private static byte[] SecondId(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        using var reopened = StoreOpening.Open(PdfIncrementalWriter.Save(store), null);
        return reopened.Trailer.GetArray(KnownName.ID)?.Get(1).AsStringBytes().ToArray() ?? [];
    }

    /// <summary>Writes a real value with the object writer.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The text written.</returns>
    private static string WriteReal(double number)
    {
        var writer = new PdfObjectWriter(new PdfNameTable());
        try
        {
            writer.WriteValue(PdfValue.FromReal(number));
            return Encoding.ASCII.GetString(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Formats a number as text.</summary>
    /// <param name="number">The number.</param>
    /// <param name="precise">Whether to use the precise format.</param>
    /// <returns>The text.</returns>
    private static string FormatNumber(double number, bool precise)
    {
        var buffer = new byte[PdfNumber.MaxFormattedLength];
        var written = precise ? PdfNumber.FormatPrecise(number, buffer) : PdfNumber.Format(number, buffer);
        return Encoding.ASCII.GetString(buffer, 0, written);
    }

    /// <summary>Reads the offset after the last <c>startxref</c>.</summary>
    /// <param name="text">The file as text.</param>
    /// <returns>The offset.</returns>
    private static long StartXref(string text)
    {
        const string Marker = "startxref\n";
        var start = text.LastIndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        var end = text.IndexOf('\n', start);
        return long.Parse(text.AsSpan(start, end - start), CultureInfo.InvariantCulture);
    }
}
