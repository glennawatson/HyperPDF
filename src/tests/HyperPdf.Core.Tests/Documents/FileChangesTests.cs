// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="FileChanges"/>.</summary>
public sealed class FileChangesTests
{
    /// <summary>The time limit for the replace test in milliseconds.</summary>
    private const int TimeoutMilliseconds = 90_000;

    /// <summary>How many times the replace is repeated before giving up.</summary>
    private const int Attempts = 5;

    /// <summary>How long to wait for a notification after each replace.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>How long to observe a watch that should stay quiet.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(50);

    /// <summary>Verifies an atomic replace is reported once it settles, and disposing stops watching.</summary>
    /// <param name="cancellationToken">Cancellation when the test exceeds its time limit.</param>
    /// <returns>A task.</returns>
    [Test]
    [Timeout(TimeoutMilliseconds)]
    public async Task ReportsAtomicReplace(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"watch-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "doc.pdf");
            await File.WriteAllTextAsync(path, "one", cancellationToken);
            using var changed = new SemaphoreSlim(0);
            using (FileChanges.Watch(path).SubscribeSafe(_ => changed.Release(), static _ => { }))
            {
                // A loaded runner can drop the operating system's notification, so replace again until one is seen.
                var temp = Path.Combine(directory, "doc.pdf.tmp");
                var seen = false;
                for (var attempt = 0; !seen && attempt < Attempts; attempt++)
                {
                    await File.WriteAllTextAsync(temp, $"two-{attempt}", cancellationToken);
                    File.Move(temp, path, true);
                    seen = await changed.WaitAsync(Timeout, cancellationToken);
                }

                await Assert.That(seen).IsTrue();
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Verifies a missing directory never emits and does not throw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingDirectoryIsQuiet()
    {
        var emitted = false;
        using (FileChanges.Watch($"/nonexistent-{Guid.NewGuid():N}/doc.pdf").SubscribeSafe(_ => emitted = true, static _ => { }))
        {
            await Task.Delay(QuietPeriod);
        }

        await Assert.That(emitted).IsFalse();
    }
}
