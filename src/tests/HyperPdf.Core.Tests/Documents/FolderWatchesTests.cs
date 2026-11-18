// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Checks that many files of one folder are watched through the folder, and that each is told only of its own changes.</summary>
public sealed class FolderWatchesTests
{
    /// <summary>The whole test may take this long on a loaded runner.</summary>
    private const int TimeoutMilliseconds = 90_000;

    /// <summary>How many times the file is replaced before the test gives up waiting for a notification.</summary>
    private const int Attempts = 5;

    /// <summary>The files watched at once: more than the watchers a Linux user may hold with one for each file.</summary>
    private const int Files = 300;

    /// <summary>The file that is replaced.</summary>
    private const int Replaced = 150;

    /// <summary>How long a watch that should tell nothing is left to tell something.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(50);

    /// <summary>How long the new files are left alone before they are watched, longer than a change takes to be told.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    /// <summary>How long one notification is waited for.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Replacing one of hundreds of watched files in a folder tells that file's watcher, and not another file's.</summary>
    /// <param name="cancellationToken">Cancels the test when it runs too long.</param>
    /// <returns>A task.</returns>
    [Test]
    [Timeout(TimeoutMilliseconds)]
    public async Task ReplacingOneOfManyWatchedFilesTellsOnlyThatFile(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"watches-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        var following = new List<IDisposable>(Files);
        try
        {
            var watches = new FolderWatches();
            using var changed = new SemaphoreSlim(0);
            var others = 0;
            for (var i = 0; i < Files; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, $"doc-{i}.pdf"), "one", cancellationToken);
            }

            // The files are made before any is watched, and left to settle, so what a watcher hears is the replace alone.
            await Task.Delay(Settle, cancellationToken);
            for (var i = 0; i < Files; i++)
            {
                var path = Path.Combine(directory, $"doc-{i}.pdf");
                following.Add(i == Replaced
                    ? watches.Watch(path).SubscribeSafe(_ => changed.Release(), static _ => { })
                    : watches.Watch(path).SubscribeSafe(_ => Interlocked.Increment(ref others), static _ => { }));
            }

            // A loaded runner can drop the operating system's notification, so replace again until one is seen.
            var target = Path.Combine(directory, $"doc-{Replaced}.pdf");
            var temp = $"{target}.tmp";
            var seen = false;
            for (var attempt = 0; !seen && attempt < Attempts; attempt++)
            {
                await File.WriteAllTextAsync(temp, $"two-{attempt}", cancellationToken);
                File.Move(temp, target, true);
                seen = await changed.WaitAsync(Timeout, cancellationToken);
            }

            await Assert.That(seen).IsTrue();
            await Assert.That(Volatile.Read(ref others)).IsEqualTo(0);
        }
        finally
        {
            foreach (var follow in following)
            {
                follow.Dispose();
            }

            Directory.Delete(directory, true);
        }
    }

    /// <summary>A file in a folder that does not exist is watched without failing and tells nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingFolderIsQuiet()
    {
        var emitted = false;
        using (new FolderWatches().Watch($"/nonexistent-{Guid.NewGuid():N}/doc.pdf").SubscribeSafe(_ => emitted = true, static _ => { }))
        {
            await Task.Delay(QuietPeriod);
        }

        await Assert.That(emitted).IsFalse();
    }
}
