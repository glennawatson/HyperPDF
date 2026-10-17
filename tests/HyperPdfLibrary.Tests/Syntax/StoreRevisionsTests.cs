// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests the parsing limits retained when opening signed document revisions.</summary>
public sealed class StoreRevisionsTests
{
    /// <summary>The small output ceiling of the opened document.</summary>
    private const int DecodeLimit = 64;

    /// <summary>The page-tree object's number.</summary>
    private const int PageTreeObject = 2;

    /// <summary>The number of cross-reference entries.</summary>
    private const int XrefSize = 3;

    /// <summary>The sections in a document with one incremental update.</summary>
    private const int IncrementalSectionCount = 2;

    /// <summary>The catalog of the raw document.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The empty page tree of the raw document.</summary>
    private const string PageTree = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The trailer's catalog reference.</summary>
    private const string RootTrailer = "/Root 1 0 R";

    /// <summary>Signed-revision reads cannot reset the caller's decoding ceiling, recovery switches or cancellation.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenRevision_RestrictedContext_PreservesReadLimits(CancellationToken cancellationToken)
    {
        var file = new RawPdf()
            .Object(1, Catalog)
            .Object(PageTreeObject, PageTree)
            .Table(XrefSize, RootTrailer).ToArray();
        var options = new PdfOpenOptions() with
        {
            Recovery = false,
            IgnoreXrefStreams = true,
            MaxDecompressedBytes = DecodeLimit,
            CancellationToken = cancellationToken,
        };
        using var current = StoreOpening.OpenWith(file, options);
        using var revision = StoreRevisions.OpenRevision(current, file.LongLength);

        await Assert.That(revision.Context!.MaxDecompressedBytes).IsEqualTo(DecodeLimit);
        await Assert.That(revision.Context.Recovery).IsFalse();
        await Assert.That(revision.Context.IgnoreXrefStreams).IsTrue();
        await Assert.That(revision.Context.CancellationToken).IsEqualTo(cancellationToken);
    }

    /// <summary>Relative offsets cannot overflow when a malformed previous revision points outside the file.</summary>
    /// <param name="previous">The hostile previous section offset.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(long.MaxValue)]
    [Arguments((long)int.MaxValue)]
    public async Task ReadSectionOffsets_PreviousOffsetOutsideSource_ThrowsDomainException(long previous, CancellationToken cancellationToken)
    {
        var raw = new RawPdf()
            .Object(1, Catalog)
            .Object(PageTreeObject, PageTree)
            .Table(XrefSize, string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Prev {previous}"))
            .ToArray();
        byte[] file = [.. "junk bytes\n"u8, .. raw];
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions() with { CancellationToken = cancellationToken });

        await Assert.That(store.XrefState.OffsetBase).IsGreaterThan(0);
        await Assert.That(() => ReadSections(store)).Throws<PdfException>();
    }

    /// <summary>A circular previous-section link returns its section only once.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadSectionOffsets_CircularPreviousOffset_ReadsSectionOnce(CancellationToken cancellationToken)
    {
        var raw = new RawPdf()
            .Object(1, Catalog)
            .Object(PageTreeObject, PageTree);
        var offset = raw.Position;
        var file = raw.Table(XrefSize, string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Prev {offset}")).ToArray();
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions() with { CancellationToken = cancellationToken });
        var sections = ReadSections(store);

        await Assert.That(sections.Count).IsEqualTo(1);
        await Assert.That(sections.First).IsEqualTo(offset);
    }

    /// <summary>Section positions remain ordered newest first across incremental updates.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadSectionOffsets_IncrementalUpdate_PreservesNewestFirstOrder(CancellationToken cancellationToken)
    {
        var raw = new RawPdf()
            .Object(1, Catalog)
            .Object(PageTreeObject, PageTree);
        var first = raw.Position;
        _ = raw.Table(XrefSize, RootTrailer);
        var second = raw.Position;
        var file = raw.Table(XrefSize, string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Prev {first}")).ToArray();
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions() with { CancellationToken = cancellationToken });
        var sections = ReadSections(store);

        await Assert.That(sections.Count).IsEqualTo(IncrementalSectionCount);
        await Assert.That(sections.First).IsEqualTo(second);
        await Assert.That(sections.Second).IsEqualTo(first);
    }

    /// <summary>Detached revision snapshots retain oldest-first order and genuine EOF boundaries.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadRevisions_IncrementalUpdate_PreservesBoundaries(CancellationToken cancellationToken)
    {
        var raw = new RawPdf()
            .Object(1, Catalog)
            .Object(PageTreeObject, PageTree);
        var first = raw.Position;
        _ = raw.Table(XrefSize, RootTrailer);
        var signedEnd = raw.Position;
        var second = raw.Position;
        var file = raw.Table(XrefSize, string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Prev {first}")).ToArray();
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions() with { CancellationToken = cancellationToken });
        var revisions = StoreRevisions.ReadRevisions(store);

        await Assert.That(revisions.Length).IsEqualTo(IncrementalSectionCount);
        await Assert.That(revisions[0].XrefOffset).IsEqualTo(first);
        await Assert.That(revisions[0].EndOffset).IsEqualTo(signedEnd);
        await Assert.That(revisions[1].XrefOffset).IsEqualTo(second);
        await Assert.That(revisions[1].EndOffset).IsEqualTo(file.LongLength);
    }

    /// <summary>Marker recovery stops at the revision cap even when the repaired file contains additional EOF markers.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadRevisions_MissingXrefWithRepeatedMarkers_BoundsRecovery(CancellationToken cancellationToken)
    {
        var raw = new RawPdf().Object(1, Catalog).Object(PageTreeObject, PageTree);
        for (var index = 0; index <= PdfLimits.MaxXrefSections; index++)
        {
            _ = raw.Append("%%EOF\n");
        }

        using var store = StoreOpening.OpenWith(raw.ToArray(), new PdfOpenOptions() with { CancellationToken = cancellationToken });
        var revisions = StoreRevisions.ReadRevisions(store);

        await Assert.That(revisions.Length).IsEqualTo(PdfLimits.MaxXrefSections);
        await Assert.That(revisions[0].XrefOffset).IsEqualTo(-1);
        await Assert.That(revisions[^1].EndOffset).IsLessThan(store.Source.Length);
    }

    /// <summary>Captures the bounded traversal results before asynchronous assertions.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The count and first two positions.</returns>
    private static SectionOffsets ReadSections(PdfObjectStore store)
    {
        Span<long> sections = stackalloc long[PdfLimits.MaxXrefSections];
        var count = StoreRevisions.ReadSectionOffsets(store, sections);
        return new(count, count > 0 ? sections[0] : -1, count > 1 ? sections[1] : -1);
    }

    /// <summary>The section positions checked by these tests.</summary>
    /// <param name="Count">The section count.</param>
    /// <param name="First">The newest section, or -1.</param>
    /// <param name="Second">The next section, or -1.</param>
    private readonly record struct SectionOffsets(int Count, long First, long Second);
}
