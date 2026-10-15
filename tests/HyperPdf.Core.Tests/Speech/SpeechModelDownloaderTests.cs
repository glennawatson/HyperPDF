// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;
using HyperPdf.Http.Speech;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>Checks which voice files are downloaded again: missing ones, and ones a newer voice release replaced.</summary>
public sealed class SpeechModelDownloaderTests
{
    /// <summary>The size of the test file.</summary>
    private const int Size = 16;

    /// <summary>The hash the test release pins.</summary>
    private const string Pinned = "aa";

    /// <summary>The test file's name.</summary>
    private const string Name = "model.onnx";

    /// <summary>A file at the pinned size with no recorded hash, as left by an older download, is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsAFileOfTheRightSize()
    {
        using var downloader = new SpeechModelDownloader();
        using var folder = new TemporaryFolder();
        await File.WriteAllBytesAsync(Path.Combine(folder.Path, Name), new byte[Size]);

        await Assert.That(downloader.Missing([FileOf(Size, Pinned)], folder.Path)).IsEmpty();
    }

    /// <summary>A file missing, of another size, or whose recorded hash is not the pinned one is downloaded again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DownloadsReplacedFilesAgain()
    {
        using var downloader = new SpeechModelDownloader();
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, Name);

        var whenMissing = downloader.Missing([FileOf(Size, Pinned)], folder.Path).Count;
        await File.WriteAllBytesAsync(path, new byte[Size]);
        var whenResized = downloader.Missing([FileOf(Size + 1, Pinned)], folder.Path).Count;
        await File.WriteAllTextAsync($"{path}.sha256", "bb");
        var whenRehashed = downloader.Missing([FileOf(Size, Pinned)], folder.Path).Count;
        await File.WriteAllTextAsync($"{path}.sha256", Pinned);
        var whenCurrent = downloader.Missing([FileOf(Size, Pinned)], folder.Path).Count;

        await Assert.That(whenMissing).IsEqualTo(1);
        await Assert.That(whenResized).IsEqualTo(1);
        await Assert.That(whenRehashed).IsEqualTo(1);
        await Assert.That(whenCurrent).IsEqualTo(0);
    }

    /// <summary>Describes the test file.</summary>
    /// <param name="bytes">Its pinned size.</param>
    /// <param name="sha256">Its pinned hash.</param>
    /// <returns>The file.</returns>
    private static SpeechModelFile FileOf(long bytes, string sha256) => new(new("https://example.com/model.onnx"), Name, bytes, sha256);

    /// <summary>A folder deleted when disposed.</summary>
    private sealed class TemporaryFolder : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TemporaryFolder"/> class.</summary>
        public TemporaryFolder() => _ = Directory.CreateDirectory(Path);

        /// <summary>Gets the folder.</summary>
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"voices-{Guid.NewGuid():N}");

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path, true);
    }
}
