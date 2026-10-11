// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks demand-only persistence, shared generation and retry after cancellation.</summary>
public sealed class FontDataCacheTests
{
    /// <summary>The resource group under test.</summary>
    private const string Folder = "CMaps";

    /// <summary>The requested asset.</summary>
    private const string FileName = "requested.bin";

    /// <summary>The number of simultaneous requests.</summary>
    private const int Requests = 16;

    /// <summary>The generated fixture bytes.</summary>
    private static readonly byte[] Bytes = [1];

    /// <summary>The resource state owned by each test instance.</summary>
    private readonly PdfFontResources _resources = new();

    /// <summary>Only the requested asset and its notice are persisted; the second request does no generation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PersistsOnlyRequestedDataAndReusesIt()
    {
        using var cache = new FontDataTestCache();
        var calls = 0;
        ValueTask<byte[]> Generate(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _ = Interlocked.Increment(ref calls);
            return ValueTask.FromResult(Bytes);
        }

        await Assert.That(Directory.Exists(cache.DirectoryPath)).IsFalse();
        await FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, CancellationToken.None);
        await FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, CancellationToken.None);
        var files = Directory.GetFiles(cache.DirectoryPath, "*", SearchOption.AllDirectories).Select(static file => Path.GetFileName(file)!).ToArray();

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(files).IsEquivalentTo([FileName, "LICENSE.txt"]);
        await Assert.That(await File.ReadAllBytesAsync(Path.Combine(cache.DirectoryPath, Folder, FileName))).IsEquivalentTo(Bytes);
    }

    /// <summary>A completed entry published by another process is preserved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesAnotherProcessesPublication()
    {
        using var cache = new FontDataTestCache();
        var path = Path.Combine(cache.DirectoryPath, Folder, FileName);
        async ValueTask<byte[]> Generate(CancellationToken token)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [0x02], token);
            return [1];
        }

        await FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, CancellationToken.None);
        await Assert.That(await File.ReadAllBytesAsync(path)).IsEquivalentTo([(byte)0x02]);
        await Assert.That(Directory.GetFiles(cache.DirectoryPath, "*.tmp", SearchOption.AllDirectories)).IsEmpty();
    }

    /// <summary>An already published asset advances the version once without calling its generator.</summary>
    /// <returns>A task.</returns>
    [Test]
    [NotInParallel]
    public async Task ObservesExternalPublicationOnce()
    {
        using var cache = new FontDataTestCache();
        var path = Path.Combine(cache.DirectoryPath, Folder, FileName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Bytes);
        var calls = 0;
        ValueTask<byte[]> Generate(CancellationToken _)
        {
            calls++;
            return ValueTask.FromResult(Bytes);
        }

        var before = _resources.Generation;
        await FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, CancellationToken.None);
        var observed = _resources.Generation;
        await FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, CancellationToken.None);

        await Assert.That(calls).IsEqualTo(0);
        await Assert.That(observed).IsEqualTo(before + 1);
        await Assert.That(_resources.Generation).IsEqualTo(observed);
    }

    /// <summary>Concurrent requests share one successful generation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentRequestsGenerateOnce()
    {
        using var cache = new FontDataTestCache();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Requests));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask<byte[]> Generate(CancellationToken token)
        {
            _ = Interlocked.Increment(ref calls);
            _ = entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return [1];
        }

        var tasks = new Task[Requests];
        for (var index = 0; index < tasks.Length; index++)
        {
            tasks[index] = FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, timeout.Token).AsTask();
        }

        await entered.Task.WaitAsync(timeout.Token);
        await Assert.That(calls).IsEqualTo(1);
        _ = release.TrySetResult();
        await Task.WhenAll(tasks);
        await Assert.That(calls).IsEqualTo(1);
    }

    /// <summary>Cancelled generation publishes no file and leaves the request retryable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledGenerationCanRetry()
    {
        using var cache = new FontDataTestCache();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<byte[]> Generate(CancellationToken token)
        {
            _ = entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return [1];
        }

        var attempt = FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, cancellation.Token).AsTask();
        await entered.Task;
        await cancellation.CancelAsync();
        await Assert.That(async () => await attempt).Throws<OperationCanceledException>();
        await Assert.That(Directory.Exists(cache.DirectoryPath)).IsFalse();

        _ = release.TrySetResult();
        await FontDataResources.EnsureAsync(_resources.Data, cache.DirectoryPath, Folder, FileName, Generate, CancellationToken.None);
        await Assert.That(File.Exists(Path.Combine(cache.DirectoryPath, Folder, FileName))).IsTrue();
        await Assert.That(Directory.GetFiles(cache.DirectoryPath, "*.tmp", SearchOption.AllDirectories)).IsEmpty();
    }
}
