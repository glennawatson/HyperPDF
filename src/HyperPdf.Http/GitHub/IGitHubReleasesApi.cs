// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace HyperPdf.Http.GitHub;

/// <summary>The GitHub releases REST API.</summary>
[Headers("Accept: application/vnd.github+json", "User-Agent: HyperPDF")]
public interface IGitHubReleasesApi
{
    /// <summary>Gets the latest published release.</summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The release.</returns>
    [Get("/repos/{owner}/{repository}/releases/latest")]
    Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repository, CancellationToken cancellationToken);
}
