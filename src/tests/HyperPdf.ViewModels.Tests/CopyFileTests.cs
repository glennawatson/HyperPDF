// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.ViewModels.Output;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that a copy is written beside its destination and only moved into place when it was made.</summary>
public sealed class CopyFileTests
{
    /// <summary>The name of the copy.</summary>
    private const string FileName = "copy.pdf";

    /// <summary>The ending of the temporary file.</summary>
    private const string Suffix = ".copying";

    /// <summary>The bytes of the sample copy.</summary>
    private const int SampleLength = 300_000;

    /// <summary>A finished copy replaces the destination and leaves no temporary file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinishedCopyIsMovedIntoPlace()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, FileName);
        var bytes = new byte[SampleLength];
        RandomNumberGenerator.Fill(bytes);

        var written = await CopyFile.WriteBlocking(path, Suffix, (stream, token) => WriteAll(bytes, stream, token), static done => done, CancellationToken.None).ToTask();

        await Assert.That(written).IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(path)).IsEquivalentTo(bytes);
        await Assert.That(File.Exists(path + Suffix)).IsFalse();
    }

    /// <summary>A copy the writer says was not made leaves nothing at the destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusedCopyLeavesNothing()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, FileName);

        var written = await CopyFile.WriteBlocking(path, Suffix, static (_, _) => false, static done => done, CancellationToken.None).ToTask();

        await Assert.That(written).IsFalse();
        await Assert.That(File.Exists(path)).IsFalse();
        await Assert.That(File.Exists(path + Suffix)).IsFalse();
    }

    /// <summary>A stopped copy fails with the cancellation and leaves nothing behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StoppedCopyLeavesNothing()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, FileName);
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        var bytes = new byte[SampleLength];

        var outcome = CopyFile.WriteBlocking(path, Suffix, (stream, token) => WriteAll(bytes, stream, token), static done => done, stop.Token).ToTask();

        await Assert.That(async () => await outcome).Throws<OperationCanceledException>();
        await Assert.That(File.Exists(path)).IsFalse();
        await Assert.That(File.Exists(path + Suffix)).IsFalse();
    }

    /// <summary>A writer that fails reports the failure and leaves nothing behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedCopyLeavesNothing()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, FileName);

        var outcome = CopyFile.WriteBlocking<bool>(path, Suffix, static (_, _) => throw new IOException("The disk is full."), static done => done, CancellationToken.None).ToTask();

        await Assert.That(async () => await outcome).Throws<IOException>();
        await Assert.That(File.Exists(path)).IsFalse();
        await Assert.That(File.Exists(path + Suffix)).IsFalse();
    }

    /// <summary>Copies the bytes through the same chunked copy the app uses.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="stream">The temporary file.</param>
    /// <param name="token">Stops the copy.</param>
    /// <returns><see langword="true"/> once copied.</returns>
    private static bool WriteAll(byte[] bytes, Stream stream, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var input = new MemoryStream(bytes);
        return CopyFile.CopyChunks(input, stream, token);
    }

    /// <summary>A folder that is removed with its contents when disposed.</summary>
    private sealed class TemporaryFolder : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TemporaryFolder"/> class.</summary>
        internal TemporaryFolder() => Path = Directory.CreateTempSubdirectory("hyperpdf-copy-").FullName;

        /// <summary>Gets the folder.</summary>
        internal string Path { get; }

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path, true);
    }
}
