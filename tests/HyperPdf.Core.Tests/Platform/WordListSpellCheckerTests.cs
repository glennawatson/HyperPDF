// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Platform.Linux.Spelling;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests the word list spell checker against small lists written for the test.</summary>
public sealed class WordListSpellCheckerTests
{
    /// <summary>The British English list's name.</summary>
    private const string British = "british-english";

    /// <summary>The words in each list.</summary>
    private const string Words = "hello\nworld\nreceive\nrelieve\nform\nBritain\n";

    /// <summary>A word in the list, with a capital.</summary>
    private const string Capitalised = "Receive";

    /// <summary>A word that is not in the list.</summary>
    private const string Misspelt = "recieve";

    /// <summary>The checkers made and disposed while words are checked.</summary>
    private const int DisposeRounds = 200;

    /// <summary>The threads that check words.</summary>
    private const int CheckingThreads = 4;

    /// <summary>The size of the pool arrays a small word list uses.</summary>
    private const int SmallArray = 16;

    /// <summary>The arrays rented after the dispose, enough to cover both the table and the word starts.</summary>
    private const int PoisonedArrays = 4;

    /// <summary>The length of the word whose hash takes long enough to overlap the dispose.</summary>
    private const int LongWordLength = 2_000_000;

    /// <summary>Verifies words are checked without case, and corrections keep the typed capitals.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksAndSuggests()
    {
        var folder = CreateWordLists(British);
        try
        {
            using var checker = new WordListSpellChecker(Path.Combine(folder, British));

            await Assert.That(checker.IsAvailable).IsTrue();
            await Assert.That(checker.Language).IsEqualTo(British);
            await Assert.That(checker.IsCorrect(Capitalised)).IsTrue();
            await Assert.That(checker.IsCorrect("britain")).IsTrue();
            await Assert.That(checker.IsCorrect(Misspelt)).IsFalse();
            List<string> suggestions = [];
            checker.Suggest(Misspelt, suggestions);
            await Assert.That(suggestions).Contains("receive");
            suggestions.Clear();
            checker.Suggest("Recieve", suggestions);
            await Assert.That(suggestions).Contains("Receive");
            suggestions.Clear();
            checker.Suggest("RECIEVE", suggestions);
            await Assert.That(suggestions).Contains("RECEIVE");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Verifies the reader's own list comes first, then the package for their region, and none means unavailable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsTheReadersWordList()
    {
        var folder = CreateWordLists("american-english", British, "en-GB.txt", "french");
        try
        {
            string[] folders = [Path.Combine(folder, "missing"), folder];

            await Assert.That(Path.GetFileName(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("en-GB"), folders))).IsEqualTo("en-GB.txt");
            await Assert.That(Path.GetFileName(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("en-AU"), folders))).IsEqualTo(British);
            await Assert.That(Path.GetFileName(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("fr-CA"), folders))).IsEqualTo("french");
            await Assert.That(WordListSpellChecker.FindWordList(CultureInfo.GetCultureInfo("ja-JP"), folders)).IsNull();
            await Assert.That(new WordListSpellChecker(null).IsAvailable).IsFalse();
            await Assert.That(new WordListSpellChecker(null).IsCorrect("anything")).IsTrue();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Checks words on several threads while the checker is disposed and other threads fill returned pool arrays with bad
    /// values: a check never reads an array that went back to the pool.
    /// </summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeWhileChecksRunNeverReadsReturnedArrays(CancellationToken cancellationToken)
    {
        var folder = CreateWordLists(British);
        try
        {
            var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
            var longWord = new string('a', LongWordLength);
            for (var round = 0; round < DisposeRounds; round++)
            {
                using var started = new SemaphoreSlim(0);
                var checker = new WordListSpellChecker(Path.Combine(folder, British));
                using var stop = new CancellationTokenSource();
                var job = new CheckJob(checker, failures, longWord, started, stop.Token);
                var tasks = new List<Task>();

                // The checks spin until stopped, so they run on their own threads and never starve the thread pool.
                for (var reader = 0; reader < CheckingThreads; reader++)
                {
                    tasks.Add(DedicatedThread.Run(job, CheckUntilStopped, cancellationToken));
                }

                // Hashing the long word takes long enough that a check is still running when the checker is disposed.
                for (var reader = 0; reader < CheckingThreads; reader++)
                {
                    await started.WaitAsync(cancellationToken);
                }

                await DedicatedThread.Run(checker, DisposeAndPoison, cancellationToken);
                await stop.CancelAsync();
                await Task.WhenAll(tasks);
            }

            await Assert.That(failures.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Checks words until stopped, recording any exception.</summary>
    /// <param name="job">The checker, the words, and what stops and reports the loop.</param>
    private static void CheckUntilStopped(CheckJob job)
    {
        try
        {
            var signalled = false;
            while (!job.Stop.IsCancellationRequested)
            {
                _ = job.Checker.IsCorrect(Capitalised);
                if (!signalled)
                {
                    _ = job.Started.Release();
                    signalled = true;
                }

                _ = job.Checker.IsCorrect(job.LongWord);
            }
        }
        catch (Exception ex)
        {
            job.Failures.Enqueue(ex);

            // The test waits for every thread to start, so a thread that failed first still lets it go on.
            _ = job.Started.Release();
        }
    }

    /// <summary>
    /// Disposes the checker, then rents the small int arrays the pool just returned and fills them with a value no word
    /// table holds, on the same thread so the pool hands them back, so a check still reading them fails loudly.
    /// </summary>
    /// <param name="checker">The checker.</param>
    private static void DisposeAndPoison(WordListSpellChecker checker)
    {
        checker.Dispose();
        var rented = new int[PoisonedArrays][];
        for (var i = 0; i < rented.Length; i++)
        {
            rented[i] = System.Buffers.ArrayPool<int>.Shared.Rent(SmallArray);
            rented[i].AsSpan().Fill(int.MaxValue);
        }

        foreach (var array in rented)
        {
            System.Buffers.ArrayPool<int>.Shared.Return(array);
        }
    }

    /// <summary>Writes word lists into a new folder.</summary>
    /// <param name="names">The lists' file names.</param>
    /// <returns>The folder.</returns>
    private static string CreateWordLists(params string[] names)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-words-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(folder, name), Words);
        }

        return folder;
    }

    /// <summary>What one checking thread reads and reports to.</summary>
    /// <param name="Checker">The checker.</param>
    /// <param name="Failures">Receives the exceptions.</param>
    /// <param name="LongWord">A word whose hash takes long enough to overlap the dispose.</param>
    /// <param name="Started">Released once the thread has made its first check.</param>
    /// <param name="Stop">Stops the loop.</param>
    private sealed record CheckJob(
        WordListSpellChecker Checker,
        System.Collections.Concurrent.ConcurrentQueue<Exception> Failures,
        string LongWord,
        SemaphoreSlim Started,
        CancellationToken Stop);
}
