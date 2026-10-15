// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using HyperPdf.Core.Ocr;
using HyperPdf.Http;
using HyperPdf.Http.Ocr;

namespace HyperPdf.Core.Tests.Ocr;

/// <summary>Checks language packs download through Refit intact, atomically and cancellably, and can be removed.</summary>
public sealed class OcrLanguagePackDownloaderTests
{
    /// <summary>The size of the test pack; several copy buffers, so progress is reported more than once.</summary>
    private const int PackSize = 200_000;

    /// <summary>The host the catalogue's packs come from.</summary>
    private static readonly Uri Host = new("https://raw.githubusercontent.com");

    /// <summary>A pack downloads into the folder with its hash recorded, reporting progress to the end and leaving no partial file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DownloadsAndVerifiesAPack()
    {
        using var downloader = new OcrLanguagePackDownloader();
        using var folder = new TemporaryFolder();
        var content = Content();
        var pack = PackFor(content);
        using var handler = new FakeHandler(content);
        var progress = new RecordingProgress();

        await downloader.DownloadAsync(Api(handler), [pack], folder.Path, progress, CancellationToken.None);

        var path = Path.Combine(folder.Path, pack.FileName);
        await Assert.That(await File.ReadAllBytesAsync(path)).IsEquivalentTo(content);
        await Assert.That(await File.ReadAllTextAsync($"{path}.sha256")).IsEqualTo(pack.Sha256);
        await Assert.That(downloader.IsInstalled(pack, folder.Path)).IsTrue();
        await Assert.That(handler.Requests).IsEquivalentTo(["/tesseract-ocr/tessdata_fast/4.1.0/tst.traineddata"]);
        await Assert.That(progress.Values.Count).IsGreaterThan(1);
        await Assert.That(progress.Values[^1]).IsEqualTo(1D);
        await Assert.That(Directory.GetFiles(folder.Path, "*.part")).IsEmpty();
    }

    /// <summary>A pack already downloaded is not fetched again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsInstalledPacks()
    {
        using var downloader = new OcrLanguagePackDownloader();
        using var folder = new TemporaryFolder();
        var content = Content();
        var pack = PackFor(content);
        using var handler = new FakeHandler(content);
        await downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None);

        await downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None);

        await Assert.That(handler.Requests.Count).IsEqualTo(1);
        await Assert.That(downloader.Missing([pack], folder.Path)).IsEmpty();
    }

    /// <summary>A damaged download is removed, and its hash context is clean when the same service retries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DiscardsADamagedPack()
    {
        using var downloader = new OcrLanguagePackDownloader();
        using var folder = new TemporaryFolder();
        var content = Content();
        var pack = PackFor(content);
        var damaged = (byte[])content.Clone();
        damaged[0] ^= 0xFF;
        using var handler = new FakeHandler(damaged);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None));

        await Assert.That(error!.Message).Contains("did not download intact");
        await Assert.That(Directory.GetFiles(folder.Path)).IsEmpty();
        await Assert.That(downloader.IsInstalled(pack, folder.Path)).IsFalse();

        damaged[0] ^= 0xFF;
        await downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None);
        await Assert.That(downloader.IsInstalled(pack, folder.Path)).IsTrue();
    }

    /// <summary>A server failure is reported and leaves nothing behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsServerFailures()
    {
        using var downloader = new OcrLanguagePackDownloader();
        using var folder = new TemporaryFolder();
        var pack = PackFor(Content());
        using var handler = new FakeHandler(null);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None));

        await Assert.That(Directory.GetFiles(folder.Path)).IsEmpty();
    }

    /// <summary>Cancellation removes partial output and resets the reused hash before a retry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelsWithoutLeavingAPartialFile()
    {
        using var downloader = new OcrLanguagePackDownloader();
        using var folder = new TemporaryFolder();
        var content = Content();
        var pack = PackFor(content);
        using var handler = new FakeHandler(content);
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress(cancellation.Cancel);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => downloader.DownloadAsync(Api(handler), [pack], folder.Path, progress, cancellation.Token));

        await Assert.That(Directory.GetFiles(folder.Path)).IsEmpty();
        await downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None);
        await Assert.That(await File.ReadAllBytesAsync(Path.Combine(folder.Path, pack.FileName))).IsEquivalentTo(content);
    }

    /// <summary>Removing a pack deletes it and its recorded hash.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesAPack()
    {
        using var downloader = new OcrLanguagePackDownloader();
        using var folder = new TemporaryFolder();
        var content = Content();
        var pack = PackFor(content);
        using var handler = new FakeHandler(content);
        await downloader.DownloadAsync(Api(handler), [pack], folder.Path, new RecordingProgress(), CancellationToken.None);

        downloader.Remove(pack, folder.Path);

        await Assert.That(Directory.GetFiles(folder.Path)).IsEmpty();
        await Assert.That(downloader.Missing([pack], folder.Path)).IsEquivalentTo([pack]);
    }

    /// <summary>Makes the test pack's bytes.</summary>
    /// <returns>The bytes.</returns>
    private static byte[] Content()
    {
        var content = new byte[PackSize];
        for (var i = 0; i < content.Length; i++)
        {
            content[i] = (byte)i;
        }

        return content;
    }

    /// <summary>Describes a test pack holding the given bytes.</summary>
    /// <param name="content">The bytes.</param>
    /// <returns>The pack.</returns>
    private static OcrLanguagePack PackFor(byte[] content) => new("tst", "Sample language", content.Length, Convert.ToHexStringLower(SHA256.HashData(content)));

    /// <summary>Creates a Refit client for the catalogue's host that sends through a fake handler.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns>The client.</returns>
    private static Http.Remote.IRemoteDocumentApi Api(FakeHandler handler) =>
        RefitClients.CreateRemoteDocumentApi(new(handler, false) { BaseAddress = Host });

    /// <summary>Answers every request with fixed content, or 404 when there is none, and records the paths asked for.</summary>
    /// <param name="content">The content, or <see langword="null"/> to fail.</param>
    private sealed class FakeHandler(byte[]? content) : HttpMessageHandler
    {
        /// <summary>Gets the paths requested.</summary>
        public ConcurrentQueue<string> Requests { get; } = new();

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(request.RequestUri!.AbsolutePath);
            return Task.FromResult(content is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }

    /// <summary>Records each fraction reported, straight away, and can act on the first.</summary>
    /// <param name="onFirst">Run when the first fraction arrives, or <see langword="null"/>.</param>
    private sealed class RecordingProgress(Action? onFirst = null) : IProgress<double>
    {
        /// <summary>Gets the fractions reported.</summary>
        public List<double> Values { get; } = [];

        /// <inheritdoc/>
        public void Report(double value)
        {
            Values.Add(value);
            if (Values.Count == 1)
            {
                onFirst?.Invoke();
            }
        }
    }

    /// <summary>A folder deleted when disposed.</summary>
    private sealed class TemporaryFolder : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TemporaryFolder"/> class.</summary>
        public TemporaryFolder() => _ = Directory.CreateDirectory(Path);

        /// <summary>Gets the folder.</summary>
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tessdata-{Guid.NewGuid():N}");

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path, true);
    }
}
