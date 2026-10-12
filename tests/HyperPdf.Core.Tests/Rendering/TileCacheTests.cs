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

    /// <summary>Creates a key.</summary>
    /// <param name="document">The document identifier.</param>
    /// <param name="column">The column.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int document, int column) => new(document, 0, 1, PageRotation.None, 0, (short)column, 0);
}
