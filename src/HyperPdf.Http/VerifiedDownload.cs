// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdf.Core.Reactive;
using HyperPdf.Http.Remote;
using ReactiveUI.Primitives;

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
    /// <param name="hashes">The hash contexts owned by the download service.</param>
    /// <param name="progress">Counts the bytes received.</param>
    /// <returns>
    /// A cold signal giving the bytes received once. Unsubscribing cancels the request or the copy and deletes the partial file;
    /// the signal fails with <see cref="HttpRequestException"/> when the server fails the request and with
    /// <see cref="InvalidDataException"/> when the download does not match its SHA-256.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<long> Download(IRemoteDocumentApi api, string requestPath, DownloadFile file, string sha256, DownloadHashes hashes, DownloadProgress progress) =>
        api.Download(requestPath)

            // Copying the body to disk blocks on the network and the disk for as long as the file takes, so it gets a thread of its own.
            .SelectMany(message => DedicatedThread.Run("HyperPdf download", token => Receive(message, file, sha256, hashes, progress, token)));

    /// <summary>Copies a response to disk, checking its hash, on the calling thread.</summary>
    /// <param name="message">The response; it is disposed here.</param>
    /// <param name="file">Where the file is kept.</param>
    /// <param name="sha256">Its expected SHA-256 as lowercase hexadecimal.</param>
    /// <param name="hashes">The hash contexts owned by the download service.</param>
    /// <param name="progress">Counts the bytes received.</param>
    /// <param name="token">Stops the copy, deleting the partial file.</param>
    /// <returns>The bytes received.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails the request.</exception>
    /// <exception cref="InvalidDataException">Thrown when the download does not match its SHA-256.</exception>
    private static long Receive(HttpResponseMessage message, DownloadFile file, string sha256, DownloadHashes hashes, DownloadProgress progress, CancellationToken token)
    {
        using (message)
        {
            _ = message.EnsureSuccessStatusCode();
            if (!Directory.Exists(file.Folder))
            {
                _ = Directory.CreateDirectory(file.Folder);
            }

            string? partial = null;
            var received = 0L;
            var moved = false;
            var hash = hashes.Take();
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                partial = file.TakePartialPath();
                using (var input = message.Content.ReadAsStream(token))
                using (var output = File.OpenHandle(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        RandomAccess.Write(output, buffer.AsSpan(0, read), received);
                        hash.AppendData(buffer, 0, read);
                        received += read;
                        progress.Report(received);
                    }
                }

                if (!HashMatches(hash, sha256))
                {
                    throw new InvalidDataException($"{Path.GetFileName(file.Target)} did not download intact; try again.");
                }

                File.Move(partial, file.Target, true);
                moved = true;
                using var recorded = File.OpenHandle(file.HashTarget, FileMode.Create, FileAccess.Write);
                RandomAccess.Write(recorded, file.EncodedHash(sha256).Span, 0);
                return received;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                ReleaseDownload(file, partial, moved, hashes, hash);
            }
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
