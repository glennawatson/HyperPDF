// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Reactive;
using ReactiveUI.Primitives;

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
    /// <returns>
    /// A cold signal giving the path of the downloaded file once; unsubscribing cancels the request or the copy. It fails with
    /// <see cref="HttpRequestException"/> when the server fails the request and with <see cref="InvalidDataException"/> when the document is too large.
    /// </returns>
    public IObservable<string> Download(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return _clients.For(uri).Download(RefitClients.RequestPathOf(uri))

            // Copying the body to disk blocks for as long as the file takes, so it gets a thread of its own.
            .SelectMany(response => DedicatedThread.Run("HyperPdf download", token => Save(response, uri, token)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _clients.Dispose();

    /// <summary>Copies a response to a file in the download folder, on the calling thread.</summary>
    /// <param name="response">The response; it is disposed here.</param>
    /// <param name="uri">The document URI, which names the file.</param>
    /// <param name="token">Stops the copy.</param>
    /// <returns>The path of the file.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server fails the request.</exception>
    /// <exception cref="InvalidDataException">Thrown when the document is too large.</exception>
    private string Save(HttpResponseMessage response, Uri uri, CancellationToken token)
    {
        using (response)
        {
            _ = response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                throw new InvalidDataException("The document is too large to download.");
            }

            _ = Directory.CreateDirectory(_downloadDirectory);
            var name = Path.GetFileName(uri.LocalPath);
            var target = Path.Combine(_downloadDirectory, string.IsNullOrWhiteSpace(name) ? $"{Guid.NewGuid():N}.pdf" : name);
            using var source = response.Content.ReadAsStream(token);
            using var output = File.Create(target);
            var buffer = ArrayPool<byte>.Shared.Rent(VerifiedDownload.BufferSize);
            try
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return target;
        }
    }
}
