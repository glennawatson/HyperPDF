// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests that the object store keeps a bounded set of decoded object streams and decodes the rest again on demand.</summary>
public sealed class ObjectStreamCacheTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int PageCount = 1_500;

    /// <summary>The objects packed in each object stream, which gives the document many object streams.</summary>
    private const int ObjectsPerStream = 10;

    /// <summary>A byte budget no stream fits in, so only the newest stream is kept.</summary>
    private const long TinyBytes = 1;

    /// <summary>A byte budget that holds a few streams.</summary>
    private const long SmallBytes = 4096;

    /// <summary>A small stream budget, also used to alternate the readers' page orders.</summary>
    private const int FewStreams = 2;

    /// <summary>The readers in the concurrent test.</summary>
    private const int Readers = 8;

    /// <summary>The step that walks the pages in a scattered order; it shares no factor with the page count.</summary>
    private const int ScatterStep = 37;

    /// <summary>Every object reads back correctly, in forward, reverse and scattered order, whatever the budget.</summary>
    /// <param name="maxStreams">The most streams kept.</param>
    /// <param name="maxBytes">The most bytes kept.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, TinyBytes)]
    [Arguments(1, long.MaxValue)]
    [Arguments(2, SmallBytes)]
    [Arguments(ObjectStreamCache.DefaultMaxStreams, ObjectStreamCache.DefaultMaxBytes)]
    public async Task EveryObjectReadsBackWithAnyBudget(int maxStreams, long maxBytes)
    {
        foreach (var order in new[]
        {
            Forward(),
            Reverse(),
            Scattered(),
        }

        )
        {
            using var store = StoreOpening.Open(ObjectStreamPdf.Create(PageCount, ObjectsPerStream), null);
            StoreObjectStreams.SetObjectStreamLimits(store, maxStreams, maxBytes);
            foreach (var page in order)
            {
                await Assert.That(PageIsIntact(store, page)).IsTrue();
            }

            await Assert.That(store.CachedObjectStreams).IsLessThanOrEqualTo(maxStreams);
        }
    }

    /// <summary>A document with many object streams holds no more than the budget once every object has been read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MemoryStaysWithinTheBudget()
    {
        var file = ObjectStreamPdf.Create(PageCount, ObjectsPerStream);
        var containers = ObjectStreamPdf.ContainerCount(PageCount, ObjectsPerStream);
        using var store = StoreOpening.Open(file, null);
        StoreObjectStreams.SetObjectStreamLimits(store, FewStreams, SmallBytes);
        for (var page = 0; page < PageCount; page++)
        {
            _ = PageIsIntact(store, page);
        }

        await Assert.That(containers).IsGreaterThan(ObjectStreamCache.DefaultMaxStreams);
        await Assert.That(store.CachedObjectStreams).IsLessThanOrEqualTo(FewStreams);
        await Assert.That(store.CachedObjectStreamBytes).IsLessThanOrEqualTo(SmallBytes);
    }

    /// <summary>With the default budget the count never passes the default limit however many object streams the file has.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultBudgetBoundsTheCount()
    {
        using var store = StoreOpening.Open(ObjectStreamPdf.Create(PageCount, ObjectsPerStream), null);
        for (var page = 0; page < PageCount; page++)
        {
            _ = PageIsIntact(store, page);
        }

        await Assert.That(store.CachedObjectStreams).IsLessThanOrEqualTo(ObjectStreamCache.DefaultMaxStreams);
        await Assert.That(store.CachedObjectStreamBytes).IsLessThanOrEqualTo(ObjectStreamCache.DefaultMaxBytes);
    }

    /// <summary>A stream bigger than the byte budget is still kept while it is the newest, so reading it works.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewestStreamStaysEvenOverTheByteBudget()
    {
        using var store = StoreOpening.Open(ObjectStreamPdf.Create(PageCount, ObjectsPerStream), null);
        StoreObjectStreams.SetObjectStreamLimits(store, ObjectStreamCache.DefaultMaxStreams, TinyBytes);
        for (var page = 0; page < PageCount; page++)
        {
            await Assert.That(PageIsIntact(store, page)).IsTrue();
            await Assert.That(store.CachedObjectStreams).IsEqualTo(1);
        }
    }

    /// <summary>Several threads read the same objects through a one-stream budget, so streams are decoded again while others read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentReadersSeeEveryObject()
    {
        using var store = StoreOpening.Open(ObjectStreamPdf.Create(PageCount, ObjectsPerStream), null);
        StoreObjectStreams.SetObjectStreamLimits(store, 1, TinyBytes);
        var tasks = new Task<bool>[Readers];
        for (var reader = 0; reader < Readers; reader++)
        {
            var order = reader % FewStreams == 0 ? Reverse() : Scattered();
            tasks[reader] = Task.Run(() => ReadAll(store, order));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            await Assert.That(result).IsTrue();
        }
    }

    /// <summary>Reads every page in an order.</summary>
    /// <param name="store">The store.</param>
    /// <param name="order">The page indexes.</param>
    /// <returns><see langword="true"/> when every page is intact.</returns>
    private static bool ReadAll(PdfObjectStore store, int[] order)
    {
        foreach (var page in order)
        {
            if (!PageIsIntact(store, page))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks a page's dictionary, its parent and its content stream, which come from different places in the file.</summary>
    /// <param name="store">The store.</param>
    /// <param name="page">The zero-based page index.</param>
    /// <returns><see langword="true"/> when all three read back.</returns>
    private static bool PageIsIntact(PdfObjectStore store, int page)
    {
        var dictionary = StoreReading.GetObject(store, new(ObjectStreamPdf.PageObject(page), 0)).AsDictionary();
        return dictionary is not null
        && dictionary.GetDictionary(KnownName.Parent) is { } parent
        && parent.GetInt32(KnownName.Count) == PageCount
        && dictionary.Get(KnownName.Contents).AsStream() is not null;
    }

    /// <summary>Gets the page indexes in order.</summary>
    /// <returns>The indexes.</returns>
    private static int[] Forward()
    {
        var order = new int[PageCount];
        for (var i = 0; i < PageCount; i++)
        {
            order[i] = i;
        }

        return order;
    }

    /// <summary>Gets the page indexes in reverse.</summary>
    /// <returns>The indexes.</returns>
    private static int[] Reverse()
    {
        var order = Forward();
        Array.Reverse(order);
        return order;
    }

    /// <summary>Gets every page index once, jumping around.</summary>
    /// <returns>The indexes.</returns>
    private static int[] Scattered()
    {
        var order = new int[PageCount];
        for (var i = 0; i < PageCount; i++)
        {
            order[i] = (i * ScatterStep) % PageCount;
        }

        return order;
    }
}
