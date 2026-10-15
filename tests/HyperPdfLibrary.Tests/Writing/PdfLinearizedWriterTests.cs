// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Checks that linearized saves preserve pages and write usable hint groups.</summary>
public sealed class PdfLinearizedWriterTests
{
    /// <summary>The media-box width of the test pages.</summary>
    private const int PageWidth = 200;

    /// <summary>The media-box height of the test pages.</summary>
    private const int PageHeight = 100;

    /// <summary>The case with an object shared only by later pages.</summary>
    private const int LaterSharedPageCount = 3;

    /// <summary>The parts of a file identifier.</summary>
    private const int IdParts = 2;

    /// <summary>Gets the raw stream-data marker in the unfiltered writer output.</summary>
    private static ReadOnlySpan<byte> StreamKeyword => "stream\r\n"u8;

    /// <summary>Repeated saves rebuild their object graph after metadata edits and undo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Save_CachedGraphFollowsMetadataEditAndUndo()
    {
        const string editedTitle = "Edited title";
        using var source = PdfDocumentReader.Open(CreatePages(1), null);
        var originalTitle = PdfDocumentMetadata.GetInfo(source).Title;
        _ = PdfLinearizedWriter.Save(source);

        PdfDocumentMetadataEditing.SetMetadata(source, new() { Title = editedTitle });
        using var changed = PdfDocumentReader.Open(PdfLinearizedWriter.Save(source), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(changed).Title).IsEqualTo(editedTitle);
        await AssertNoLinearizationFaults(changed);

        await Assert.That(PdfDocumentEditing.Undo(source)).IsTrue();
        using var restored = PdfDocumentReader.Open(PdfLinearizedWriter.Save(source), null);
        await Assert.That(PdfDocumentMetadata.GetInfo(restored).Title).IsEqualTo(originalTitle);
        await AssertNoLinearizationFaults(restored);
    }

    /// <summary>One page and three pages with later-only shared fonts round trip with correct hints.</summary>
    /// <param name="pageCount">The number of pages to write.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(LaterSharedPageCount)]
    public async Task Save_PageGroupsRoundTrip(int pageCount)
    {
        using var source = PdfDocumentReader.Open(CreatePages(pageCount), null);
        var bytes = PdfLinearizedWriter.Save(source);
        using var reopened = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentLinearization.GetInfo(reopened);
        var hints = await PdfLinearizationHints.GetPageRangesAsync(reopened, CancellationToken.None);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.FileLength).IsEqualTo(bytes.Length);
        await Assert.That(info.PageCount).IsEqualTo(pageCount);
        await Assert.That(hints).IsNotNull();
        await Assert.That(hints!.Length).IsEqualTo(pageCount);
        for (var index = 0; index < pageCount; index++)
        {
            await Assert.That(hints[index].Offset).IsGreaterThan(0);
            await Assert.That(hints[index].FirstObjectNumber).IsEqualTo(PdfDocumentPages.GetPage(reopened, index).Id.Number);
            await Assert.That(PdfDocumentText.GetTextPage(reopened, index).Text).IsEqualTo(PdfDocumentText.GetTextPage(source, index).Text);
        }

        await AssertNoLinearizationFaults(reopened);
    }

    /// <summary>A form and outline book retains its text, widgets and outline destinations.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Save_FormsAndOutlinesRoundTrip()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        var bytes = PdfLinearizedWriter.Save(source);
        using var reopened = PdfDocumentReader.Open(bytes, null);
        var info = PdfDocumentLinearization.GetInfo(reopened);
        var hints = await PdfLinearizationHints.GetPageRangesAsync(reopened, CancellationToken.None);

        await Assert.That(info).IsNotNull();
        await Assert.That(hints).IsNotNull();
        await Assert.That(reopened.PageCount).IsEqualTo(CarryTestDocuments.BookPages);
        for (var index = 0; index < reopened.PageCount; index++)
        {
            await Assert.That(PdfDocumentText.GetTextPage(reopened, index).Text).IsEqualTo(PdfDocumentText.GetTextPage(source, index).Text);
            await Assert.That(CarryTestDocuments.WidgetNames(reopened, index).SequenceEqual(CarryTestDocuments.WidgetNames(source, index))).IsTrue();
        }

        await Assert.That(CarryTestDocuments.OutlinePages(reopened).SequenceEqual(CarryTestDocuments.OutlinePages(source))).IsTrue();
        var hintStream = ReadHintStream(reopened, info!);
        await Assert.That(hintStream.Dictionary.GetInt32(KnownName.O)).IsGreaterThan(0);
        await AssertNoLinearizationFaults(reopened);
    }

    /// <summary>An outline pane requested on open places its outline objects inside the first-page section.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Save_OpeningOutlinePaneStaysInFirstSection()
    {
        var original = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Outlines 5 0 R /PageMode /UseOutlines >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << >> >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << >> >>",
            "<< /Type /Outlines /First 6 0 R /Last 6 0 R /Count 1 >>",
            "<< /Title (One) /Parent 5 0 R /Dest [3 0 R /Fit] >>");
        using var source = PdfDocumentReader.Open(original, null);
        using var reopened = PdfDocumentReader.Open(PdfLinearizedWriter.Save(source), null);
        var info = PdfDocumentLinearization.GetInfo(reopened)!;
        var outline = reopened.Catalog.GetDictionary(KnownName.Outlines)!;
        var firstOutline = outline.GetRaw(KnownName.First).AsReference();
        var outlineOffset = StoreRevisions.GetDefinitionOffset(reopened.Objects, firstOutline.Number);

        await Assert.That(outlineOffset).IsLessThan(info.FirstPageEndOffset);
        await Assert.That(ReadHintStream(reopened, info).Dictionary.GetInt32(KnownName.O)).IsGreaterThan(0);
        await AssertNoLinearizationFaults(reopened);
    }

    /// <summary>The checker reports an outline hint whose declared first object does not exist.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Save_DamagedOutlineHintIsReported()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        var bytes = PdfLinearizedWriter.Save(source);
        int hintOffset;
        int outlineOffset;
        using (var original = PdfDocumentReader.Open(bytes, null))
        {
            var info = PdfDocumentLinearization.GetInfo(original)!;
            hintOffset = (int)info.Hints[0].Offset;
            outlineOffset = ReadHintStream(original, info).Dictionary.GetInt32(KnownName.O);
        }

        var marker = bytes.AsSpan(hintOffset).IndexOf(StreamKeyword);
        var dataOffset = hintOffset + marker + StreamKeyword.Length;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(dataOffset + outlineOffset), 0);
        using var damaged = PdfDocumentReader.Open(bytes, null);

        await Assert.That(await PdfLinearizationHints.GetPageRangesAsync(damaged, CancellationToken.None)).IsNull();
        var report = await PdfDocumentCheck.CheckAsync(damaged, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsTrue();
    }

    /// <summary>The async save writes the same unencrypted file and honors cancellation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveAsync_WritesFileAndHonorsCancellation()
    {
        using var document = PdfDocumentReader.Open(CreatePages(1), null);
        var expected = PdfLinearizedWriter.Save(document);
        await using var output = new MemoryStream();
        await PdfLinearizedWriter.SaveAsync(document, output, CancellationToken.None);
        await Assert.That(output.ToArray().AsSpan().SequenceEqual(expected)).IsTrue();

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(async () => await PdfLinearizedWriter.SaveAsync(document, output, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>A page that reaches a long chain of indirect objects, such as a linked list, linearizes with every link kept (#181).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Save_LongReferenceChainLinearizes()
    {
        const int links = 500;
        var builder = new PdfDocumentBuilder();
        var next = builder.Names.Intern("Next"u8);
        var head = default(PdfObjectId);
        for (var index = 0; index < links; index++)
        {
            var link = new PdfDictionary(null);
            if (head.IsValid)
            {
                link.Set(next, PdfValue.FromReference(head));
            }

            head = builder.Add(PdfValue.FromDictionary(link));
        }

        var page = new PdfDictionary(null);
        page.Set(KnownName.MediaBox, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 0, PageWidth, PageHeight])));
        page.Set(builder.Names.Intern("Chain"u8), PdfValue.FromReference(head));
        _ = builder.AddPage(page);
        using var source = PdfDocumentReader.Open(builder.ToArray(), null);
        using var reopened = PdfDocumentReader.Open(PdfLinearizedWriter.Save(source), null);
        var count = 0;
        var chain = reopened.Objects.Names.Intern("Chain"u8);
        var nextLink = reopened.Objects.Names.Intern("Next"u8);
        for (var link = PdfDocumentPages.GetPage(reopened, 0).Dictionary.GetDictionary(chain); link is not null; link = link.GetDictionary(nextLink))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(links);
        await AssertNoLinearizationFaults(reopened);
    }

    /// <summary>Saving the same document twice gives the same bytes, with one /ID shared by both trailers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Save_IsDeterministic()
    {
        using var source = PdfDocumentReader.Open(CreatePages(LaterSharedPageCount), null);
        var first = PdfLinearizedWriter.Save(source);
        var second = PdfLinearizedWriter.Save(source);
        using var reopened = PdfDocumentReader.Open(first, null);
        await Assert.That(second).IsEquivalentTo(first);
        await Assert.That(reopened.Objects.Trailer.GetArray(KnownName.ID)?.Count).IsEqualTo(IdParts);
    }

    /// <summary>Builds pages with a font shared by the later two pages.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The source file.</returns>
    private static byte[] CreatePages(int pageCount)
    {
        var builder = new PdfDocumentBuilder();
        var firstFont = builder.Add(PdfValue.FromDictionary(Font(builder, "Helvetica"u8)));
        var laterFont = builder.Add(PdfValue.FromDictionary(Font(builder, "Courier"u8)));
        for (var index = 0; index < pageCount; index++)
        {
            var content = new PdfStream(new(null), Encoding.ASCII.GetBytes($"BT /F1 12 Tf 50 50 Td (Page {index + 1}) Tj ET"));
            var contentId = builder.Add(PdfValue.FromStream(content));
            var fonts = new PdfDictionary(null);
            fonts.Set(builder.Names.Intern("F1"u8), PdfValue.FromReference(index == 0 ? firstFont : laterFont));
            var resources = new PdfDictionary(null);
            resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
            var page = new PdfDictionary(null);
            page.Set(KnownName.MediaBox, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 0, PageWidth, PageHeight])));
            page.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
            page.Set(KnownName.Contents, PdfValue.FromReference(contentId));
            _ = builder.AddPage(page);
        }

        return builder.ToArray();
    }

    /// <summary>Builds a Standard 14 font dictionary.</summary>
    /// <param name="builder">The name table owner.</param>
    /// <param name="name">The base font name.</param>
    /// <returns>The font dictionary.</returns>
    private static PdfDictionary Font(PdfDocumentBuilder builder, ReadOnlySpan<byte> name)
    {
        var font = new PdfDictionary(null);
        font.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        font.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type1));
        font.Set(KnownName.BaseFont, PdfValue.FromName(builder.Names.Intern(name)));
        return font;
    }

    /// <summary>Parses the hint stream at the header's declared file offset.</summary>
    /// <param name="document">The reopened file.</param>
    /// <param name="info">Its linearization header.</param>
    /// <returns>The hint stream.</returns>
    /// <exception cref="InvalidOperationException">The declared object is missing.</exception>
    private static PdfStream ReadHintStream(PdfDocument document, PdfLinearizationInfo info)
    {
        if (!StoreParsing.TryParseObjectAt(document.Objects, info.Hints[0].Offset, out _, out var value) || value.AsStream() is not { } stream)
        {
            throw new InvalidOperationException("The saved hint stream is missing.");
        }

        return stream;
    }

    /// <summary>Checks the library's linearization diagnostic on a saved file.</summary>
    /// <param name="document">The reopened file.</param>
    /// <returns>A task.</returns>
    private static async Task AssertNoLinearizationFaults(PdfDocument document)
    {
        var report = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default with { DecodeStreams = false, ParseContent = false }, CancellationToken.None);
        await Assert.That(report.Faults.Any(static fault => fault.Code == PdfDiagnosticCode.BadLinearization)).IsFalse();
    }
}
