// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using HyperPdf.Http.Remote;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

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
    public static long TotalBytes(ReadOnlySpan<OcrLanguagePack> packs)
    {
        var total = 0L;
        for (var i = 0; i < packs.Length; i++)
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
    public List<OcrLanguagePack> Missing(OcrLanguagePack[] packs, string directory)
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
    /// <returns>
    /// A cold signal giving one value once every pack is in place; unsubscribing stops the download and keeps packs already finished. It fails
    /// with <see cref="HttpRequestException"/> when the server fails a request and with <see cref="InvalidDataException"/> when a download does not match its SHA-256.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Download(OcrLanguagePack[] packs, string directory, IProgress<double> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return DownloadMissing(null, packs, directory, progress);
    }

    /// <summary>Downloads the missing packs through a given client.</summary>
    /// <param name="api">A client whose base address is the packs' host.</param>
    /// <param name="packs">The packs wanted.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <returns>A cold signal giving one value once every pack is in place; unsubscribing stops the download and keeps packs already finished.</returns>
    public IObservable<RxVoid> Download(IRemoteDocumentApi api, OcrLanguagePack[] packs, string directory, IProgress<double> progress)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(progress);
        return DownloadMissing(api, packs, directory, progress);
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
    /// <returns>A cold signal giving one value once every pack is in place.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> DownloadMissing(IRemoteDocumentApi? api, OcrLanguagePack[] packs, string directory, IProgress<double> progress) =>
        Signal.Using(
            () => FindMissing(packs, directory),
            missing =>
            {
                if (missing.Count == 0)
                {
                    progress.Report(1D);
                    return Signal.Return(RxVoid.Default);
                }

                var client = api ?? _clients.For(OcrLanguageCatalog.BaseAddress);
                return DownloadSequence.Run(missing, progress, (pack, step) => VerifiedDownload.Download(client, pack.RequestPath, _files.For(directory, pack.FileName), pack.Sha256, _hashes, step));
            });

    /// <summary>Snapshots missing packs without allocating a temporary list.</summary>
    /// <param name="packs">The chosen packs.</param>
    /// <param name="directory">The download folder.</param>
    /// <returns>The snapshot; the caller disposes it.</returns>
    private DownloadBatch<OcrLanguagePack> FindMissing(ReadOnlySpan<OcrLanguagePack> packs, string directory)
    {
        if (packs.IsEmpty)
        {
            return new([], 0, 0);
        }

        var items = ArrayPool<OcrLanguagePack>.Shared.Rent(packs.Length);
        try
        {
            var count = 0;
            var bytes = 0L;
            for (var i = 0; i < packs.Length; i++)
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
