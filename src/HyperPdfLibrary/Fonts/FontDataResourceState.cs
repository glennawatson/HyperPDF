// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Generation;
using Refit;

namespace HyperPdfLibrary.Fonts;

/// <summary>Owns resource download, availability and generation state for one drawing service.</summary>
[DebuggerDisplay("Font data generation {Generation}, {Mode}")]
internal sealed class FontDataResourceState
{
    /// <summary>The HTTP transport, built on the first remote request.</summary>
    private readonly Lazy<HttpClient> _client;

    /// <summary>The generated remote resource client, built on the first request.</summary>
    private readonly Lazy<IFontDataApi> _api;

    /// <summary>The number of assets first observed available by this service.</summary>
    private int _generation;

    /// <summary>Initializes a new instance of the <see cref="FontDataResourceState"/> class that may download.</summary>
    internal FontDataResourceState()
        : this(PdfFontDataMode.OnDemand, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontDataResourceState"/> class.</summary>
    /// <param name="mode">Whether missing data may be downloaded.</param>
    /// <param name="handler">The transport to download with, or <see langword="null"/> for the default; the state does not own it.</param>
    internal FontDataResourceState(PdfFontDataMode mode, HttpMessageHandler? handler)
    {
        Mode = mode;
        _client = new(() => CreateClient(handler));
        _api = new(() => RestService.ForGenerated<IFontDataApi>(_client.Value, FontDataJsonContext.Default));
    }

    /// <summary>Gets whether missing data may be downloaded.</summary>
    internal PdfFontDataMode Mode { get; }

    /// <summary>Gets the gates for generating requested assets.</summary>
    internal ConcurrentDictionary<string, SemaphoreSlim> Gates { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the availability of requested assets.</summary>
    internal ConcurrentDictionary<string, FontDataAvailability> Availability { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the assets an offline service needed but did not have, by group and file name.</summary>
    internal ConcurrentDictionary<string, bool> Missing { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the path and missing-list key of each requested asset, built once per asset.</summary>
    internal ConcurrentDictionary<FontAssetKey, FontAssetPaths> Paths { get; } = new();

    /// <summary>Gets the generated remote resource client.</summary>
    internal IFontDataApi Api => _api.Value;

    /// <summary>Gets the resource generation for refreshing document caches.</summary>
    internal int Generation => Volatile.Read(ref _generation);

    /// <summary>Advances generation after an asset first becomes available.</summary>
    internal void Advance() => _ = Interlocked.Increment(ref _generation);

    /// <summary>Closes an unused download transport after a direct detached font load.</summary>
    internal void CloseDownloadClient()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }
    }

    /// <summary>Creates the client that reads the pinned resource packs.</summary>
    /// <param name="handler">The transport the caller owns, or <see langword="null"/> for a transport the client owns.</param>
    /// <returns>The client.</returns>
    private static HttpClient CreateClient(HttpMessageHandler? handler)
    {
        HttpClient client = handler is null ? new() : new(handler, false);
        client.BaseAddress = new("https://raw.githubusercontent.com");
        client.Timeout = TimeSpan.FromSeconds(FontDataDownload.TimeoutSeconds);
        return client;
    }
}
