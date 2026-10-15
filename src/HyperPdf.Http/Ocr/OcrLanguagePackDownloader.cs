// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using HyperPdf.Http.Remote;

namespace HyperPdf.Http.Ocr;

/// <summary>
/// Downloads, finds and removes Tesseract language packs in a folder with Refit. Each pack streams to its own
/// <c>.part</c> file, is checked against its SHA-256 and is then moved into place, so Tesseract never sees a partial
/// or damaged pack. A pack already present is kept while its size and recorded hash still match the catalogue.
/// </summary>
[DebuggerDisplay("OCR language pack downloads with owned clients")]
public sealed class OcrLanguagePackDownloader : IDisposable
{
    /// <summary>The files checked by this service.</summary>
    private readonly DownloadFiles _files = new();

    /// <summary>The HTTP clients owned by this service.</summary>
    private readonly DownloadClients _clients = new();

    /// <summary>The reusable hashes owned by this service.</summary>
    private readonly DownloadHashes _hashes = new();

    /// <summary>Gets how many bytes a set of packs needs.</summary>
    /// <param name="packs">The packs.</param>
    /// <returns>The total.</returns>
    public static long TotalBytes(IReadOnlyList<OcrLanguagePack> packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        var total = 0L;
        for (var i = 0; i < packs.Count; i++)
        {
            total += packs[i].Bytes;
        }

        return total;
    }

    /// <summary>Determines whether a pack is downloaded and current in a folder.</summary>
    /// <param name="pack">The pack.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <returns><see langword="true"/> when it need not be downloaded.</returns>
    public bool IsInstalled(OcrLanguagePack pack, string directory)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return _files.For(directory, pack.FileName).IsCurrent(pack.Bytes, pack.Sha256);
    }

    /// <summary>Gets the packs not yet downloaded to a folder, or downloaded from another release.</summary>
    /// <param name="packs">The packs.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <returns>The missing packs.</returns>
    public List<OcrLanguagePack> Missing(IReadOnlyList<OcrLanguagePack> packs, string directory)
    {
        using var snapshot = FindMissing(packs, directory);
        var missing = new List<OcrLanguagePack>(snapshot.Count);
        for (var i = 0; i < snapshot.Count; i++)
        {
            missing.Add(snapshot.Items[i]);
        }

        return missing;
    }

    /// <summary>Deletes a downloaded pack and its recorded hash. Packs installed with Tesseract elsewhere are not touched.</summary>
    /// <param name="pack">The pack.</param>
    /// <param name="directory">The language pack folder.</param>
    public void Remove(OcrLanguagePack pack, string directory)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _files.For(directory, pack.FileName).Delete();
    }

    /// <summary>Downloads the missing packs from the catalogue's host.</summary>
    /// <param name="packs">The packs wanted.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download, keeping packs already finished.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails a request.</exception>
    /// <exception cref="InvalidDataException">Thrown when a download does not match its SHA-256.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DownloadAsync(IReadOnlyList<OcrLanguagePack> packs, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return DownloadMissingAsync(null, packs, directory, progress, cancellationToken);
    }

    /// <summary>Downloads the missing packs through a given client.</summary>
    /// <param name="api">A client whose base address is the packs' host.</param>
    /// <param name="packs">The packs wanted.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download, keeping packs already finished.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails a request.</exception>
    /// <exception cref="InvalidDataException">Thrown when a download does not match its SHA-256.</exception>
    public Task DownloadAsync(IRemoteDocumentApi api, IReadOnlyList<OcrLanguagePack> packs, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(progress);
        return DownloadMissingAsync(api, packs, directory, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _clients.Dispose();
        _hashes.Dispose();
    }

    /// <summary>Downloads the missing packs one after another. The client's base address is the host, so only each pack's path is sent.</summary>
    /// <param name="api">A client whose base address is the packs' host.</param>
    /// <param name="packs">The packs wanted.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>A task.</returns>
    private async Task DownloadMissingAsync(
        IRemoteDocumentApi? api,
        IReadOnlyList<OcrLanguagePack> packs,
        string directory,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        using var missing = FindMissing(packs, directory);
        if (missing.Count == 0)
        {
            progress.Report(1D);
            return;
        }

        var total = missing.Bytes;
        var client = api ?? _clients.For(OcrLanguageCatalog.BaseAddress);
        var done = 0L;
        var buffer = ArrayPool<byte>.Shared.Rent(VerifiedDownload.BufferSize);
        try
        {
            for (var i = 0; i < missing.Count; i++)
            {
                var pack = missing.Items[i];
                var file = _files.For(directory, pack.FileName);
                done += await VerifiedDownload.DownloadAsync(client, pack.RequestPath, file, pack.Sha256, new(buffer, _hashes), new(progress, done, total), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        new DownloadProgress(progress, done, total).Complete();
    }

    /// <summary>Snapshots missing packs without allocating a temporary list.</summary>
    /// <param name="packs">The chosen packs.</param>
    /// <param name="directory">The download folder.</param>
    /// <returns>The snapshot; the caller disposes it.</returns>
    private DownloadBatch<OcrLanguagePack> FindMissing(IReadOnlyList<OcrLanguagePack> packs, string directory)
    {
        ArgumentNullException.ThrowIfNull(packs);
        if (packs.Count == 0)
        {
            return new([], 0, 0);
        }

        var items = ArrayPool<OcrLanguagePack>.Shared.Rent(packs.Count);
        try
        {
            var count = 0;
            var bytes = 0L;
            for (var i = 0; i < packs.Count; i++)
            {
                var pack = packs[i];
                if (IsInstalled(pack, directory))
                {
                    continue;
                }

                items[count] = pack;
                count++;
                bytes += pack.Bytes;
            }

            return new(items, count, bytes);
        }
        catch
        {
            ArrayPool<OcrLanguagePack>.Shared.Return(items, true);
            throw;
        }
    }
}
