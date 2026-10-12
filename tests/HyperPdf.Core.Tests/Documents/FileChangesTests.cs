// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="FileChanges"/>.</summary>
public sealed class FileChangesTests
{
    /// <summary>How long to wait for a notification.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>How long to observe a watch that should stay quiet.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(50);

    /// <summary>Verifies an atomic replace is reported once it settles, and disposing stops watching.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsAtomicReplace()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"watch-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "doc.pdf");
            await File.WriteAllTextAsync(path, "one");
            using var changed = new SemaphoreSlim(0);
            using (FileChanges.Watch(path).SubscribeSafe(_ => changed.Release(), static _ => { }))
            {
                var temp = Path.Combine(directory, "doc.pdf.tmp");
                await File.WriteAllTextAsync(temp, "two");
                File.Move(temp, path, true);

                await Assert.That(await changed.WaitAsync(Timeout)).IsTrue();
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
