// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>Checks that stream cache tables follow the registered host's lifetime.</summary>
public sealed class StreamPageCacheTests
{
    /// <summary>The number of pages in a multi-page cache check.</summary>
    private const int TwoPages = 2;

    /// <summary>Read-ahead is unnecessary once ordinary reads have cached every byte of the source.</summary>
    /// <param name="pages">The source page count.</param>
    /// <param name="reads">The number of distinct pages read.</param>
    /// <param name="capacity">The cache capacity in pages.</param>
    /// <param name="needsPrefetch">Whether uncached pages still require read-ahead.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 1, 1, false)]
    [Arguments(TwoPages, 1, TwoPages, true)]
    [Arguments(TwoPages, TwoPages, TwoPages, false)]
    [Arguments(TwoPages, TwoPages, 1, true)]
    public async Task OrdinaryReadsRecogniseCompleteCache(int pages, int reads, int capacity, bool needsPrefetch)
    {
        await using var stream = new MemoryStream(new byte[pages * StreamPdfByteSource.PageSize], false);
        using var source = new StreamPdfByteSource(stream, capacity * StreamPdfByteSource.PageSize, false);
        var buffer = new byte[1];
        for (var page = 0; page < reads; page++)
        {
            await Assert.That(source.Read(page * StreamPdfByteSource.PageSize, buffer)).IsEqualTo(1);
        }

        await Assert.That(source.NeedsPrefetch).IsEqualTo(needsPrefetch);
    }

    /// <summary>Opening a stream uses the host's tables and returns cleared tables when the document closes.</summary>
    /// <param name="asynchronous">Whether to use the asynchronous open path.</param>
    /// <param name="file">Whether to open a file path instead of a stream.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task DocumentReturnsTablesToItsHost(bool asynchronous, bool file, CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        var pool = pools.Slots<StreamPageCache>();
        var cache = StreamPageCache.Rent(1, pool);
        StreamPageCache.Return(cache, pool);
        var options = new PdfOpenOptions
        {
            CancellationToken = cancellationToken,
            CacheBytes = StreamPdfByteSource.PageSize,
            Source = PdfSourceKind.Stream,
            DrawingServices = new(new()) { SharedScratchPools = pools },
        };
        var directory = SourceOpener.CreateDirectory();
        try
        {
            var bytes = TestPdf.Create(1);
            await using var stream = new MemoryStream(bytes, false);
            var path = Path.Combine(directory, "pages.pdf");
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            using (var document = (file, asynchronous) switch
            {
                (true, true) => await PdfDocumentReader.OpenWithAsync(path, options, cancellationToken),
                (true, false) => PdfDocumentReader.OpenWith(path, options),
                (false, true) => await PdfDocumentReader.OpenWithAsync(stream, options, cancellationToken),
                _ => PdfDocumentReader.OpenWith(stream, options),
            })
            {
                await Assert.That(pool.Take()).IsNull();
                await Assert.That(document.PageCount).IsEqualTo(1);
            }

            await Assert.That(pool.Take()).IsSameReferenceAs(cache);
            await Assert.That(cache.Slots).IsEmpty();
            await Assert.That(cache.Pages[0]).IsNull();
            await Assert.That(cache.PageNumbers[0]).IsEqualTo(-1);
        }
        finally
        {
            pools.Close();
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>Closing a host while a source is active prevents its tables from being retained afterward.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HostCloseRejectsActiveSourceTables()
    {
        var pools = new ScratchPools();
        var pool = pools.Slots<StreamPageCache>();
        await using var stream = new MemoryStream([1], false);
        var source = new StreamPdfByteSource(stream, StreamPdfByteSource.PageSize, false, pools);
        pools.Close();
        source.Dispose();
        await Assert.That(pool.Take()).IsNull();
    }

    /// <summary>A host cannot rent tables retained by another host.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HostsKeepSeparateTables()
    {
        var first = new ScratchPools();
        var second = new ScratchPools();
        var cache = StreamPageCache.Rent(1, first.Slots<StreamPageCache>());
        StreamPageCache.Return(cache, first.Slots<StreamPageCache>());
        var other = StreamPageCache.Rent(1, second.Slots<StreamPageCache>());
        await Assert.That(other).IsNotSameReferenceAs(cache);
        await Assert.That(first.Slots<StreamPageCache>().Take()).IsSameReferenceAs(cache);
        first.Close();
        second.Close();
    }
}
