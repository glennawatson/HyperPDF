// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Creation;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>
/// Tests that a check decodes each page's content once and shares it between its readers, and that the shared content is
/// never served for another page, after an edit, after the scope is disposed, or above the size limits.
/// </summary>
public sealed class PdfPageContentScopeTests
{
    /// <summary>The width of the test page.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test page.</summary>
    private const float PageHeight = 400F;

    /// <summary>The number of readers that ask for one page at once.</summary>
    private const int Readers = 8;

    /// <summary>An untagged filled rectangle, which a tagged file may not draw.</summary>
    private const string UntaggedRectangle = "0 0 10 10 re f";

    /// <summary>The label of the test edits.</summary>
    private const string EditLabel = "Replace page content";

    /// <summary>One more byte than the scope keeps for a page.</summary>
    private const int OversizedLength = PdfPageContentScope.MaxPageBytes + 1;

    /// <summary>Two readers of one page in the same scope get the same decoded array, and it holds the page's content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadersOfOnePageShareOneDecode()
    {
        using var document = OpenTwoPages();
        using var scope = PdfPageContentScope.Begin(document);
        var page = PdfDocumentPages.GetPage(document, 0);

        using var first = PdfPageContentScope.Lease(document, page, CancellationToken.None);
        using var second = PdfPageContentScope.Lease(document, page, CancellationToken.None);

        await Assert.That(ReferenceEquals(first.Array, second.Array)).IsTrue();
        await Assert.That(first.Content.SequenceEqual(Decode(page))).IsTrue();
        await Assert.That(first.Owns).IsFalse();
    }

    /// <summary>Each page keeps its own content: another page's lease is a different array with that page's bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesDoNotShareContent()
    {
        using var document = OpenTwoPages();
        using var scope = PdfPageContentScope.Begin(document);
        var one = PdfDocumentPages.GetPage(document, 0);
        var two = PdfDocumentPages.GetPage(document, 1);

        using var first = PdfPageContentScope.Lease(document, one, CancellationToken.None);
        using var second = PdfPageContentScope.Lease(document, two, CancellationToken.None);
        using var firstAgain = PdfPageContentScope.Lease(document, one, CancellationToken.None);

        await Assert.That(ReferenceEquals(first.Array, second.Array)).IsFalse();
        await Assert.That(first.Content.SequenceEqual(second.Content)).IsFalse();
        await Assert.That(second.Content.SequenceEqual(Decode(two))).IsTrue();
        await Assert.That(ReferenceEquals(first.Array, firstAgain.Array)).IsTrue();
    }

    /// <summary>Content kept before an edit is not served after it: the scope decodes the edited page again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditedDocumentIsNotServedKeptContent()
    {
        using var document = OpenTwoPages();
        using var scope = PdfPageContentScope.Begin(document);
        var page = PdfDocumentPages.GetPage(document, 0);
        using var before = PdfPageContentScope.Lease(document, page, CancellationToken.None);
        var beforeBytes = before.Content.ToArray();

        ReplaceContent(document.Objects, page, UntaggedRectangle);
        PdfDocumentEditing.InvalidateCaches(document);
        using var after = PdfPageContentScope.Lease(document, PdfDocumentPages.GetPage(document, 0), CancellationToken.None);

        await Assert.That(ReferenceEquals(before.Array, after.Array)).IsFalse();
        await Assert.That(after.Owns).IsTrue();
        await Assert.That(Encoding.ASCII.GetString(after.Content)).IsEqualTo(UntaggedRectangle);
        await Assert.That(beforeBytes.AsSpan().SequenceEqual(after.Content)).IsFalse();
    }

    /// <summary>A scope that is disposed leaves the document with none, so a later reader decodes for itself and owns the result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposedScopeServesNothing()
    {
        using var document = OpenTwoPages();
        var page = PdfDocumentPages.GetPage(document, 0);
        PdfPageContentScope.Begin(document).Dispose();

        using var lease = PdfPageContentScope.Lease(document, page, CancellationToken.None);

        await Assert.That(lease.Owns).IsTrue();
        await Assert.That(lease.Content.SequenceEqual(Decode(page))).IsTrue();
        await Assert.That(PdfPageContentScope.TryLease(document, page, CancellationToken.None, out _)).IsFalse();
    }

    /// <summary>A scope opened while another is open does nothing, and disposing it leaves the first scope serving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedScopeLeavesTheOuterScopeServing()
    {
        using var document = OpenTwoPages();
        using var outer = PdfPageContentScope.Begin(document);
        var page = PdfDocumentPages.GetPage(document, 0);
        using var first = PdfPageContentScope.Lease(document, page, CancellationToken.None);

        PdfPageContentScope.Begin(document).Dispose();
        using var second = PdfPageContentScope.Lease(document, page, CancellationToken.None);

        await Assert.That(ReferenceEquals(first.Array, second.Array)).IsTrue();
    }

    /// <summary>A page above the per-page limit is decoded for each reader and owned by it, not kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedPageIsNotKept()
    {
        using var document = OpenTwoPages();
        ReplaceContent(document.Objects, PdfDocumentPages.GetPage(document, 0), string.Empty.PadRight(OversizedLength));
        PdfDocumentEditing.InvalidateCaches(document);
        using var scope = PdfPageContentScope.Begin(document);
        var page = PdfDocumentPages.GetPage(document, 0);

        using var first = PdfPageContentScope.Lease(document, page, CancellationToken.None);
        using var second = PdfPageContentScope.Lease(document, page, CancellationToken.None);

        await Assert.That(first.Owns).IsTrue();
        await Assert.That(ReferenceEquals(first.Array, second.Array)).IsFalse();
        await Assert.That(first.Length).IsEqualTo(OversizedLength);
    }

    /// <summary>A damaged stream is shared as the decoder recovers it, which is what an unshared reader would get.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedContentIsSharedAsDecoded()
    {
        using var document = OpenTwoPages();
        CorruptContent(document.Objects, PdfDocumentPages.GetPage(document, 0));
        PdfDocumentEditing.InvalidateCaches(document);
        using var scope = PdfPageContentScope.Begin(document);
        var damaged = PdfDocumentPages.GetPage(document, 0);

        var shared = PdfPageContentScope.TryLease(document, damaged, CancellationToken.None, out var first);
        using var second = PdfPageContentScope.Lease(document, damaged, CancellationToken.None);
        using (first)
        {
            await Assert.That(shared).IsTrue();
            await Assert.That(first.Content.SequenceEqual(Decode(damaged))).IsTrue();
            await Assert.That(ReferenceEquals(first.Array, second.Array)).IsTrue();
        }
    }

    /// <summary>Readers on many threads all see the page's own bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentReadersSeeTheSameContent()
    {
        using var document = OpenTwoPages();
        using var scope = PdfPageContentScope.Begin(document);
        byte[][] expected = [Decode(PdfDocumentPages.GetPage(document, 0)), Decode(PdfDocumentPages.GetPage(document, 1))];
        var tasks = new Task<bool>[Readers];
        for (var i = 0; i < tasks.Length; i++)
        {
            var index = i % expected.Length;
            tasks[i] = Task.Run(() =>
            {
                using var lease = PdfPageContentScope.Lease(document, PdfDocumentPages.GetPage(document, index), CancellationToken.None);
                return lease.Content.SequenceEqual(expected[index]);
            });
        }

        var results = await Task.WhenAll(tasks);

        await Assert.That(Array.TrueForAll(results, static same => same)).IsTrue();
    }

    /// <summary>
    /// Two checks of one store with an edit between them: the second check reads the edited content, not the first check's
    /// kept content, so the untagged drawing the edit added is found.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckAfterEditReadsTheEditedContent()
    {
        using var document = OpenClaimed();
        var store = document.Objects;
        var before = PdfUaPreflight.Check(store, false, CancellationToken.None);

        ReplaceContent(store, PdfDocumentPages.GetPage(document, 0), UntaggedRectangle);
        var after = PdfUaPreflight.Check(store, true, CancellationToken.None);

        await Assert.That(before.Exists(static finding => finding.Requirement == PdfUaRequirement.UntaggedContent)).IsFalse();
        await Assert.That(after.Exists(static finding => finding.Requirement == PdfUaRequirement.UntaggedContent && finding.PageIndex == 0)).IsTrue();
        await Assert.That(after.Exists(static finding => finding.Requirement == PdfUaRequirement.UntaggedContent && finding.PageIndex == 1)).IsFalse();
    }

    /// <summary>A check leaves no scope on the view it opened, so the same findings come from repeating it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedChecksGiveTheSameFindings()
    {
        using var document = OpenClaimed();
        var store = document.Objects;
        ReplaceContent(store, PdfDocumentPages.GetPage(document, 1), UntaggedRectangle);

        var first = PdfUaPreflight.Check(store, true, CancellationToken.None);
        var second = PdfUaPreflight.Check(store, true, CancellationToken.None);

        await Assert.That(first.Exists(static finding => finding.Requirement == PdfUaRequirement.UntaggedContent && finding.PageIndex == 1)).IsTrue();
        await Assert.That(second.SequenceEqual(first)).IsTrue();
    }

    /// <summary>Opens a two page document with different text on each page.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument OpenTwoPages() => PdfDocumentReader.Open(CreateTwoPages(false), null);

    /// <summary>Opens a two page PDF/UA-1 document with different text on each page.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument OpenClaimed() => PdfDocumentReader.Open(CreateTwoPages(true), null);

    /// <summary>Makes a document with a paragraph on each of two pages.</summary>
    /// <param name="claimed">Whether the file claims PDF/UA-1.</param>
    /// <returns>The file.</returns>
    private static byte[] CreateTwoPages(bool claimed)
    {
        var creator = new PdfDocumentCreator(new() { Title = "Pages", Language = "en", Tagged = true, UaClaim = claimed ? PdfUaConformance.Part1 : PdfUaConformance.None });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        PdfFlowBlock[] blocks =
        [
            new PdfFlowParagraph("Text on the first page."),
            new PdfFlowPageBreak(),
            new PdfFlowParagraph("Different words on the second page, which are longer."),
        ];
        _ = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight }, CancellationToken.None);
        _ = creator.Finish();
        return creator.ToArray();
    }

    /// <summary>Decodes a page's content directly, without a scope.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The content.</returns>
    private static byte[] Decode(PdfPage page)
    {
        var buffer = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref buffer, CancellationToken.None);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Finds the page's first content stream.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The stream.</returns>
    private static PdfStream FirstContent(PdfPage page)
    {
        var contents = page.Dictionary.Get(KnownName.Contents);
        return contents.AsStream() ?? contents.AsArray()!.Get(0).AsStream()!;
    }

    /// <summary>Replaces the page's first content stream with plain text content and leaves any other stream of the page empty.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="page">The page.</param>
    /// <param name="content">The new content.</param>
    private static void ReplaceContent(PdfObjectStore store, PdfPage page, string content)
    {
        var stream = FirstContent(page);
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        Replace(store, stream, dictionary, Encoding.ASCII.GetBytes(content));
    }

    /// <summary>Makes the page's first content stream claim Flate compression while holding bytes that are not.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="page">The page.</param>
    private static void CorruptContent(PdfObjectStore store, PdfPage page)
    {
        var stream = FirstContent(page);
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(store.Names.Intern("FlateDecode"u8)));
        Replace(store, stream, dictionary, "this is not a zlib stream"u8.ToArray());
    }

    /// <summary>Replaces a stream object as one edit.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="stream">The stream to replace.</param>
    /// <param name="dictionary">The new stream dictionary.</param>
    /// <param name="data">The new stream data.</param>
    private static void Replace(PdfObjectStore store, PdfStream stream, PdfDictionary dictionary, byte[] data)
    {
        var transaction = PdfGuardTransactions.JoinOrBegin(store, EditLabel, out var owned);
        transaction.Replace(stream.Id, PdfValue.FromStream(new(dictionary, data)));
        if (owned)
        {
            transaction.Commit();
        }
    }
}
