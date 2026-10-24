// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests bounded traversal of raw cross-reference trailer links.</summary>
public sealed class XrefReaderTests
{
    /// <summary>The cross-reference size of the two-object file.</summary>
    private const int TableSize = 3;

    /// <summary>The number of linked trailers in the hostile file.</summary>
    private const int HostileDepth = 10_000;

    /// <summary>Self-linked trailers are visited once without rebuilding the file.</summary>
    /// <param name="link">The trailer link that points to itself.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Prev")]
    [Arguments("XRefStm")]
    public async Task TryRead_SelfLinkedTrailer_Completes(string link)
    {
        var raw = Objects();
        var entries = string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /{link} {raw.Position}");
        using var store = StoreOpening.Open(
            raw.Table(TableSize, entries).ToArray(),
            null);

        await Assert.That(store.WasRepaired).IsFalse();
        await Assert.That(store.Catalog.IsName(KnownName.Type, KnownName.Catalog)).IsTrue();
    }

    /// <summary>A ten thousand-section chain stops before reaching its oldest trailer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryRead_DeepPrevChain_StopsAtSectionLimit()
    {
        var raw = Objects();
        var previous = raw.Position;
        _ = raw.Table(TableSize, "/Root 1 0 R /Info 2 0 R");
        for (var index = 1; index < HostileDepth; index++)
        {
            var position = raw.Position;
            _ = raw.Table(TableSize, string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Prev {previous}"));
            previous = position;
        }

        using var store = StoreOpening.Open(raw.ToArray(), null);

        await Assert.That(store.WasRepaired).IsFalse();
        await Assert.That(store.Trailer.ContainsKey(KnownName.Info)).IsFalse();
        await Assert.That(store.Catalog.IsName(KnownName.Type, KnownName.Catalog)).IsTrue();
    }

    /// <summary>Creates the catalog and empty page tree.</summary>
    /// <returns>The raw file builder.</returns>
    private static RawPdf Objects() => new RawPdf()
        .Object(1, "<< /Type /Catalog /Pages 2 0 R >>")
        .Object(TableSize - 1, "<< /Type /Pages /Kids [] /Count 0 >>");
}
