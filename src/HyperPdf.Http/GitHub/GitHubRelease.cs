// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace HyperPdf.Http.GitHub;

/// <summary>The subset of a GitHub release used for update checks.</summary>
[DebuggerDisplay("GitHubRelease: {TagName}")]
public sealed record GitHubRelease
{
    /// <summary>Gets the tag, for example <c>v1.2.0</c>.</summary>
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    /// <summary>Gets the release title.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Gets the release page.</summary>
    [JsonPropertyName("html_url")]
    public Uri? HtmlUrl { get; init; }

    /// <summary>Gets a value indicating whether the release is a pre-release.</summary>
    [JsonPropertyName("prerelease")]
    public bool IsPrerelease { get; init; }

    /// <summary>Gets when the release was published.</summary>
    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }
}
