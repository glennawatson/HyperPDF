// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using HyperPdf.Http.Remote;

namespace HyperPdf.Http.Ocr;

/// <summary>
/// Downloads, finds and removes Tesseract language packs in a folder with Refit. Each pack streams to its own
/// <c>.part</c> file, is checked against its SHA-256 and is then moved into place, so Tesseract never sees a partial
/// or damaged pack. A pack already present is kept while its size and recorded hash still match the catalogue.
/// </summary>
public static class OcrLanguagePackDownloader
{
    /// <summary>Determines whether a pack is downloaded and current in a folder.</summary>
    /// <param name="pack">The pack.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <returns><see langword="true"/> when it need not be downloaded.</returns>
    public static bool IsInstalled(OcrLanguagePack pack, string directory)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return VerifiedDownload.IsCurrent(Path.Combine(directory, pack.FileName), pack.Bytes, pack.Sha256);
    }

    /// <summary>Gets the packs not yet downloaded to a folder, or downloaded from another release.</summary>
    /// <param name="packs">The packs.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <returns>The missing packs.</returns>
    public static List<OcrLanguagePack> Missing(IReadOnlyList<OcrLanguagePack> packs, string directory)
    {
        ArgumentNullException.ThrowIfNull(packs);
        var missing = new List<OcrLanguagePack>(packs.Count);
        for (var i = 0; i < packs.Count; i++)
        {
            if (!IsInstalled(packs[i], directory))
            {
                missing.Add(packs[i]);
            }
        }

        return missing;
    }

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

    /// <summary>Deletes a downloaded pack and its recorded hash. Packs installed with Tesseract elsewhere are not touched.</summary>
    /// <param name="pack">The pack.</param>
    /// <param name="directory">The language pack folder.</param>
    public static void Remove(OcrLanguagePack pack, string directory)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var target = VerifiedDownload.TargetIn(directory, pack.FileName);
        File.Delete(target);
        File.Delete(target + VerifiedDownload.HashSuffix);
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
    public static Task DownloadAsync(IReadOnlyList<OcrLanguagePack> packs, string directory, IProgress<double> progress, CancellationToken cancellationToken) =>
        DownloadAsync(RefitClients.Download, packs, directory, progress, cancellationToken);

    /// <summary>Downloads the missing packs through a given client.</summary>
    /// <param name="api">A client whose base address is the packs' host.</param>
    /// <param name="packs">The packs wanted.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download, keeping packs already finished.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails a request.</exception>
    /// <exception cref="InvalidDataException">Thrown when a download does not match its SHA-256.</exception>
    public static Task DownloadAsync(IRemoteDocumentApi api, IReadOnlyList<OcrLanguagePack> packs, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(api);

        // The client's base address is the host, so only the path is sent.
        return DownloadAsync((uri, token) => api.DownloadAsync(uri.PathAndQuery.TrimStart('/'), token), packs, directory, progress, cancellationToken);
    }

    /// <summary>Downloads the missing packs one after another.</summary>
    /// <param name="fetch">Starts the request for an address.</param>
    /// <param name="packs">The packs wanted.</param>
    /// <param name="directory">The language pack folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>A task.</returns>
    private static async Task DownloadAsync(
        Func<Uri, CancellationToken, Task<HttpResponseMessage>> fetch,
        IReadOnlyList<OcrLanguagePack> packs,
        string directory,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var missing = Missing(packs, directory);
        var counter = new ByteProgress(TotalBytes(missing), progress);
        var buffer = ArrayPool<byte>.Shared.Rent(VerifiedDownload.BufferSize);
        try
        {
            foreach (var pack in missing)
            {
                var target = VerifiedDownload.TargetIn(directory, pack.FileName);
                await VerifiedDownload.DownloadAsync(fetch, pack.Source, target, pack.Sha256, buffer, counter, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        counter.Complete();
    }
}
