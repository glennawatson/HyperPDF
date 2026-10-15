// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Http.Remote;

/// <summary>Downloads a document from an <c>http</c> or <c>https</c> URI into a temporary file.</summary>
[DebuggerDisplay("RemoteDocumentDownloader: {_downloadDirectory}")]
public sealed class RemoteDocumentDownloader : IDisposable
{
    /// <summary>The largest document downloaded, guarding against runaway responses.</summary>
    private const long MaxBytes = 512L * 1024 * 1024;

    /// <summary>The directory downloads are written to.</summary>
    private readonly string _downloadDirectory;

    /// <summary>The HTTP clients owned by this downloader.</summary>
    private readonly DownloadClients _clients = new();

    /// <summary>Initializes a new instance of the <see cref="RemoteDocumentDownloader"/> class.</summary>
    /// <param name="downloadDirectory">The directory downloads are written to.</param>
    public RemoteDocumentDownloader(string downloadDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(downloadDirectory);
        _downloadDirectory = downloadDirectory;
    }

    /// <summary>Determines whether a string is a downloadable URI.</summary>
    /// <param name="value">The value.</param>
    /// <param name="uri">The URI.</param>
    /// <returns><see langword="true"/> for absolute http and https URIs.</returns>
    public static bool IsRemote(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    /// <summary>Downloads a document.</summary>
    /// <param name="uri">The document URI.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The path of the downloaded file.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails the request.</exception>
    /// <exception cref="InvalidDataException">Thrown when the document is too large.</exception>
    public async Task<string> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        using var response = await _clients.For(uri).DownloadAsync(RefitClients.RequestPathOf(uri), cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new InvalidDataException("The document is too large to download.");
        }

        _ = Directory.CreateDirectory(_downloadDirectory);
        var name = Path.GetFileName(uri.LocalPath);
        var target = Path.Combine(_downloadDirectory, string.IsNullOrWhiteSpace(name) ? $"{Guid.NewGuid():N}.pdf" : name);
        var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var output = File.Create(target);
            await using (output.ConfigureAwait(false))
            {
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
        }

        return target;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _clients.Dispose();
}
