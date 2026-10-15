// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Http.Remote;

namespace HyperPdf.Http;

/// <summary>Owns the HTTP clients used by one download service.</summary>
internal sealed class DownloadClients : IDisposable
{
    /// <summary>Serializes client creation and disposal.</summary>
    private readonly Lock _gate = new();

    /// <summary>The clients for the hosts this service uses.</summary>
    private readonly Dictionary<string, DownloadClient> _clients = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Whether the owner has ended this service's lifetime.</summary>
    private bool _disposed;

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var client in _clients.Values)
            {
                client.Http.Dispose();
            }

            _clients.Clear();
        }
    }

    /// <summary>Gets the client for an address's host.</summary>
    /// <param name="uri">The absolute address.</param>
    /// <returns>The client.</returns>
    internal IRemoteDocumentApi For(Uri uri)
    {
        var authority = uri.GetLeftPart(UriPartial.Authority);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_clients.TryGetValue(authority, out var found))
            {
                return found.Api;
            }

            var http = new HttpClient { BaseAddress = new(authority) };
            try
            {
                var client = new DownloadClient(http, RefitClients.CreateRemoteDocumentApi(http));
                _clients.Add(authority, client);
                return client.Api;
            }
            catch
            {
                http.Dispose();
                throw;
            }
        }
    }

    /// <summary>A generated client and the HTTP client it uses.</summary>
    /// <param name="Http">The owned HTTP client.</param>
    /// <param name="Api">The generated API.</param>
    private readonly record struct DownloadClient(HttpClient Http, IRemoteDocumentApi Api);
}
