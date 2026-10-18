// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.IO;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>One document read, edited and disposed from many tasks at once.</summary>
public sealed class ConcurrencyTests
{
    /// <summary>The number of tasks reading one document.</summary>
    private const int Readers = 16;

    /// <summary>The rounds of each test; every round uses a fresh document so caches start empty.</summary>
    private const int Rounds = 6;

    /// <summary>The rounds of the dispose test, which varies when the dispose lands.</summary>
    private const int DisposeRounds = 24;

    /// <summary>The rounds of the dispose test over file sources.</summary>
    private const int FileSourceRounds = 6;

    /// <summary>The number of writer tasks.</summary>
    private const int Writers = 4;

    /// <summary>The objects each writer adds.</summary>
    private const int AddsPerWriter = 400;

    /// <summary>The times each writer replaces the shared object.</summary>
    private const int Replacements = 800;

    /// <summary>The hang bound for parallel work, renewed after each successful disposal round.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(5);

    /// <summary>Sixteen tasks reading one document all see exactly what a single thread sees.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParallelReadsMatchSingleThreadedRead()
    {
        using var timeout = new CancellationTokenSource(Limit);
        var failures = new List<string>();
        foreach (var seed in RobustnessSeeds.Create())
        {
            var expected = ReadOnce(seed.Bytes);
            for (var round = 0; round < Rounds; round++)
            {
                failures.AddRange(await ReadInParallelAsync(seed, expected, timeout.Token));
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Objects added and replaced from several tasks while others read are all kept and never torn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentEditsWhileReading()
    {
        using var timeout = new CancellationTokenSource(Limit);
        using var document = PdfDocumentReader.Open(RobustnessSeeds.Create()[0].Bytes, null);
        var store = document.Objects;
        var shared = StoreEditing.Add(store, PdfValue.FromInteger(0));
        var problems = new ConcurrentQueue<string>();
        var writing = true;
        var readers = new Task[Readers];
        for (var i = 0; i < readers.Length; i++)
        {
            readers[i] = RunOnOwnThread(() => ReadWhileWriting(store, shared, problems, () => Volatile.Read(ref writing), timeout.Token));
        }

        var added = new ConcurrentBag<PdfObjectId>[Writers];
        var writers = new Task[Writers];
        for (var i = 0; i < writers.Length; i++)
        {
            var bag = added[i] = [];
            var writer = i;
            writers[i] = RunOnOwnThread(() => Write(store, shared, writer, bag));
        }

        await Task.WhenAll(writers);
        Volatile.Write(ref writing, false);
        await Task.WhenAll(readers);
        await Assert.That(problems).IsEmpty();
        await Assert.That(CountDistinct(added)).IsEqualTo(Writers * AddsPerWriter);
        await Assert.That(CheckAdded(store, added)).IsEmpty();
        await Assert.That(StoreReading.GetObject(store, shared).AsInteger(-1)).IsGreaterThanOrEqualTo(0);
    }

    /// <summary>Disposing while reads are in flight never crashes; each read finishes whole or throws ObjectDisposedException.</summary>
    /// <param name="token">Cancelled when the test runner stops the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeWhileReadingIsSafe(CancellationToken token)
    {
        var failures = new List<string>();
        var seeds = RobustnessSeeds.Create();
        foreach (var seed in seeds)
        {
            var expected = ReadOnce(seed.Bytes);
            for (var round = 0; round < DisposeRounds; round++)
            {
                failures.AddRange(await DisposeOnceAsync(seed, expected, round, token));
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Disposal waits for the reader-start signal without disposing the document early.</summary>
    /// <param name="token">Cancels the wait.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposalWait_ReaderStartSignal_DisposesOnlyAfterSignal(CancellationToken token)
    {
        var document = PdfDocumentReader.Open(RobustnessSeeds.CreateMini(), null);
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposal = DisposeAfterStartAsync(document, allStarted.Task, 0, token);
        try
        {
            await Assert.That(disposal.IsCompleted).IsFalse();
            await Assert.That(document.IsDisposed).IsFalse();

            allStarted.SetResult();
            await disposal;

            await Assert.That(document.IsDisposed).IsTrue();
        }
        finally
        {
            _ = allStarted.TrySetResult();
            try
            {
                await disposal;
            }
            finally
            {
                document.Dispose();
            }
        }
    }

    /// <summary>Cancelling before readers start ends the wait without disposing their document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposalWait_CancelledBeforeReaderStart_DoesNotDispose()
    {
        using var document = PdfDocumentReader.Open(RobustnessSeeds.CreateMini(), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await Assert.That(() => DisposeAfterStartAsync(document, allStarted.Task, 0, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(document.IsDisposed).IsFalse();
    }

    /// <summary>A reader releases its worker between reads and stops on cancellation without disposing the document.</summary>
    /// <param name="token">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposalReader_YieldsAndStopsOnCancellation(CancellationToken token)
    {
        var bytes = RobustnessSeeds.CreateMini();
        var expected = ReadOnce(bytes);
        using var document = PdfDocumentReader.Open(bytes, null);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var started = new CountdownEvent(1);
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var problems = new ConcurrentQueue<string>();
        var job = new DisposalReader(document, expected, problems, started, allStarted, cancellation.Token);
        var reader = ReadUntilDisposedAsync(job);
        try
        {
            await Assert.That(reader.IsCompleted).IsFalse();
            await allStarted.Task.WaitAsync(token);
        }
        finally
        {
            await cancellation.CancelAsync();
            await reader;
        }

        await Assert.That(problems).IsEmpty();
        await Assert.That(document.IsDisposed).IsFalse();
    }

    /// <summary>Disposing while reads are in flight is also safe for documents read through a file stream or a file mapping, whose byte sources hold leases and locks.</summary>
    /// <param name="kind">The source kind, from <see cref="SourceOpener.Kinds"/>.</param>
    /// <param name="token">Cancelled when the test runner stops the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("stream")]
    [Arguments("mapped")]
    public async Task DisposeWhileReadingFileSourcesIsSafe(string kind, CancellationToken token)
    {
        var directory = SourceOpener.CreateDirectory();
        try
        {
            var failures = new List<string>();
            foreach (var seed in RobustnessSeeds.Create())
            {
                var expected = ReadOnce(seed.Bytes);
                for (var round = 0; round < FileSourceRounds; round++)
                {
                    var document = SourceOpener.Open(kind, seed.Bytes, new(), directory);
                    failures.AddRange(await DisposeOnceAsync(document, seed, expected, round, token));
                }
            }

            await Assert.That(failures).IsEmpty();
        }
        finally
        {
            SourceOpener.DeleteDirectory(directory);
        }
    }

    /// <summary>After disposal, reads throw ObjectDisposedException every time, and a second dispose does nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAfterDisposeThrowConsistently()
    {
        foreach (var seed in RobustnessSeeds.Create())
        {
            var document = PdfDocumentReader.Open(seed.Bytes, null);
            var id = new PdfObjectId(1, 0);
            document.Dispose();
            document.Dispose();
            await Assert.That(document.IsDisposed).IsTrue();
            await Assert.That(() => StoreReading.GetObject(document.Objects, id)).Throws<ObjectDisposedException>();
            await Assert.That(() => StoreReading.GetObject(document.Objects, id)).Throws<ObjectDisposedException>();
            await Assert.That(() => StoreReading.Resolve(document.Objects, PdfValue.FromReference(id))).Throws<ObjectDisposedException>();
        }
    }

    /// <summary>
    /// Runs a busy-looping body on a dedicated thread. These bodies spin until another task acts, so on the shared pool
    /// 16+ of them can hold every worker while the task they wait for queues behind them; the pool then adds a thread only
    /// every half second or so, which on a busy machine outlasts the test's time limit.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>A task that completes with the body.</returns>
    private static Task RunOnOwnThread(Action body) => DedicatedThread.Run(body, static body => body(), CancellationToken.None);

    /// <summary>Opens a document and reads it on the calling thread.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The description of what was read.</returns>
    private static string ReadOnce(byte[] file)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return DocumentExerciser.Read(document);
    }

    /// <summary>Reads one fresh document from many tasks released together.</summary>
    /// <param name="seed">The seed document.</param>
    /// <param name="expected">What a single-threaded read gives.</param>
    /// <param name="token">Cancelled when the test has run too long.</param>
    /// <returns>A line for each task whose read differed or threw.</returns>
    private static async Task<List<string>> ReadInParallelAsync(RobustnessSeeds.Seed seed, string expected, CancellationToken token)
    {
        var document = PdfDocumentReader.Open(seed.Bytes, null);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task<string>[Readers];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = ReadAfterGateAsync(gate.Task, () => DocumentExerciser.Read(document));
        }

        gate.SetResult();
        var failures = new List<string>();
        var results = await Task.WhenAll(tasks).WaitAsync(token);
        document.Dispose();
        foreach (var result in results)
        {
            if (!string.Equals(result, expected, StringComparison.Ordinal))
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{seed.Name}: a parallel read differed from the single-threaded read"));
            }
        }

        return failures;
    }

    /// <summary>Waits for the gate on a pool thread, then reads the document.</summary>
    /// <param name="gate">Completes when every reader may start.</param>
    /// <param name="read">Reads the document.</param>
    /// <returns>The description of what was read.</returns>
    private static async Task<string> ReadAfterGateAsync(Task gate, Func<string> read)
    {
        await Task.Yield();
        await gate;
        return read();
    }

    /// <summary>Reads every object over and over until the writers finish.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="shared">The object the writers keep replacing.</param>
    /// <param name="problems">Receives anything unexpected.</param>
    /// <param name="writing">Says whether the writers are still running.</param>
    /// <param name="token">Cancelled when the test has run too long.</param>
    private static void ReadWhileWriting(PdfObjectStore store, PdfObjectId shared, ConcurrentQueue<string> problems, Func<bool> writing, CancellationToken token)
    {
        try
        {
            while (writing() && !token.IsCancellationRequested)
            {
                for (var number = 1; number < store.Size; number++)
                {
                    var value = StoreReading.GetObject(store, new(number, 0));
                    if (number == shared.Number && value.AsInteger(-1) < 0)
                    {
                        problems.Enqueue("the shared object was read as something other than a counter");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            problems.Enqueue(ex.ToString());
        }
    }

    /// <summary>Adds objects and replaces the shared object.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="shared">The shared object.</param>
    /// <param name="writer">This writer's number.</param>
    /// <param name="added">Receives the ids added.</param>
    private static void Write(PdfObjectStore store, PdfObjectId shared, int writer, ConcurrentBag<PdfObjectId> added)
    {
        for (var i = 0; i < AddsPerWriter; i++)
        {
            added.Add(StoreEditing.Add(store, PdfValue.FromInteger((writer * AddsPerWriter) + i)));
        }

        for (var i = 0; i < Replacements; i++)
        {
            StoreEditing.Replace(store, shared, PdfValue.FromInteger(i));
        }
    }

    /// <summary>Counts the different ids that were handed out.</summary>
    /// <param name="added">The ids each writer added.</param>
    /// <returns>The number of distinct ids.</returns>
    private static int CountDistinct(ConcurrentBag<PdfObjectId>[] added)
    {
        var numbers = new HashSet<int>();
        foreach (var bag in added)
        {
            foreach (var id in bag)
            {
                _ = numbers.Add(id.Number);
            }
        }

        return numbers.Count;
    }

    /// <summary>Checks that every added object reads back as an integer from the writers' range.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="added">The ids each writer added.</param>
    /// <returns>A line for each object that is wrong.</returns>
    private static List<string> CheckAdded(PdfObjectStore store, ConcurrentBag<PdfObjectId>[] added)
    {
        var wrong = new List<string>();
        for (var writer = 0; writer < added.Length; writer++)
        {
            foreach (var id in added[writer])
            {
                var value = StoreReading.GetObject(store, id).AsInteger(-1);
                if (value < writer * AddsPerWriter || value >= (writer + 1) * AddsPerWriter)
                {
                    wrong.Add(string.Create(CultureInfo.InvariantCulture, $"object {id.Number} read {value}"));
                }
            }
        }

        return wrong;
    }

    /// <summary>Reads one document from many tasks and disposes it while they work.</summary>
    /// <param name="seed">The seed document.</param>
    /// <param name="expected">What a complete read gives.</param>
    /// <param name="round">The round, which sets how long the disposer waits.</param>
    /// <param name="token">Cancelled when the test has run too long.</param>
    /// <returns>A line for each unexpected exception or damaged read.</returns>
    private static Task<List<string>> DisposeOnceAsync(
        RobustnessSeeds.Seed seed,
        string expected,
        int round,
        CancellationToken token) =>
        DisposeOnceAsync(
        PdfDocumentReader.Open(
        seed.Bytes,
        null),
        seed,
        expected,
        round,
        token);

    /// <summary>Reads an open document from many tasks and disposes it while they work.</summary>
    /// <param name="document">The document, which this method disposes.</param>
    /// <param name="seed">The seed document.</param>
    /// <param name="expected">What a complete read gives.</param>
    /// <param name="round">The round, which sets how long the disposer waits.</param>
    /// <param name="token">Cancelled when the test has run too long.</param>
    /// <returns>A line for each unexpected exception or damaged read.</returns>
    private static async Task<List<string>> DisposeOnceAsync(PdfDocument document, RobustnessSeeds.Seed seed, string expected, int round, CancellationToken token)
    {
        // Completing a round proves progress; CPU contention across earlier rounds is not a hang in this one.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(Limit);
        var roundToken = timeout.Token;
        using var started = new CountdownEvent(Readers);
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var problems = new ConcurrentQueue<string>();
        var tasks = new Task[Readers + 1];
        var job = new DisposalReader(document, expected, problems, started, allStarted, roundToken);
        for (var i = 0; i < Readers; i++)
        {
            tasks[i] = ReadUntilDisposedAsync(job);
        }

        tasks[Readers] = DisposeAfterStartAsync(document, allStarted.Task, round, roundToken);
        try
        {
            await Task.WhenAll(tasks).WaitAsync(roundToken);
            var failures = new List<string>();
            foreach (var problem in problems)
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{seed.Name} round {round}: {problem}"));
            }

            return failures;
        }
        finally
        {
            document.Dispose();

            // Join readers before disposing their startup signal, including when the round is cancelled.
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>Waits until every reader has started, yields a number of times, then disposes the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="allStarted">Completes when every reader has started.</param>
    /// <param name="yields">How many times to yield before disposing.</param>
    /// <param name="token">Cancelled when the test has run too long.</param>
    /// <returns>A task.</returns>
    private static async Task DisposeAfterStartAsync(PdfDocument document, Task allStarted, int yields, CancellationToken token)
    {
        // Wait for startup without competing with readers for CPU.
        await allStarted.WaitAsync(token);

        for (var i = 0; i < yields; i++)
        {
            await Task.Yield();
        }

        document.Dispose();
    }

    /// <summary>Reads until the document is disposed; only ObjectDisposedException may stop the loop.</summary>
    /// <param name="job">The document, startup signal and cancellation for one disposal round.</param>
    /// <returns>A task that completes when reading stops.</returns>
    private static async Task ReadUntilDisposedAsync(DisposalReader job)
    {
        // Each read yields the worker, allowing the other readers and the disposer to run on small CI runners.
        await Task.Yield();
        var (document, expected, problems, started, allStarted, token) = job;
        if (started.Signal())
        {
            allStarted.SetResult();
        }

        try
        {
            var result = DocumentExerciser.Read(document);
            while (string.Equals(result, expected, StringComparison.Ordinal) && !token.IsCancellationRequested)
            {
                await Task.Yield();
                result = DocumentExerciser.Read(document);
            }

            if (!token.IsCancellationRequested)
            {
                problems.Enqueue("a read finished with different content");
            }
        }
        catch (ObjectDisposedException)
        {
            // A read after (or racing with) the dispose; this is the expected way out.
        }
        catch (Exception ex)
        {
            problems.Enqueue(ex.ToString());
        }
    }

    /// <summary>The shared state of readers racing one document disposal.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Expected">What a complete read gives.</param>
    /// <param name="Problems">Receives unexpected failures.</param>
    /// <param name="Started">Counts reader startup.</param>
    /// <param name="AllStarted">Completes when every reader starts.</param>
    /// <param name="Token">Cancels the round.</param>
    private sealed record DisposalReader(
        PdfDocument Document,
        string Expected,
        ConcurrentQueue<string> Problems,
        CountdownEvent Started,
        TaskCompletionSource AllStarted,
        CancellationToken Token);
}
