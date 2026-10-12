// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdf.Core.Speech;

namespace HyperPdf.Http.Speech;

/// <summary>
/// Downloads an on-device voice's files with Refit. Each file is written to its own <c>.part</c> file and moved into
/// place once complete, so an interrupted download is never mistaken for a finished one and two downloads at once do
/// not collide. Each download is checked against its SHA-256 before it is kept, and the hash is recorded beside it;
/// a file already present is kept while its size and recorded hash still match the release.
/// </summary>
public static class SpeechModelDownloader
{
    /// <summary>Gets the files not yet downloaded, or downloaded from an older voice release.</summary>
    /// <param name="files">The files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <returns>The missing files.</returns>
    public static List<SpeechModelFile> Missing(IReadOnlyList<SpeechModelFile> files, string directory)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        var missing = new List<SpeechModelFile>(files.Count);
        foreach (var file in files)
        {
            if (!VerifiedDownload.IsCurrent(Path.Combine(directory, file.LocalName), file.Bytes, file.Sha256))
            {
                missing.Add(file);
            }
        }

        return missing;
    }

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

    /// <summary>Downloads the missing files.</summary>
    /// <param name="files">The files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download, leaving finished files in place.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when a server fails a request.</exception>
    /// <exception cref="InvalidDataException">Thrown when a file's name would place it outside the voice folder, or a download does not match its SHA-256.</exception>
    public static async Task DownloadAsync(IReadOnlyList<SpeechModelFile> files, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var missing = Missing(files, directory);
        var counter = new ByteProgress(TotalBytes(missing), progress);
        var buffer = ArrayPool<byte>.Shared.Rent(VerifiedDownload.BufferSize);
        try
        {
            foreach (var file in missing)
            {
                var target = VerifiedDownload.TargetIn(directory, file.LocalName);
                await VerifiedDownload.DownloadAsync(RefitClients.Download, file.Source, target, file.Sha256, buffer, counter, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        counter.Complete();
    }
}
