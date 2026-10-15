// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Http.GitHub;
using HyperPdf.Http.Remote;
using HyperPdf.Http.Signatures;
using HyperPdf.Http.Speech;
using Refit;

namespace HyperPdf.Http;

/// <summary>
/// Creates Refit clients through the compile time generated implementations and source generated JSON metadata, so no
/// reflection is needed under Native AOT.
/// </summary>
public static class RefitClients
{
    /// <summary>Gets the GitHub API base address.</summary>
    public static Uri GitHubApiAddress { get; } = new("https://api.github.com");

    /// <summary>Creates a GitHub releases client.</summary>
    /// <param name="httpClient">The HTTP client; its base address defaults to the GitHub API.</param>
    /// <returns>The client.</returns>
    public static IGitHubReleasesApi CreateGitHubReleasesApi(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        httpClient.BaseAddress ??= GitHubApiAddress;
        return RestService.ForGenerated<IGitHubReleasesApi>(httpClient, HttpJsonContext.Default);
    }

    /// <summary>Creates a client that downloads documents from the HTTP client's base address.</summary>
    /// <param name="httpClient">The HTTP client with its base address set to the document host.</param>
    /// <returns>The client.</returns>
    public static IRemoteDocumentApi CreateRemoteDocumentApi(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return RestService.ForGenerated<IRemoteDocumentApi>(httpClient, HttpJsonContext.Default);
    }

    /// <summary>Creates an Azure Speech client for a region.</summary>
    /// <param name="httpClient">The HTTP client; its base address defaults to the region's speech endpoint.</param>
    /// <param name="region">The Azure region, for example <c>uksouth</c>.</param>
    /// <returns>The client.</returns>
    public static IAzureSpeechApi CreateAzureSpeechApi(HttpClient httpClient, string region)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        httpClient.BaseAddress ??= AzureSpeechEngine.EndpointFor(region);
        return RestService.ForGenerated<IAzureSpeechApi>(httpClient, HttpJsonContext.Default);
    }

    /// <summary>Creates a client for an RFC 3161 timestamp authority.</summary>
    /// <param name="httpClient">The HTTP client with its base address set to the authority's host.</param>
    /// <returns>The client.</returns>
    public static ITimestampAuthorityApi CreateTimestampAuthorityApi(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return RestService.ForGenerated<ITimestampAuthorityApi>(httpClient, HttpJsonContext.Default);
    }

    /// <summary>Creates a client for a certificate authority's revocation endpoints.</summary>
    /// <param name="httpClient">The HTTP client with its base address set to the authority's host.</param>
    /// <returns>The client.</returns>
    public static IRevocationApi CreateRevocationApi(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return RestService.ForGenerated<IRevocationApi>(httpClient, HttpJsonContext.Default);
    }

    /// <summary>Gets the path and query of an address as the download client takes them, relative to its host.</summary>
    /// <param name="uri">The absolute address.</param>
    /// <returns>The path and query without the leading slash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string RequestPathOf(Uri uri) => uri.PathAndQuery.TrimStart('/');
}
