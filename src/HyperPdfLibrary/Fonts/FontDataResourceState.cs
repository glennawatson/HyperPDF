// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdfLibrary.Fonts.Generation;
using Refit;

namespace HyperPdfLibrary.Fonts;

/// <summary>Owns resource download, availability and generation state for one drawing service.</summary>
[DebuggerDisplay("Font data generation {Generation}")]
internal sealed class FontDataResourceState
{
    /// <summary>The HTTP transport, built on the first remote request.</summary>
    private readonly Lazy<HttpClient> _client = new(static () => new HttpClient
    {
        BaseAddress = new("https://raw.githubusercontent.com"),
        Timeout = TimeSpan.FromSeconds(FontDataDownload.TimeoutSeconds),
    });

    /// <summary>The generated remote resource client, built on the first request.</summary>
    private readonly Lazy<IFontDataApi> _api;

    /// <summary>The number of assets first observed available by this service.</summary>
    private int _generation;

    /// <summary>Initializes a new instance of the <see cref="FontDataResourceState"/> class.</summary>
    internal FontDataResourceState() => _api = new(() => RestService.ForGenerated<IFontDataApi>(_client.Value, FontDataJsonContext.Default));

    /// <summary>Gets the gates for generating requested assets.</summary>
    internal ConcurrentDictionary<string, SemaphoreSlim> Gates { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the availability of requested assets.</summary>
    internal ConcurrentDictionary<string, FontDataAvailability> Availability { get; } = new(StringComparer.Ordinal);

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
}
