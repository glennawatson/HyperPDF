// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for the writer controls of <see cref="PdfCompactOptions"/>: reproducible output, file identifiers, versions, stream modes and unreferenced objects.</summary>
public sealed class WriterControlTests
{
    /// <summary>The length of the identifier parts the writer makes: an MD5 hash.</summary>
    private const int IdLength = 16;

    /// <summary>The page count of the object stream sample.</summary>
    private const int ObjectStreamPages = 3;

    /// <summary>The objects in each object stream of the object stream sample.</summary>
    private const int ObjectsPerContainer = 4;

    /// <summary>The title set to change a document.</summary>
    private const string NewTitle = "A changed title";

    /// <summary>The name of the Flate filter.</summary>
    private const string FlateFilter = "FlateDecode";

    /// <summary>The name of the ASCIIHex filter.</summary>
    private const string AsciiHexFilter = "ASCIIHexDecode";

    /// <summary>The first object of the unreferenced sample that nothing references.</summary>
    private const int OrphanDictionary = 5;

    /// <summary>Two saves of one document are the same bytes in every stream mode and layout, with page content normalised.</summary>
    /// <param name="mode">The stream mode.</param>
    /// <param name="useObjectStreams">Whether to pack objects into object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStreamMode.Compress, false)]
    [Arguments(PdfStreamMode.Compress, true)]
    [Arguments(PdfStreamMode.Preserve, false)]
    [Arguments(PdfStreamMode.Preserve, true)]
    [Arguments(PdfStreamMode.Recompress, false)]
    [Arguments(PdfStreamMode.Recompress, true)]
    [Arguments(PdfStreamMode.Uncompressed, false)]
    [Arguments(PdfStreamMode.Uncompressed, true)]
    public async Task ReproducibleSavesAreByteIdentical(PdfStreamMode mode, bool useObjectStreams)
    {
        var options = new PdfCompactOptions(useObjectStreams, false) { StreamMode = mode, Reproducible = true, Normalisation = PdfContentNormalisation.All };
        var source = WriterControlSamples.ContentPage();

        var first = Save(source, options);
        var second = Save(source, options);
        using var store = StoreOpening.Open(source, null);
        var third = PdfCompactWriter.Save(store, options);
        var fourth = PdfCompactWriter.Save(store, options);

        await Assert.That(second.AsSpan().SequenceEqual(first)).IsTrue();
        await Assert.That(third.AsSpan().SequenceEqual(first)).IsTrue();
        await Assert.That(fourth.AsSpan().SequenceEqual(first)).IsTrue();
    }

    /// <summary>Output encrypted with RC4 has no random part, so reproducible saves of it are the same bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReproducibleRc4SavesAreByteIdentical()
    {
        var encrypted = WritingTestDocuments.Encrypt(TestPdf.Create(1));
        var options = PdfCompactOptions.Default with { Reproducible = true };

        var first = Save(encrypted, options);
        var second = Save(encrypted, options);

        await Assert.That(second.AsSpan().SequenceEqual(first)).IsTrue();
    }

    /// <summary>AES picks a random initialisation vector for each string and stream, so a reproducible save of AES output fails; saved decrypted it works.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReproducibleSaveRefusesAesOutput()
    {
        using var source = StoreOpening.Open(AesGcmPdf.Create(AesGcmLayout.Everything, true), AesGcmPdf.UserPassword);
        var options = PdfCompactOptions.Default with { Reproducible = true };

        _ = await Assert.That(() => PdfCompactWriter.Save(source, options)).Throws<PdfException>();
        var decrypted = PdfCompactWriter.Save(source, options with { RemoveEncryption = true });

        await Assert.That(Save(decrypted, options).Length).IsGreaterThan(0);
    }

    /// <summary>A document with no /ID gets one on its first write, with both parts the same hash.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewIdIsMadeOnFirstWrite()
    {
        var saved = Save(WriterControlSamples.WithOrphans(), PdfCompactOptions.Default);

        var id = WriterStreams.ReadId(saved);

        await Assert.That(id).IsNotNull();
        await Assert.That(id!.Value.First.Length).IsEqualTo(IdLength);
        await Assert.That(id.Value.Second.AsSpan().SequenceEqual(id.Value.First)).IsTrue();
    }

    /// <summary>Saving the same content again keeps both parts; saving a changed document keeps the first part and changes the second.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondPartChangesWithTheContentAndFirstPartStays()
    {
        var first = Save(WriterControlSamples.WithOrphans(), PdfCompactOptions.Default);
        var firstId = WriterStreams.ReadId(first)!.Value;

        var unchanged = WriterStreams.ReadId(Save(first, PdfCompactOptions.Default))!.Value;
        using var document = PdfDocumentReader.Open(first, null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = NewTitle });
        var changed = WriterStreams.ReadId(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default))!.Value;

        await Assert.That(unchanged.First.AsSpan().SequenceEqual(firstId.First)).IsTrue();
        await Assert.That(unchanged.Second.AsSpan().SequenceEqual(firstId.Second)).IsTrue();
        await Assert.That(changed.First.AsSpan().SequenceEqual(firstId.First)).IsTrue();
        await Assert.That(changed.Second.AsSpan().SequenceEqual(firstId.Second)).IsFalse();
    }

    /// <summary>A minimum version raises the header, and a lower one never lowers it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MinimumVersionRaisesTheHeaderAndNeverLowersIt()
    {
        var source = WriterControlSamples.WithOrphans();

        var raised = Save(source, PdfCompactOptions.Classic with { MinimumVersion = PdfVersion.Pdf20 });
        var kept = Save(raised, PdfCompactOptions.Classic with { MinimumVersion = PdfVersion.Pdf14 });

        await Assert.That(WriterStreams.Header(raised)).IsEqualTo("%PDF-2.0");
        await Assert.That(WriterStreams.Header(kept)).IsEqualTo("%PDF-2.0");
    }

    /// <summary>A catalog /Version newer than the header raises the written header (ISO 32000-2 §7.2.2).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CatalogVersionRaisesTheHeader()
    {
        var text = Encoding.Latin1.GetString(WriterControlSamples.WithOrphans())
            .Replace("%PDF-1.7", "%PDF-1.4", StringComparison.Ordinal)
            .Replace("/Type /Catalog", "/Type /Catalog /Version /1.6", StringComparison.Ordinal);

        var saved = Save(Encoding.Latin1.GetBytes(text), PdfCompactOptions.Classic);

        await Assert.That(WriterStreams.Header(saved)).IsEqualTo("%PDF-1.6");
    }

    /// <summary>The default stream mode compresses a plain stream and copies a filtered one; preserve copies both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultCompressesPlainStreamsAndPreserveCopiesAll()
    {
        var source = WriterControlSamples.FilteredStreams(false);

        var compressed = Save(source, PdfCompactOptions.Classic);
        var preserved = Save(source, PdfCompactOptions.Classic with { StreamMode = PdfStreamMode.Preserve });

        await Assert.That(Filters(compressed)).IsEquivalentTo([AsciiHexFilter, FlateFilter]);
        await Assert.That(Filters(preserved)).IsEquivalentTo([AsciiHexFilter, string.Empty]);
    }

    /// <summary>Recompress writes byte-filtered and plain streams as Flate, keeping what they decode to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecompressWritesFlateAndKeepsTheData()
    {
        var source = WriterControlSamples.FilteredStreams(false);

        var recompressed = Save(source, PdfCompactOptions.Classic with { StreamMode = PdfStreamMode.Recompress });

        await Assert.That(Filters(recompressed)).IsEquivalentTo([FlateFilter, FlateFilter]);
        await Assert.That(Decoded(recompressed)).IsEqualTo(Decoded(source));
    }

    /// <summary>The smallest level makes a repetitive stream no larger than the fastest, and both decode to the same content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallestLevelIsNoLargerThanFastest()
    {
        var source = WriterControlSamples.RepetitivePage();
        var fast = PdfCompactOptions.Classic with { StreamMode = PdfStreamMode.Recompress, CompressionLevel = PdfCompressionLevel.Fastest };

        var fastest = Save(source, fast);
        var smallest = Save(source, fast with { CompressionLevel = PdfCompressionLevel.Smallest });

        await Assert.That(smallest.Length).IsLessThanOrEqualTo(fastest.Length);
        await Assert.That(Decoded(smallest)).IsEqualTo(Decoded(source));
        await Assert.That(Decoded(fastest)).IsEqualTo(Decoded(source));
    }

    /// <summary>Uncompressed mode leaves no byte filter on any stream, object streams included, and copies an image codec untouched.</summary>
    /// <param name="useObjectStreams">Whether to pack objects into object streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UncompressedLeavesNoByteFilterAndKeepsImageCodecs(bool useObjectStreams)
    {
        var source = WriterControlSamples.FilteredStreams(true);

        var saved = Save(source, PdfCompactOptions.Default with { UseObjectStreams = useObjectStreams, StreamMode = PdfStreamMode.Uncompressed });

        using var store = StoreOpening.Open(saved, null);

        // The cross-reference stream is the one stream the writer always compresses.
        var filters = WriterStreams.All(store)
            .Where(static stream => !stream.Dictionary.IsName(KnownName.Type, KnownName.XRef))
            .Select(stream => WriterStreams.FilterOf(store, stream))
            .Where(static filter => filter.Length > 0)
            .ToList();
        var jpeg = WriterStreams.Find(store, stream => WriterStreams.FilterOf(store, stream) == "DCTDecode");
        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(jpeg).IsNotNull();
        await Assert.That(Encoding.Latin1.GetString(jpeg!.DecodeToArray())).IsEqualTo("not-a-real-jpeg");
        await Assert.That(Encoding.Latin1.GetString(saved).Contains("/ObjStm", StringComparison.Ordinal)).IsEqualTo(useObjectStreams);
        await Assert.That(Encoding.Latin1.GetString(saved).Contains("1 0 0 rg 20 20 100 100 re f", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Recompress copies an image codec untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecompressCopiesImageCodecsUntouched()
    {
        var saved = Save(WriterControlSamples.FilteredStreams(true), PdfCompactOptions.Classic with { StreamMode = PdfStreamMode.Recompress });

        using var store = StoreOpening.Open(saved, null);
        var jpeg = WriterStreams.Find(store, stream => WriterStreams.FilterOf(store, stream) == "DCTDecode");
        await Assert.That(jpeg).IsNotNull();
        await Assert.That(Encoding.Latin1.GetString(jpeg!.CopyRawData())).IsEqualTo("not-a-real-jpeg");
    }

    /// <summary>Objects no reference reaches are dropped by default and kept on request.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreferencedObjectsAreDroppedOrKept()
    {
        var source = WriterControlSamples.WithOrphans();

        var dropped = Encoding.Latin1.GetString(Save(source, PdfCompactOptions.Classic));
        var keptBytes = Save(source, PdfCompactOptions.Classic with { KeepUnreferenced = true });

        await Assert.That(dropped.Contains(WriterControlSamples.OrphanMarker, StringComparison.Ordinal)).IsFalse();
        await Assert.That(Encoding.Latin1.GetString(keptBytes).Contains(WriterControlSamples.OrphanMarker, StringComparison.Ordinal)).IsTrue();
        using var store = StoreOpening.Open(keptBytes, null);
        await Assert.That(WritingTestDocuments.CountMissing(store)).IsEqualTo(0);
        await Assert.That(StoreReading.GetObject(store, new(OrphanDictionary, 0)).IsNull).IsFalse();
    }

    /// <summary>Keeping unreferenced objects never copies the object streams of the source file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepingUnreferencedObjectsSkipsSourceObjectStreams()
    {
        var source = ObjectStreamPdf.Create(ObjectStreamPages, ObjectsPerContainer);

        var saved = Save(source, PdfCompactOptions.Classic with { KeepUnreferenced = true });

        await Assert.That(Encoding.Latin1.GetString(saved).Contains("/ObjStm", StringComparison.Ordinal)).IsFalse();
        using var store = StoreOpening.Open(saved, null);
        await Assert.That(WritingTestDocuments.CountMissing(store)).IsEqualTo(0);
        await Assert.That(WritingTestDocuments.PageContents(store).Count).IsEqualTo(ObjectStreamPages);
    }

    /// <summary>Every mode's output is accepted by <c>qpdf --check</c> when qpdf is installed.</summary>
    /// <param name="mode">The stream mode.</param>
    /// <param name="useObjectStreams">Whether to pack objects into object streams.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStreamMode.Compress, false)]
    [Arguments(PdfStreamMode.Compress, true)]
    [Arguments(PdfStreamMode.Preserve, true)]
    [Arguments(PdfStreamMode.Recompress, false)]
    [Arguments(PdfStreamMode.Recompress, true)]
    [Arguments(PdfStreamMode.Uncompressed, false)]
    [Arguments(PdfStreamMode.Uncompressed, true)]
    public async Task EveryModePassesQpdfCheck(PdfStreamMode mode, bool useObjectStreams, CancellationToken cancellationToken)
    {
        var options = new PdfCompactOptions(useObjectStreams, false) { StreamMode = mode, Normalisation = PdfContentNormalisation.All, KeepUnreferenced = true };
        var saved = Save(WriterControlSamples.ContentPage(), options);

        var outcome = await QpdfCheck.RunAsync($"mode-{mode}-{(useObjectStreams ? "objstm" : "classic")}", saved, cancellationToken);

        if (outcome is not { } result)
        {
            Skip.Test("qpdf is not installed.");
            return;
        }

        await Assert.That(result.IsClean).IsTrue();
    }

    /// <summary>Opens a file and saves it.</summary>
    /// <param name="file">The file.</param>
    /// <param name="options">The layout.</param>
    /// <returns>The saved file.</returns>
    private static byte[] Save(byte[] file, in PdfCompactOptions options)
    {
        using var store = StoreOpening.Open(file, null);
        return PdfCompactWriter.Save(store, options);
    }

    /// <summary>Lists the byte filter of the first page's content stream and of the first image of the file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The content stream's filter, then the image's filter; empty for none.</returns>
    private static string[] Filters(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        var content = WriterStreams.Find(store, static stream => stream.Dictionary.Get(KnownName.Subtype).IsNull);
        var image = WriterStreams.Find(store, static stream => stream.Dictionary.IsName(KnownName.Subtype, KnownName.Image));
        return [WriterStreams.FilterOf(store, content!), WriterStreams.FilterOf(store, image!)];
    }

    /// <summary>Decodes the first stream that is not an image.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The decoded text.</returns>
    private static string Decoded(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        var content = WriterStreams.Find(store, static stream => stream.Dictionary.Get(KnownName.Subtype).IsNull);
        return Encoding.Latin1.GetString(content!.DecodeToArray());
    }
}
