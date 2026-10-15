// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Http.Remote;

namespace HyperPdf.Http;

/// <summary>
/// Downloads one file whose size and SHA-256 are known. The file is written to its own <c>.part</c> file and moved into
/// place once complete and checked, so an interrupted download is never mistaken for a finished one and two downloads at
/// once do not collide. The hash is recorded beside the file so a later release that changes it is noticed.
/// </summary>
internal static class VerifiedDownload
{
    /// <summary>The copy buffer size.</summary>
    internal const int BufferSize = 81_920;

    /// <summary>The bytes in a SHA-256 hash.</summary>
    private const int HashBytes = 32;

    /// <summary>The characters in a SHA-256 hash written as hexadecimal.</summary>
    private const int HashChars = HashBytes * 2;

    /// <summary>Downloads a file, checking its SHA-256 before it is kept.</summary>
    /// <param name="api">The client for the file's host.</param>
    /// <param name="requestPath">The file's path and query relative to the client's base address.</param>
    /// <param name="file">Where the file is kept.</param>
    /// <param name="sha256">Its expected SHA-256 as lowercase hexadecimal.</param>
    /// <param name="copy">The reusable copy and hash resources.</param>
    /// <param name="progress">Counts the bytes received.</param>
    /// <param name="cancellationToken">Cancels the download, deleting the partial file.</param>
    /// <returns>The bytes received.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails the request.</exception>
    /// <exception cref="InvalidDataException">Thrown when the download does not match its SHA-256.</exception>
    internal static async Task<long> DownloadAsync(
        IRemoteDocumentApi api,
        string requestPath,
        DownloadFile file,
        string sha256,
        DownloadCopy copy,
        DownloadProgress progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(file.Folder))
        {
            _ = Directory.CreateDirectory(file.Folder);
        }

        string? partial = null;
        var received = 0L;
        var moved = false;
        var hash = copy.Hashes.Take();
        try
        {
            partial = file.TakePartialPath();
            using (var message = await api.DownloadAsync(requestPath, cancellationToken).ConfigureAwait(false))
            {
                _ = message.EnsureSuccessStatusCode();
                var input = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (input.ConfigureAwait(false))
                {
                    using var output = File.OpenHandle(partial, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous);
                    int read;
                    while ((read = await input.ReadAsync(copy.Buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await RandomAccess.WriteAsync(output, copy.Buffer.AsMemory(0, read), received, cancellationToken).ConfigureAwait(false);
                        hash.AppendData(copy.Buffer, 0, read);
                        received += read;
                        progress.Report(received);
                    }
                }
            }

            if (!HashMatches(hash, sha256))
            {
                throw new InvalidDataException($"{Path.GetFileName(file.Target)} did not download intact; try again.");
            }

            File.Move(partial, file.Target, true);
            moved = true;
            using var recorded = File.OpenHandle(file.HashTarget, FileMode.Create, FileAccess.Write, FileShare.Read, FileOptions.Asynchronous);
            await RandomAccess.WriteAsync(recorded, file.EncodedHash(sha256), 0, CancellationToken.None).ConfigureAwait(false);
            return received;
        }
        finally
        {
            ReleaseDownload(file, partial, moved, copy.Hashes, hash);
        }
    }

    /// <summary>Removes unfinished output and releases its path and hash context even when deletion fails.</summary>
    /// <param name="file">The download's file state.</param>
    /// <param name="partial">The partial path.</param>
    /// <param name="moved">Whether the file was already moved into place.</param>
    /// <param name="hashes">The service's hash owner.</param>
    /// <param name="hash">The context used by the operation.</param>
    private static void ReleaseDownload(DownloadFile file, string? partial, bool moved, DownloadHashes hashes, IncrementalHash hash)
    {
        try
        {
            try
            {
                if (partial is not null && !moved)
                {
                    File.Delete(partial);
                }
            }
            finally
            {
                if (partial is not null)
                {
                    file.ReturnPartialPath(partial);
                }
            }
        }
        finally
        {
            hashes.Return(hash);
        }
    }

    /// <summary>Determines whether a finished hash is the expected one, without making a string of it.</summary>
    /// <param name="hash">The hash, which is reset.</param>
    /// <param name="sha256">The expected SHA-256 as lowercase hexadecimal.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool HashMatches(IncrementalHash hash, string sha256)
    {
        Span<byte> digest = stackalloc byte[HashBytes];
        Span<char> hex = stackalloc char[HashChars];
        var written = hash.GetHashAndReset(digest);
        return Convert.TryToHexStringLower(digest[..written], hex, out var chars) && hex[..chars].SequenceEqual(sha256);
    }
}
