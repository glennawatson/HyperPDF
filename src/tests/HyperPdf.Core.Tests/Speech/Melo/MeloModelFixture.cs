// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Http.Speech;
using HyperPdf.Speech.Melo;

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>
/// Finds the real MeloTTS voice files for the integration tests: in <c>HYPERPDF_MELO_DIR</c>, or the user's cache
/// folder, downloading them there with the app's own downloader when <c>HYPERPDF_MELO_DOWNLOAD=1</c> (as CI does).
/// </summary>
internal static class MeloModelFixture
{
    /// <summary>The variable naming a folder that already holds the files.</summary>
    private const string DirectoryVariable = "HYPERPDF_MELO_DIR";

    /// <summary>The variable allowing a download.</summary>
    private const string DownloadVariable = "HYPERPDF_MELO_DOWNLOAD";

    /// <summary>Serialises the download.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Gets the folder used.</summary>
    internal static string Directory { get; } = Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } configured
        ? configured
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "hyperpdf", "melo");

    /// <summary>Makes sure the files are present, downloading them when allowed.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">The files are missing and downloading is not allowed.</exception>
    internal static async Task EnsureAsync()
    {
        using var downloader = new SpeechModelDownloader();
        if (downloader.Missing(MeloModel.Files, Directory).Count == 0)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable(DownloadVariable) != "1")
        {
            throw new TUnit.Core.Exceptions.SkipTestException($"The MeloTTS voice files are not in {Directory}; set {DownloadVariable}=1 to download them.");
        }

        await Gate.WaitAsync();
        try
        {
            await downloader.DownloadAsync(MeloModel.Files, Directory, new Progress<double>(), CancellationToken.None);
        }
        finally
        {
            _ = Gate.Release();
        }
    }
}
