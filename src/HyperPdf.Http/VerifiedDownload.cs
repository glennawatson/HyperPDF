// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

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

    /// <summary>The suffix of the file beside each download that records its SHA-256.</summary>
    internal const string HashSuffix = ".sha256";

    /// <summary>
    /// Determines whether a downloaded file is the one wanted: it exists at the expected size and, when its download
    /// recorded a hash beside it, that hash matches. A new release changes the size or hash of any file it replaces, so
    /// updated files are downloaded again without hashing large files at every start.
    /// </summary>
    /// <param name="path">Where the file is kept.</param>
    /// <param name="bytes">Its expected size.</param>
    /// <param name="sha256">Its expected SHA-256 as lowercase hexadecimal.</param>
    /// <returns><see langword="true"/> when it need not be downloaded.</returns>
    internal static bool IsCurrent(string path, long bytes, string sha256)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != bytes)
        {
            return false;
        }

        var recorded = path + HashSuffix;
        return !File.Exists(recorded) || string.Equals(File.ReadAllText(recorded).Trim(), sha256, StringComparison.Ordinal);
    }

    /// <summary>Gets a file's path inside a folder, refusing names that would place it outside.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="localName">The file's path inside the folder.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="InvalidDataException">Thrown when the name leaves the folder.</exception>
    internal static string TargetIn(string directory, string localName)
    {
        var root = Path.GetFullPath(directory);
        var target = Path.GetFullPath(Path.Combine(root, localName));
        if (!target.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{localName} is outside the download folder.");
        }

        return target;
    }

    /// <summary>Downloads a file to a target path, checking its SHA-256 before it is kept.</summary>
    /// <param name="fetch">Starts the request for an address; the response is disposed here.</param>
    /// <param name="source">The file's address.</param>
    /// <param name="target">Where the file is kept.</param>
    /// <param name="sha256">Its expected SHA-256 as lowercase hexadecimal.</param>
    /// <param name="buffer">A reusable copy buffer.</param>
    /// <param name="progress">Counts the bytes received.</param>
    /// <param name="cancellationToken">Cancels the download, deleting the partial file.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails the request.</exception>
    /// <exception cref="InvalidDataException">Thrown when the download does not match its SHA-256.</exception>
    internal static async Task DownloadAsync(
        Func<Uri, CancellationToken, Task<HttpResponseMessage>> fetch,
        Uri source,
        string target,
        string sha256,
        byte[] buffer,
        ByteProgress progress,
        CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var partial = $"{target}.{Guid.NewGuid():N}.part";
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var message = await fetch(source, cancellationToken).ConfigureAwait(false))
            {
                _ = message.EnsureSuccessStatusCode();
                var input = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (input.ConfigureAwait(false))
                {
                    var output = File.Create(partial);
                    await using (output.ConfigureAwait(false))
                    {
                        int read;
                        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            hash.AppendData(buffer, 0, read);
                            progress.Add(read);
                        }
                    }
                }
            }

            if (!string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{Path.GetFileName(target)} did not download intact; try again.");
            }

            File.Move(partial, target, true);
            await File.WriteAllTextAsync(target + HashSuffix, sha256, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(partial);
        }
    }
}
