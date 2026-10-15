// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using HyperPdf.Core.Speech;

namespace HyperPdf.Http.Speech;

/// <summary>
/// Downloads an on-device voice's files with Refit. Each file is written to its own <c>.part</c> file and moved into
/// place once complete, so an interrupted download is never mistaken for a finished one and two downloads at once do
/// not collide. Each download is checked against its SHA-256 before it is kept, and the hash is recorded beside it;
/// a file already present is kept while its size and recorded hash still match the release.
/// </summary>
[DebuggerDisplay("Speech model downloads with owned clients")]
public sealed class SpeechModelDownloader : IDisposable
{
    /// <summary>The files checked by this service.</summary>
    private readonly DownloadFiles _files = new();

    /// <summary>The HTTP clients owned by this service.</summary>
    private readonly DownloadClients _clients = new();

    /// <summary>The reusable hashes owned by this service.</summary>
    private readonly DownloadHashes _hashes = new();

    /// <summary>Gets about how many bytes a set of files needs.</summary>
    /// <param name="files">The files.</param>
    /// <returns>The approximate total.</returns>
    public static long TotalBytes(IReadOnlyList<SpeechModelFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var total = 0L;
        foreach (var file in files)
        {
            total += file.Bytes;
        }

        return total;
    }

    /// <summary>Gets the files not yet downloaded, or downloaded from an older voice release.</summary>
    /// <param name="files">The files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <returns>The missing files.</returns>
    public List<SpeechModelFile> Missing(IReadOnlyList<SpeechModelFile> files, string directory)
    {
        using var snapshot = FindMissing(files, directory);
        var missing = new List<SpeechModelFile>(snapshot.Count);
        for (var i = 0; i < snapshot.Count; i++)
        {
            missing.Add(snapshot.Items[i]);
        }

        return missing;
    }

    /// <summary>Downloads the missing files.</summary>
    /// <param name="files">The files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download, leaving finished files in place.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when a server fails a request.</exception>
    /// <exception cref="InvalidDataException">Thrown when a file's name would place it outside the voice folder, or a download does not match its SHA-256.</exception>
    public async Task DownloadAsync(IReadOnlyList<SpeechModelFile> files, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        using var missing = FindMissing(files, directory);
        if (missing.Count == 0)
        {
            progress.Report(1D);
            return;
        }

        var total = missing.Bytes;
        var done = 0L;
        var buffer = ArrayPool<byte>.Shared.Rent(VerifiedDownload.BufferSize);
        try
        {
            for (var i = 0; i < missing.Count; i++)
            {
                var file = missing.Items[i];
                done += await VerifiedDownload.DownloadAsync(
                    _clients.For(file.Source),
                    RefitClients.RequestPathOf(file.Source),
                    _files.For(directory, file.LocalName),
                    file.Sha256,
                    new(buffer, _hashes),
                    new(progress, done, total),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        new DownloadProgress(progress, done, total).Complete();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _clients.Dispose();
        _hashes.Dispose();
    }

    /// <summary>Snapshots missing voice files without allocating a temporary list.</summary>
    /// <param name="files">The chosen files.</param>
    /// <param name="directory">The download folder.</param>
    /// <returns>The snapshot; the caller disposes it.</returns>
    private DownloadBatch<SpeechModelFile> FindMissing(IReadOnlyList<SpeechModelFile> files, string directory)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (files.Count == 0)
        {
            return new([], 0, 0);
        }

        var items = ArrayPool<SpeechModelFile>.Shared.Rent(files.Count);
        try
        {
            var count = 0;
            var bytes = 0L;
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (_files.For(directory, file.LocalName).IsCurrent(file.Bytes, file.Sha256))
                {
                    continue;
                }

                items[count] = file;
                count++;
                bytes += file.Bytes;
            }

            return new(items, count, bytes);
        }
        catch
        {
            ArrayPool<SpeechModelFile>.Shared.Return(items, true);
            throw;
        }
    }
}
