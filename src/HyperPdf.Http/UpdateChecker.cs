// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Http.GitHub;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Http;

/// <summary>Checks GitHub for a newer release.</summary>
[DebuggerDisplay("UpdateChecker: {_owner}/{_repository}")]
public sealed class UpdateChecker
{
    /// <summary>The API client.</summary>
    private readonly IGitHubReleasesApi _api;

    /// <summary>The repository owner.</summary>
    private readonly string _owner;

    /// <summary>The repository name.</summary>
    private readonly string _repository;

    /// <summary>Initializes a new instance of the <see cref="UpdateChecker"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    public UpdateChecker(IGitHubReleasesApi api, string owner, string repository)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(repository);
        _api = api;
        _owner = owner;
        _repository = repository;
    }

    /// <summary>Parses a release tag such as <c>v1.2.3</c> into a version.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> when parsed.</returns>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var text = tag.AsSpan().Trim().TrimStart('v').TrimStart('V');
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            text = text[..dash];
        }

        if (!Version.TryParse(text, out var parsed))
        {
            return false;
        }

        version = parsed;
        return true;
    }

    /// <summary>Gets the latest release when it is newer than <paramref name="current"/>.</summary>
    /// <param name="current">The running version.</param>
    /// <returns>A cold signal giving the newer release once, or <see langword="null"/> when there is none or GitHub could not be reached; unsubscribing cancels the check.</returns>
    public IObservable<GitHubRelease?> GetNewerRelease(Version current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return _api.GetLatestRelease(_owner, _repository)
            .Select(release => !release.IsPrerelease && TryParseTag(release.TagName, out var latest) && latest > current ? release : null)
            .Catch<GitHubRelease?, Exception>(static error => error is HttpRequestException or Refit.ApiException
                ? Signal.Return<GitHubRelease?>(null)
                : Signal.Throw<GitHubRelease?>(error));
    }
}
