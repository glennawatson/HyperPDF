// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Platform.Linux.Recent;

namespace PdfViewerLite.Core.Tests.Platform;

/// <summary>Tests for <see cref="XbelRecentDocumentStore"/>.</summary>
public sealed class XbelRecentDocumentStoreTests
{
    /// <summary>Minimal file contents for recent-list tests.</summary>
    private const string PdfContents = "%PDF-1.7";

    /// <summary>Verifies documents are recorded once, listed newest first, and other entries are preserved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordsAndListsDocuments()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xbel-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var xbel = Path.Combine(directory, "recently-used.xbel");
            var first = Path.Combine(directory, "first.pdf");
            var second = Path.Combine(directory, "second.pdf");
            await File.WriteAllTextAsync(first, PdfContents);
            await File.WriteAllTextAsync(second, PdfContents);
            await File.WriteAllTextAsync(xbel, """
                <?xml version="1.0" encoding="UTF-8"?>
                <xbel version="1.0" xmlns:bookmark="http://www.freedesktop.org/standards/desktop-bookmarks" xmlns:mime="http://www.freedesktop.org/standards/shared-mime-info">
                  <bookmark href="file:///etc/hosts" added="2020-01-01T00:00:00Z" modified="2020-01-01T00:00:00Z" visited="2020-01-01T00:00:00Z">
                    <info><metadata owner="http://freedesktop.org"><mime:mime-type type="text/plain"/></metadata></info>
                  </bookmark>
                </xbel>
                """);
            var clock = new ManualTimeProvider(new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var store = new XbelRecentDocumentStore(xbel, clock);

            store.Add(first);
            clock.Advance(TimeSpan.FromMinutes(1));
            store.Add(second);
            clock.Advance(TimeSpan.FromMinutes(1));
            store.Add(first);

            const int maxResults = 10;
            const int pdfCount = 2;
            var recent = store.GetRecent(maxResults);
            await Assert.That(recent.Count).IsEqualTo(pdfCount);
            await Assert.That(recent[0].FilePath).IsEqualTo(first);
            await Assert.That(recent[1].FilePath).IsEqualTo(second);
            var content = await File.ReadAllTextAsync(xbel);
            await Assert.That(content).Contains("file:///etc/hosts");
            await Assert.That(content).Contains("count=\"2\"");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Cleanup removes PDF bookmarks while preserving other file types and the PDF files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesPdfBookmarksOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xbel-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        try
        {
            var xbel = Path.Combine(directory, "recently-used.xbel");
            var first = Path.Combine(directory, "first.pdf");
            var second = Path.Combine(directory, "second.pdf");
            await File.WriteAllTextAsync(first, PdfContents);
            await File.WriteAllTextAsync(second, PdfContents);
            await File.WriteAllTextAsync(xbel, """
                <?xml version="1.0" encoding="UTF-8"?>
                <xbel version="1.0" xmlns:bookmark="http://www.freedesktop.org/standards/desktop-bookmarks" xmlns:mime="http://www.freedesktop.org/standards/shared-mime-info">
                  <bookmark href="file:///etc/hosts"><info><metadata owner="http://freedesktop.org"><mime:mime-type type="text/plain"/></metadata></info></bookmark>
                </xbel>
                """);
            var store = new XbelRecentDocumentStore(xbel, TimeProvider.System);
            store.Add(first);
            store.Add(second);
            store.Remove(first);
            const int maxResults = 10;
            await Assert.That(store.GetRecent(maxResults).Select(static entry => entry.FilePath)).IsEquivalentTo([second]);
            store.Clear();
            await Assert.That(store.GetRecent(maxResults)).IsEmpty();
            await Assert.That(await File.ReadAllTextAsync(xbel)).Contains("file:///etc/hosts");
            await Assert.That(File.Exists(first) && File.Exists(second)).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>A clock the test controls.</summary>
    /// <param name="now">The starting time.</param>
    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        /// <summary>The current time.</summary>
        private DateTimeOffset _now = now;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _now;

        /// <summary>Moves the clock forward.</summary>
        /// <param name="by">The amount.</param>
        internal void Advance(TimeSpan by) => _now += by;
    }
}
