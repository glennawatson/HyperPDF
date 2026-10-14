// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>Checks that disposing a document that reads a file releases it safely, even while other threads read.</summary>
public sealed class SourceDisposeTests
{
    /// <summary>The threads reading while the document is disposed.</summary>
    private const int Readers = 6;

    /// <summary>The disposal rounds per seed and source.</summary>
    private const int Rounds = 4;

    /// <summary>The length of the short stream source.</summary>
    private const int ShortLength = 3;

    /// <summary>The object number of the mini document's content stream.</summary>
    private const int ContentStream = 5;

    /// <summary>The kinds that hold a file or mapping.</summary>
    private static readonly string[] FileKinds = ["mapped", "handle", "stream"];

    /// <summary>Disposing while reads are in flight never crashes; each read finishes whole or throws ObjectDisposedException.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeWhileReadingIsSafe()
    {
        var problems = new ConcurrentQueue<string>();
        var directory = SourceOpener.CreateDirectory();
        try
        {
            foreach (var seed in RobustnessSeeds.Create())
            {
                foreach (var kind in FileKinds)
                {
                    for (var round = 0; round < Rounds; round++)
                    {
                        await DisposeOnceAsync(seed, kind, round, directory, problems);
                    }
                }
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }

        await Assert.That(problems).IsEmpty();
    }

    /// <summary>After disposal, objects and streams held from before throw ObjectDisposedException, and the file is released.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAfterDisposeThrow()
    {
        var directory = SourceOpener.CreateDirectory();
        try
        {
            foreach (var kind in FileKinds)
            {
                var document = SourceOpener.Open(kind, RobustnessSeeds.CreateMini(), PdfOpenOptions.Default, directory);
                var stream = StoreReading.GetObject(document.Objects, new(ContentStream, 0)).AsStream()!;
                document.Dispose();
                await Assert.That(() => StoreReading.GetObject(document.Objects, new(1, 0))).Throws<ObjectDisposedException>();
                await Assert.That(() => stream.DecodeToArray()).Throws<ObjectDisposedException>();
                await Assert.That(document.Objects.Source.IsDisposed).IsTrue();
            }
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>A mapped window taken before disposal stays readable until it is released.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MappedWindowOutlivesDisposal()
    {
        var directory = SourceOpener.CreateDirectory();
        try
        {
            var bytes = RobustnessSeeds.CreateMini();
            var path = SourceOpener.WriteFile(bytes, directory);
            var source = MappedPdfByteSource.Open(path);
            var first = ReadFirstAfterDispose(source);
            await Assert.That(first).IsEqualTo(bytes[0]);
            await Assert.That(() => source.Lease(0, 1).Dispose()).Throws<ObjectDisposedException>();
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>Disposing an empty stream source or reading past the end is harmless.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamSourceReadsPastTheEndAsShort()
    {
        using var source = new StreamPdfByteSource(new MemoryStream(new byte[ShortLength], false));
        var buffer = new byte[ShortLength * ShortLength];
        await Assert.That(source.Read(1, buffer)).IsEqualTo(ShortLength - 1);
        await Assert.That(source.Read(ShortLength, buffer)).IsEqualTo(0);
        await Assert.That(() => source.Lease(ShortLength - 1, ShortLength).Dispose()).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Takes a window, disposes the source and reads the window's first byte.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The first byte.</returns>
    private static byte ReadFirstAfterDispose(PdfByteSource source)
    {
        var window = source.Lease(0, 1);
        try
        {
            source.Dispose();
            return window.Span[0];
        }
        finally
        {
            window.Dispose();
        }
    }

    /// <summary>Reads one document from several threads and disposes it while they work.</summary>
    /// <param name="seed">The seed document.</param>
    /// <param name="kind">The source kind.</param>
    /// <param name="round">The round, which sets how long the disposer waits.</param>
    /// <param name="directory">A temporary directory.</param>
    /// <param name="problems">Receives anything unexpected.</param>
    /// <returns>A task.</returns>
    private static async Task DisposeOnceAsync(RobustnessSeeds.Seed seed, string kind, int round, string directory, ConcurrentQueue<string> problems)
    {
        var document = SourceOpener.Open(kind, seed.Bytes, PdfOpenOptions.Default, directory);
        var started = new CountdownEvent(Readers);
        var tasks = new Task[Readers];
        var job = new ReadJob(document, started, problems, seed.Name, kind);

        // The readers spin until the dispose, so they run on their own threads and never starve this method's continuations.
        for (var i = 0; i < Readers; i++)
        {
            tasks[i] = DedicatedThread.Run(job, ReadUntilDisposed, CancellationToken.None);
        }

        while (started.CurrentCount > 0)
        {
            await Task.Yield();
        }

        for (var i = 0; i < round; i++)
        {
            await Task.Yield();
        }

        document.Dispose();
        await Task.WhenAll(tasks);
        started.Dispose();
    }

    /// <summary>Reads until the document is disposed; only ObjectDisposedException may stop the loop.</summary>
    /// <param name="job">The document and what the reader signals and reports to.</param>
    private static void ReadUntilDisposed(ReadJob job)
    {
        var (document, started, problems, name, kind) = job;
        _ = started.Signal();
        try
        {
            while (true)
            {
                _ = DocumentExerciser.Read(document);
            }
        }
        catch (ObjectDisposedException)
        {
            // A read after (or racing with) the dispose; this is the expected way out.
        }
        catch (Exception ex)
        {
            problems.Enqueue(string.Create(CultureInfo.InvariantCulture, $"{name} via {kind}: {ex}"));
        }
    }

    /// <summary>What one reading thread reads and reports to.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Started">Signalled when the reader starts.</param>
    /// <param name="Problems">Receives anything unexpected.</param>
    /// <param name="Name">The seed name.</param>
    /// <param name="Kind">The source kind.</param>
    private sealed record ReadJob(PdfDocument Document, CountdownEvent Started, ConcurrentQueue<string> Problems, string Name, string Kind);
}
