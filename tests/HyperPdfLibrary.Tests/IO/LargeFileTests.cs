// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>
/// Checks that large files are read without being loaded: opening and reading a page allocates far less than the file
/// size, and files past 2 GB open with 64-bit offsets. Runs alone because each test writes and maps a 96 MB file.
/// </summary>
[NotInParallel]
public sealed class LargeFileTests
{
    /// <summary>The pages of the generated file.</summary>
    private const int Pages = 96;

    /// <summary>The length of each page's content stream: 1 MB, so the file is about 96 MB.</summary>
    private const int ContentLength = 1 << 20;

    /// <summary>The page cache budget of the stream source.</summary>
    private const long CacheBytes = 1 << 20;

    /// <summary>The share of the file the managed heap may grow by: one eighth.</summary>
    private const int HeapShare = 8;

    /// <summary>The page read after the first, to read from the middle of the file.</summary>
    private const int MiddlePage = Pages / 2;

    /// <summary>The measurements taken; the smallest is kept, so another thread's garbage cannot fail the test.</summary>
    private const int Attempts = 3;

    /// <summary>The offset of the first object of the sparse file: past 2 GB.</summary>
    private const long SparseGap = (long)int.MaxValue + (1L << 28);

    /// <summary>The pages of the sparse file.</summary>
    private const int SparsePages = 3;

    /// <summary>The content length of the sparse file's pages.</summary>
    private const int SparseContentLength = 4096;

    /// <summary>Opening a 96 MB file and reading two pages grows the managed heap by less than an eighth of the file, unlike reading it into memory.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpeningAndReadingPagesKeepsTheHeapSmall()
    {
        var directory = SourceOpener.CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "large.pdf");
            LargePdf.Write(path, Pages, ContentLength, 0);
            var length = new FileInfo(path).Length;
            var bound = length / HeapShare;
            await Assert.That(MeasureGrowth(path, PdfSourceKind.Stream)).IsLessThan(bound);
            await Assert.That(MeasureGrowth(path, PdfSourceKind.Mapped)).IsLessThan(bound);
            await Assert.That(MeasureGrowth(path, PdfSourceKind.Automatic)).IsLessThan(bound);

            // The measurement sees a whole-file load, so the bounds above mean something.
            await Assert.That(MeasureGrowth(path, PdfSourceKind.Memory)).IsGreaterThanOrEqualTo(length);
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>A file whose objects all lie past 2 GB opens and reads through the mapped and stream sources.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">The file system cannot hold a sparse file.</exception>
    [Test]
    public async Task FilesPastTwoGigabytesOpen()
    {
        if (OperatingSystem.IsWindows())
        {
            // NTFS fills the gap with real zeros unless the file is marked sparse, which needs a native call.
            throw new TUnit.Core.Exceptions.SkipTestException("Sparse files need a native call on Windows.");
        }

        var directory = SourceOpener.CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "sparse.pdf");
            LargePdf.Write(path, SparsePages, SparseContentLength, SparseGap);
            foreach (var kind in (PdfSourceKind[])[PdfSourceKind.Mapped, PdfSourceKind.Stream, PdfSourceKind.Automatic])
            {
                using var document = PdfDocumentReader.OpenWith(path, new PdfOpenOptions { Source = kind });
                await Assert.That(document.PageCount).IsEqualTo(SparsePages);
                await Assert.That(document.Objects.WasRepaired).IsFalse();
                await Assert.That(DecodePage(document, SparsePages - 1)).IsEqualTo(SparseContentLength);
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>Measures the managed bytes allocated by opening a file and reading two pages, keeping the smallest of a few tries.</summary>
    /// <param name="path">The file.</param>
    /// <param name="kind">How to read it.</param>
    /// <returns>The growth in bytes, with the document still open.</returns>
    private static long MeasureGrowth(string path, PdfSourceKind kind)
    {
        var smallest = long.MaxValue;
        for (var i = 0; i < Attempts; i++)
        {
            smallest = Math.Min(smallest, MeasureOnce(path, kind));
        }

        return smallest;
    }

    /// <summary>Measures the allocation once.</summary>
    /// <param name="path">The file.</param>
    /// <param name="kind">How to read it.</param>
    /// <returns>The growth in bytes.</returns>
    private static long MeasureOnce(string path, PdfSourceKind kind)
    {
        // Bytes allocated by this thread, not GC.GetTotalMemory: the total includes other threads' garbage and pool
        // trimming, which swung the result by hundreds of MB (even negative) on a busy machine. Opening and decoding run
        // synchronously on this thread, so its own counter sees all of the work and nothing else.
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var document = PdfDocumentReader.OpenWith(path, new PdfOpenOptions { Source = kind, CacheBytes = CacheBytes });
        _ = DecodePage(document, 0);
        _ = DecodePage(document, Math.Min(MiddlePage, document.PageCount - 1));
        var after = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(document);
        return after - before;
    }

    /// <summary>Decodes a page's content streams.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The decoded length.</returns>
    private static int DecodePage(PdfDocument document, int index)
    {
        var contents = StoreReading.Resolve(document.Objects, PdfDocumentPages.GetPage(document, index).Dictionary.GetRaw(KnownName.Contents)).AsStream();
        if (contents is null)
        {
            return -1;
        }

        var output = default(PooledBuffer);
        try
        {
            _ = contents.Decode(ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
