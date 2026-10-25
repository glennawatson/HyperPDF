// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Linearized files: a first-page cross-reference section, a main section, and a hint stream.</summary>
public sealed class LinearizedTests
{
    /// <summary>The object number of the linearization dictionary.</summary>
    private const int LinearizationNumber = 7;

    /// <summary>The object number of the hint stream.</summary>
    private const int HintNumber = 8;

    /// <summary>The length of the hint stream's data.</summary>
    private const int HintLength = 78;

    /// <summary>The number of /H ranges when an overflow hint stream is present.</summary>
    private const int BothHintRanges = 2;

    /// <summary>The width of the first page.</summary>
    private const int FirstWidth = 200;

    /// <summary>The height of the first page.</summary>
    private const int FirstHeight = 100;

    /// <summary>The object number of the first page in the linearized fixture.</summary>
    private const int FirstPageNumber = 5;

    /// <summary>The offset from /L to its first decimal digit.</summary>
    private const int LengthValueOffset = 3;

    /// <summary>The decimal width of an offset field in the generated header.</summary>
    private const int OffsetDigits = 10;

    /// <summary>The byte offset of the packed page object counts after the hint header.</summary>
    private const int PageObjectCountsOffset = 36;

    /// <summary>The byte offset of the shared-object table in the fixture's hint data.</summary>
    private const int SharedTableOffset = 54;

    /// <summary>The byte offset of the shared table's total-object field.</summary>
    private const int SharedTotalOffset = 12;

    /// <summary>The second page's one-bit object-count delta.</summary>
    private const byte SecondPageObjectDelta = 0x40;

    /// <summary>The number of objects in the first page group.</summary>
    private const int FirstPageObjects = 2;

    /// <summary>The number of objects in the second page group.</summary>
    private const int SecondPageObjects = 2;

    /// <summary>The byte offset of the page-length delta width in a hint stream.</summary>
    private const int PageLengthBitsOffset = 14;

    /// <summary>A packed integer width outside the hint format's range.</summary>
    private const ushort InvalidBitWidth = 33;

    /// <summary>Gets the marker before the raw hint bytes.</summary>
    private static ReadOnlySpan<byte> StreamKeyword => "stream\n"u8;

    /// <summary>Both layouts read without repair, with the right pages, contents and objects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedFilesReadWithoutRepair()
    {
        foreach (var streams in new[]
        {
            false,
            true,
        }

        )
        {
            using var store = StoreOpening.Open(LinearizedDocuments.Create(streams), null);
            var contents = WritingTestDocuments.PageContents(store);
            await Assert.That(store.WasRepaired).IsFalse();
            await Assert.That(store.UsesXrefStreams).IsEqualTo(streams);
            await Assert.That(WritingTestDocuments.CountMissing(store)).IsEqualTo(0);
            await Assert.That(Encoding.Latin1.GetString(contents[0])).IsEqualTo(LinearizedDocuments.FirstContent);
            await Assert.That(Encoding.Latin1.GetString(contents[1])).IsEqualTo(LinearizedDocuments.SecondContent);
        }
    }

    /// <summary>The pages come out in the order of the page tree, not the order of the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedPagesFollowThePageTree()
    {
        using var document = PdfDocumentReader.Open(LinearizedDocuments.Create(false), null);
        await Assert.That(document.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(FirstWidth);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Height).IsEqualTo(FirstHeight);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Width).IsEqualTo(FirstHeight);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Height).IsEqualTo(FirstWidth);
    }

    /// <summary>The first object reports bounded linearization parameters and the hint object range.</summary>
    /// <param name="streams">Whether the xref sections are streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LinearizationHeaderReportsFirstPage(bool streams)
    {
        var bytes = LinearizedDocuments.Create(streams);
        using var document = PdfDocumentReader.Open(bytes, null);
        var header = await PdfDocumentLinearization.GetInfoAsync(document, CancellationToken.None);

        await Assert.That(header).IsNotNull();
        await Assert.That(header!.FileLength).IsEqualTo(bytes.Length);
        await Assert.That(header.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
        await Assert.That(header.FirstPageIndex).IsEqualTo(0);
        await Assert.That(header.FirstPageObjectNumber).IsEqualTo(FirstPageNumber);
        await Assert.That(header.FirstPageEndOffset).IsGreaterThan(header.Hints[0].Offset);
        await Assert.That(bytes.AsSpan((int)header.Hints[0].Offset).StartsWith("8 0 obj"u8)).IsTrue();
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsFalse();
    }

    /// <summary>A mismatched /L leaves the file readable but removes its linearized claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongFileLengthIsNotLinearized()
    {
        var bytes = LinearizedDocuments.Create(false);
        var lengthValue = bytes.AsSpan().IndexOf("/L "u8) + LengthValueOffset;
        bytes[lengthValue] = (byte)'9';
        using var document = PdfDocumentReader.Open(bytes, null);

        await Assert.That(PdfDocumentLinearization.GetInfo(document)).IsNull();
        await Assert.That(document.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsTrue();
    }

    /// <summary>The page-offset hint table reports both page groups and their byte ranges.</summary>
    /// <param name="streams">Whether the xref sections are streams.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PageHintsDecodeOffsets(bool streams)
    {
        var bytes = LinearizedDocuments.Create(streams);
        using var document = PdfDocumentReader.Open(bytes, null);
        var header = PdfDocumentLinearization.GetInfo(document)!;
        var pages = await PdfLinearizationHints.GetPageRangesAsync(document, CancellationToken.None);
        var firstObjectOffset = bytes.AsSpan().IndexOf("5 0 obj"u8);

        await Assert.That(pages).IsNotNull();
        await Assert.That(pages!.Length).IsEqualTo(LinearizedDocuments.PageCount);
        await Assert.That(pages[0].Offset).IsEqualTo(firstObjectOffset);
        await Assert.That(pages[0].Length).IsEqualTo(header.FirstPageEndOffset - firstObjectOffset);
        await Assert.That(pages[0].FirstObjectNumber).IsEqualTo(FirstPageNumber);
        await Assert.That(pages[0].ObjectCount).IsEqualTo(FirstPageObjects);
        await Assert.That(pages[1].Offset).IsEqualTo(header.FirstPageEndOffset);
        await Assert.That(pages[1].Length).IsEqualTo(bytes.AsSpan().IndexOf("3 0 obj"u8) - header.FirstPageEndOffset);
        await Assert.That(pages[1].FirstObjectNumber).IsEqualTo(1);
        await Assert.That(pages[1].ObjectCount).IsEqualTo(SecondPageObjects);
    }

    /// <summary>Primary and second /H ranges decode together without changing the page offsets.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondHintRangeDecodes()
    {
        var bytes = LinearizedDocuments.CreateWithSecondHint();
        using var document = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentLinearization.GetInfo(document)!;
        var asyncRanges = await PdfLinearizationHints.GetPageRangesAsync(document, CancellationToken.None);

        await Assert.That(info.Hints.Length).IsEqualTo(BothHintRanges);
        await Assert.That(asyncRanges).IsNotNull();
        await Assert.That(asyncRanges![0].Offset).IsEqualTo(bytes.AsSpan().IndexOf("5 0 obj"u8));
        await Assert.That(asyncRanges![1].Offset).IsEqualTo(info.FirstPageEndOffset);
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsFalse();
    }

    /// <summary>The synchronous hint API reads the same two page groups from an in-memory file.</summary>
    /// <exception cref="InvalidOperationException">The hint table did not decode.</exception>
    [Test]
    public void SynchronousPageHintsDecode()
    {
        foreach (var bytes in new[] { LinearizedDocuments.Create(false), LinearizedDocuments.CreateWithSecondHint() })
        {
            using var document = PdfDocumentReader.Open(bytes, null);
            var pages = PdfLinearizationHints.GetPageRanges(document, CancellationToken.None);
            if (pages is not { Length: LinearizedDocuments.PageCount } || pages[0].FirstObjectNumber != FirstPageNumber)
            {
                throw new InvalidOperationException("The synchronous page hints did not decode the first page.");
            }
        }
    }

    /// <summary>A damaged packed width rejects the hint table without losing the readable pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidHintWidthIsIgnored()
    {
        var bytes = LinearizedDocuments.Create(false);
        int hintOffset;
        using (var original = PdfDocumentReader.Open(bytes, null))
        {
            hintOffset = (int)PdfDocumentLinearization.GetInfo(original)!.Hints[0].Offset;
        }

        var streamKeyword = bytes.AsSpan(hintOffset).IndexOf(StreamKeyword);
        await Assert.That(streamKeyword).IsGreaterThanOrEqualTo(0);
        var dataOffset = hintOffset + streamKeyword + StreamKeyword.Length;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(dataOffset + PageLengthBitsOffset), InvalidBitWidth);
        using var document = PdfDocumentReader.Open(bytes, null);
        await Assert.That(await PdfLinearizationHints.GetPageRangesAsync(document, CancellationToken.None)).IsNull();
        await Assert.That(document.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsTrue();
    }

    /// <summary>The checker catches a plausible but wrong /E or /T value.</summary>
    /// <param name="endOfFirstPage">Whether to damage /E instead of /T.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task WrongSectionOffsetIsReported(bool endOfFirstPage)
    {
        var bytes = LinearizedDocuments.Create(false);
        var key = endOfFirstPage ? "/E "u8 : "/T "u8;
        var value = bytes.AsSpan().IndexOf(key) + key.Length;
        var last = value + OffsetDigits - 1;
        bytes[last] = bytes[last] == (byte)'0' ? (byte)'1' : (byte)'0';
        using var document = PdfDocumentReader.Open(bytes, null);

        await Assert.That(PdfDocumentLinearization.GetInfo(document)).IsNotNull();
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsTrue();
    }

    /// <summary>/T may point at the whitespace immediately before the first object-zero xref entry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MainXrefWhitespaceOffsetIsAccepted()
    {
        var bytes = LinearizedDocuments.Create(false);
        var value = bytes.AsSpan().IndexOf("/T "u8) + "/T "u8.Length;
        var last = value + OffsetDigits - 1;
        await Assert.That(bytes[last]).IsGreaterThan((byte)'0');
        bytes[last]--;
        using var document = PdfDocumentReader.Open(bytes, null);

        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsFalse();
    }

    /// <summary>A page hint that includes the page-tree object outside its byte range is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongPageObjectCountIsReported()
    {
        var bytes = LinearizedDocuments.Create(false);
        int hintOffset;
        using (var original = PdfDocumentReader.Open(bytes, null))
        {
            hintOffset = (int)PdfDocumentLinearization.GetInfo(original)!.Hints[0].Offset;
        }

        var streamKeyword = bytes.AsSpan(hintOffset).IndexOf(StreamKeyword);
        var dataOffset = hintOffset + streamKeyword + StreamKeyword.Length;
        bytes[dataOffset + PageObjectCountsOffset] = SecondPageObjectDelta;
        using var document = PdfDocumentReader.Open(bytes, null);

        await Assert.That(await PdfLinearizationHints.GetPageRangesAsync(document, CancellationToken.None)).IsNotNull();
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsTrue();
    }

    /// <summary>A shared-object count without its packed entries is reported as a damaged hint table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedSharedHintsAreReported()
    {
        var bytes = LinearizedDocuments.Create(false);
        int hintOffset;
        using (var original = PdfDocumentReader.Open(bytes, null))
        {
            hintOffset = (int)PdfDocumentLinearization.GetInfo(original)!.Hints[0].Offset;
        }

        var streamKeyword = bytes.AsSpan(hintOffset).IndexOf(StreamKeyword);
        var dataOffset = hintOffset + streamKeyword + StreamKeyword.Length;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(dataOffset + SharedTableOffset + SharedTotalOffset), 1);
        using var document = PdfDocumentReader.Open(bytes, null);

        await Assert.That(await PdfLinearizationHints.GetPageRangesAsync(document, CancellationToken.None)).IsNull();
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsTrue();
    }

    /// <summary>The linearization dictionary and hint stream are plain objects that read back and do no harm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HintStreamAndLinearizationDictionaryRead()
    {
        foreach (var streams in new[]
        {
            false,
            true,
        }

        )
        {
            using var document = PdfDocumentReader.Open(LinearizedDocuments.Create(streams), null);
            var linearization = StoreReading.GetDictionary(document.Objects, new(LinearizationNumber, 0))!;
            var hint = StoreReading.GetObject(document.Objects, new(HintNumber, 0)).AsStream()!;
            await Assert.That(linearization.GetInt32(document.Objects.Names.Intern("Linearized"))).IsEqualTo(1);
            await Assert.That(linearization.GetInt32(KnownName.N)).IsEqualTo(LinearizedDocuments.PageCount);
            await Assert.That(hint.RawLength).IsEqualTo(HintLength);
            await Assert.That(DocumentExerciser.Read(document)).IsNotEmpty();
        }
    }

    /// <summary>An incremental update appended to a linearized file reads back with the edit and the original pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalUpdateOfLinearizedFileReadsBack()
    {
        foreach (var streams in new[]
        {
            false,
            true,
        }

        )
        {
            var original = LinearizedDocuments.Create(streams);
            using var store = StoreOpening.Open(original, null);
            var marker = StoreEditing.Add(store, PdfValue.FromInteger(SaveReopenTests.MarkerValue));
            var catalogId = store.Trailer.GetRaw(KnownName.Root).AsReference();
            var catalog = store.Catalog.Clone();
            catalog.Set(store.Names.Intern(SaveReopenTests.MarkerKey), PdfValue.FromReference(marker));
            StoreEditing.Replace(store, catalogId, PdfValue.FromDictionary(catalog));
            var saved = PdfIncrementalWriter.Save(store);
            using var reopened = PdfDocumentReader.Open(saved, null);
            await Assert.That(saved.AsSpan(0, original.Length).SequenceEqual(original)).IsTrue();
            await Assert.That(reopened.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
            await Assert.That(reopened.Catalog.GetInteger(store.Names.Intern(SaveReopenTests.MarkerKey))).IsEqualTo(SaveReopenTests.MarkerValue);
        }
    }

    /// <summary>A linearized file with a spoiled startxref is rebuilt and still gives both pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedFileWithBrokenStartXrefRepairs()
    {
        foreach (var streams in new[]
        {
            false,
            true,
        }

        )
        {
            var broken = Pdf2Documents.Replace(LinearizedDocuments.Create(streams), "startxref", "startxxxx");
            using var store = StoreOpening.Open(broken, null);
            var contents = WritingTestDocuments.PageContents(store);
            await Assert.That(store.WasRepaired).IsTrue();
            await Assert.That(contents.Count).IsEqualTo(LinearizedDocuments.PageCount);
            await Assert.That(Encoding.Latin1.GetString(contents[1])).IsEqualTo(LinearizedDocuments.SecondContent);
        }
    }

    /// <summary>A linearized file cut off after its first page, as in a partial download, still shows the first page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedFileCutAfterFirstPageShowsFirstPage()
    {
        foreach (var streams in new[]
        {
            false,
            true,
        }

        )
        {
            var full = LinearizedDocuments.Create(streams);
            int end;
            using (var store = StoreOpening.Open(full, null))
            {
                end = StoreReading.GetDictionary(store, new(LinearizationNumber, 0))!.GetInt32(store.Names.Intern("E"));
            }

            using var document = PdfDocumentReader.Open(full.AsSpan(0, end).ToArray(), null);
            var content = PdfDocumentPages.GetPage(document, 0).Dictionary.GetStream(KnownName.Contents)!.DecodeToArray();
            await Assert.That(document.PageCount).IsGreaterThanOrEqualTo(1);
            await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(FirstWidth);
            await Assert.That(Encoding.Latin1.GetString(content)).IsEqualTo(LinearizedDocuments.FirstContent);
        }
    }
}
