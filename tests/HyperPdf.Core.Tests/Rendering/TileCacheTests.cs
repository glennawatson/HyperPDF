// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Tests for <see cref="TileCache"/>.</summary>
public sealed class TileCacheTests
{
    /// <summary>The edge of each test tile.</summary>
    private const int Edge = 16;

    /// <summary>The bytes one test tile uses.</summary>
    private const int TileBytes = Edge * Edge * 4;

    /// <summary>The number of tiles the test budget holds.</summary>
    private const int Capacity = 3;

    /// <summary>Previews may take one over this of the budget.</summary>
    private const int Half = 2;

    /// <summary>The index of the third page.</summary>
    private const int ThirdPage = 2;

    /// <summary>Verifies the least recently used tile is evicted and disposed when over budget.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvictsLeastRecentlyUsed()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        var surfaces = new FakeSurface[Capacity + 1];
        for (var i = 0; i < Capacity; i++)
        {
            surfaces[i] = new(Edge, Edge);
            cache.Add(Key(1, i), surfaces[i]);
        }

        _ = cache.TryGet(Key(1, 0), out _);
        surfaces[Capacity] = new(Edge, Edge);
        cache.Add(Key(1, Capacity), surfaces[Capacity]);

        await Assert.That(cache.Count).IsEqualTo(Capacity);
        await Assert.That(cache.CurrentBytes).IsEqualTo(TileBytes * Capacity);
        await Assert.That(surfaces[1].IsDisposed).IsTrue();
        await Assert.That(cache.Contains(Key(1, 1))).IsFalse();
        await Assert.That(cache.Contains(Key(1, 0))).IsTrue();
        await Assert.That(surfaces[0].IsDisposed).IsFalse();
    }

    /// <summary>Verifies replacing a key disposes the old surface.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacingDisposesOldSurface()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        var first = new FakeSurface(Edge, Edge);
        var second = new FakeSurface(Edge, Edge);

        cache.Add(Key(1, 0), first);
        cache.Add(Key(1, 0), second);

        await Assert.That(first.IsDisposed).IsTrue();
        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(cache.CurrentBytes).IsEqualTo(TileBytes);
    }

    /// <summary>Verifies removing a document only removes its tiles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesDocumentTiles()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        const int otherDocument = 2;
        cache.Add(Key(1, 0), new FakeSurface(Edge, Edge));
        cache.Add(Key(otherDocument, 0), new FakeSurface(Edge, Edge));

        cache.RemoveDocument(1);

        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(cache.Contains(Key(otherDocument, 0))).IsTrue();
    }

    /// <summary>Verifies shrinking the budget trims immediately.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShrinkingBudgetTrims()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        for (var i = 0; i < Capacity; i++)
        {
            cache.Add(Key(1, i), new FakeSurface(Edge, Edge));
        }

        cache.BudgetBytes = TileBytes;

        await Assert.That(cache.Count).IsEqualTo(1);
    }

    /// <summary>Verifies a page preview is kept while the cache sheds tiles, because it stands in for the page while its tiles are made.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewsOutliveTilesWhenOverBudget()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        var preview = new FakeSurface(Edge, Edge);
        cache.Add(PreviewKey(1, 0), preview);
        for (var i = 0; i < Capacity; i++)
        {
            cache.Add(Key(1, i), new FakeSurface(Edge, Edge));
        }

        await Assert.That(cache.Contains(PreviewKey(1, 0))).IsTrue();
        await Assert.That(preview.IsDisposed).IsFalse();
        await Assert.That(cache.Contains(Key(1, 0))).IsFalse();
        await Assert.That(cache.Count).IsEqualTo(Capacity);
        await Assert.That(cache.CurrentBytes).IsEqualTo(TileBytes * Capacity);
    }

    /// <summary>Verifies previews never take more than half the budget, so they cannot push every tile out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewsAreCappedAtHalfTheBudget()
    {
        const int pages = 4;
        using var cache = new TileCache(TileBytes * pages);
        var surfaces = new FakeSurface[pages];
        for (var page = 0; page < pages; page++)
        {
            surfaces[page] = new(Edge, Edge);
            cache.Add(PreviewKey(1, page), surfaces[page]);
        }

        await Assert.That(cache.PreviewBytes).IsEqualTo(TileBytes * pages / Half);
        await Assert.That(surfaces[0].IsDisposed).IsTrue();
        await Assert.That(surfaces[1].IsDisposed).IsTrue();
        await Assert.That(cache.Contains(PreviewKey(1, pages - 1))).IsTrue();
    }

    /// <summary>Verifies a preview that is looked up counts as recently used, so the previews in view are the last to go.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LookedUpPreviewsAreKept()
    {
        const int pages = 4;
        using var cache = new TileCache(TileBytes * pages);
        cache.Add(PreviewKey(1, 0), new FakeSurface(Edge, Edge));
        cache.Add(PreviewKey(1, 1), new FakeSurface(Edge, Edge));
        _ = cache.TryGet(PreviewKey(1, 0), out _);

        cache.Add(PreviewKey(1, ThirdPage), new FakeSurface(Edge, Edge));

        await Assert.That(cache.Contains(PreviewKey(1, 0))).IsTrue();
        await Assert.That(cache.Contains(PreviewKey(1, 1))).IsFalse();
    }

    /// <summary>Verifies removing a document or a page removes its previews as well as its tiles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesPreviewsWithTheirDocumentAndPage()
    {
        const int otherDocument = 2;
        using var cache = new TileCache(TileBytes * Capacity * Capacity);
        cache.Add(PreviewKey(1, 0), new FakeSurface(Edge, Edge));
        cache.Add(PreviewKey(1, 1), new FakeSurface(Edge, Edge));
        cache.Add(PreviewKey(otherDocument, 0), new FakeSurface(Edge, Edge));
        cache.Add(Key(1, 0), new FakeSurface(Edge, Edge));

        cache.RemovePage(1, 0);

        await Assert.That(cache.Contains(PreviewKey(1, 0))).IsFalse();
        await Assert.That(cache.Contains(Key(1, 0))).IsFalse();
        await Assert.That(cache.Contains(PreviewKey(1, 1))).IsTrue();

        cache.RemoveDocument(1);

        await Assert.That(cache.Contains(PreviewKey(1, 1))).IsFalse();
        await Assert.That(cache.Contains(PreviewKey(otherDocument, 0))).IsTrue();
        await Assert.That(cache.PreviewBytes).IsEqualTo(TileBytes);
    }

    /// <summary>Verifies a tile that keeps being looked up survives repeated eviction while untouched tiles go.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HotTileSurvivesRepeatedEviction()
    {
        const int added = 10;
        using var cache = new TileCache(TileBytes * Capacity);
        cache.Add(Key(1, 0), new FakeSurface(Edge, Edge));
        for (var i = 1; i < added; i++)
        {
            _ = cache.TryGet(Key(1, 0), out _);
            cache.Add(Key(1, i), new FakeSurface(Edge, Edge));
        }

        await Assert.That(cache.Count).IsEqualTo(Capacity);
        await Assert.That(cache.Contains(Key(1, 0))).IsTrue();
        await Assert.That(cache.Contains(Key(1, added - 1))).IsTrue();
        await Assert.That(cache.CurrentBytes).IsEqualTo(TileBytes * Capacity);
    }

    /// <summary>Verifies the cache still evicts when every tile has been looked up.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvictsWhenEveryTileWasLookedUp()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        for (var i = 0; i < Capacity; i++)
        {
            cache.Add(Key(1, i), new FakeSurface(Edge, Edge));
            _ = cache.TryGet(Key(1, i), out _);
        }

        cache.Add(Key(1, Capacity), new FakeSurface(Edge, Edge));

        await Assert.That(cache.Count).IsEqualTo(Capacity);
        await Assert.That(cache.CurrentBytes).IsEqualTo(TileBytes * Capacity);
        await Assert.That(cache.Contains(Key(1, Capacity))).IsTrue();
    }

    /// <summary>Verifies a lookup reports misses and hits with the stored surface.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryGetReturnsStoredSurfaceOnlyForCachedKeys()
    {
        using var cache = new TileCache(TileBytes * Capacity);
        var surface = new FakeSurface(Edge, Edge);
        cache.Add(Key(1, 0), surface);

        await Assert.That(cache.TryGet(Key(1, 0), out var found)).IsTrue();
        await Assert.That(found).IsSameReferenceAs(surface);
        await Assert.That(cache.TryGet(Key(1, 1), out _)).IsFalse();
    }

    /// <summary>Verifies removing a page under two document identifiers removes only those tiles and previews of that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesPageUnderBothDocumentIdentifiers()
    {
        const int otherDocument = 2;
        using var cache = new TileCache(TileBytes * Capacity * Capacity * Capacity);
        cache.Add(Key(1, 0), new FakeSurface(Edge, Edge));
        cache.Add(Key(~1, 0), new FakeSurface(Edge, Edge));
        cache.Add(PreviewKey(1, 0), new FakeSurface(Edge, Edge));
        cache.Add(PreviewKey(~1, 0), new FakeSurface(Edge, Edge));
        cache.Add(PreviewKey(1, 1), new FakeSurface(Edge, Edge));
        cache.Add(Key(otherDocument, 0), new FakeSurface(Edge, Edge));

        cache.RemovePage(1, ~1, 0);

        await Assert.That(cache.Contains(Key(1, 0))).IsFalse();
        await Assert.That(cache.Contains(Key(~1, 0))).IsFalse();
        await Assert.That(cache.Contains(PreviewKey(1, 0))).IsFalse();
        await Assert.That(cache.Contains(PreviewKey(~1, 0))).IsFalse();
        await Assert.That(cache.Contains(PreviewKey(1, 1))).IsTrue();
        await Assert.That(cache.Contains(Key(otherDocument, 0))).IsTrue();
        await Assert.That(cache.Count).IsEqualTo(Capacity - 1);
        await Assert.That(cache.CurrentBytes).IsEqualTo(TileBytes * (Capacity - 1));
    }

    /// <summary>Creates a key.</summary>
    /// <param name="document">The document identifier.</param>
    /// <param name="column">The column.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int document, int column) => new(document, 0, 1, PageRotation.None, 0, (short)column, 0);

    /// <summary>Creates the key of a page preview.</summary>
    /// <param name="document">The document identifier.</param>
    /// <param name="page">The page.</param>
    /// <returns>The key.</returns>
    private static TileKey PreviewKey(int document, int page) => TileKey.Preview(document, page, PageRotation.None, 0);
}
