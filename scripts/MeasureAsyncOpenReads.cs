#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:project ../src/HyperPdfLibrary/HyperPdfLibrary.csproj

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace PdfViewerLite.Scripts;

/// <summary>Counts file reads while a document and its first page content are opened.</summary>
internal static class MeasureAsyncOpenReads
{
    /// <summary>The number of paired measurements.</summary>
    private const int Passes = 5;

    /// <summary>Bytes per mebibyte.</summary>
    private const int BytesPerMiB = 1 << 20;

    /// <summary>Runs paired opens against one PDF.</summary>
    /// <param name="args">The PDF path and page-cache size in MiB.</param>
    /// <returns>A task for the measurement.</returns>
    /// <exception cref="ArgumentException">The arguments are invalid.</exception>
    internal static async Task Main(string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[1], out var cacheMiB) || cacheMiB <= 0)
        {
            throw new ArgumentException("Usage: MeasureAsyncOpenReads <PDF path> <cache MiB>");
        }

        var options = new PdfOpenOptions { CacheBytes = (long)cacheMiB * BytesPerMiB };
        for (var pass = 0; pass < Passes; pass++)
        {
            await ProbeAsync(args[0], options, false);
            await ProbeAsync(args[0], options, true);
        }
    }

    /// <summary>Measures one open and counts its reads.</summary>
    /// <param name="path">The PDF file.</param>
    /// <param name="options">The page-cache settings.</param>
    /// <param name="asynchronous">Whether to open asynchronously.</param>
    /// <returns>A task for the measurement.</returns>
    private static async Task ProbeAsync(string path, PdfOpenOptions options, bool asynchronous)
    {
        await using var input = new CountingStream(File.OpenRead(path));
        var started = Stopwatch.GetTimestamp();
        if (asynchronous)
        {
            using var document = await PdfDocumentReader.OpenWithAsync(input, options, CancellationToken.None);
            DecodeFirstContent(document);
        }
        else
        {
            using var document = PdfDocumentReader.OpenWith(input, options);
            DecodeFirstContent(document);
        }

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Console.WriteLine($"{(asynchronous ? "async" : "sync")} {elapsed:F3}ms sync={input.SyncReads}x/{input.SyncBytes}B async={input.AsyncReads}x/{input.AsyncBytes}B");
    }

    /// <summary>Decodes the first page's content stream.</summary>
    /// <param name="document">The open PDF.</param>
    private static void DecodeFirstContent(PdfDocument document)
    {
        var page = PdfDocumentPages.GetPage(document, 0);
        var stream = StoreReading.Resolve(document.Objects, page.Dictionary.GetRaw(KnownName.Contents)).AsStream();
        var buffer = default(PooledBuffer);
        try
        {
            if (stream is not null)
            {
                _ = stream.Decode(ref buffer);
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>A seekable stream that counts file reads.</summary>
    /// <param name="inner">The stream being measured.</param>
    private sealed class CountingStream(Stream inner) : Stream
    {
        /// <inheritdoc/>
        public override bool CanRead => inner.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => inner.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => inner.Length;

        /// <inheritdoc/>
        public override long Position { get => inner.Position; set => inner.Position = value; }

        /// <summary>Gets the number of synchronous reads.</summary>
        internal int SyncReads { get; private set; }

        /// <summary>Gets the bytes returned by synchronous reads.</summary>
        internal long SyncBytes { get; private set; }

        /// <summary>Gets the number of asynchronous reads.</summary>
        internal int AsyncReads { get; private set; }

        /// <summary>Gets the bytes returned by asynchronous reads.</summary>
        internal long AsyncBytes { get; private set; }

        /// <inheritdoc/>
        public override void Flush() => inner.Flush();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await inner.ReadAsync(buffer, cancellationToken);
            AsyncReads++;
            AsyncBytes += count;
            return count;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>Records a synchronous read.</summary>
        /// <param name="count">The bytes returned.</param>
        /// <returns>The byte count.</returns>
        private int Count(int count)
        {
            SyncReads++;
            SyncBytes += count;
            return count;
        }
    }
}
