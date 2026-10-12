// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Http.Speech;
using HyperPdf.Speech.Kokoro;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>
/// Finds the real Kokoro voice files for the integration tests: in <c>HYPERPDF_KOKORO_DIR</c>, or the user's cache
/// folder, downloading them there with the app's own downloader when <c>HYPERPDF_KOKORO_DOWNLOAD=1</c> (as CI does).
/// </summary>
internal static class KokoroModelFixture
{
    /// <summary>The variable naming a folder that already holds the files.</summary>
    private const string DirectoryVariable = "HYPERPDF_KOKORO_DIR";

    /// <summary>The variable allowing a download.</summary>
    private const string DownloadVariable = "HYPERPDF_KOKORO_DOWNLOAD";

    /// <summary>Serialises the download.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Gets the folder used.</summary>
    internal static string Directory { get; } = Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } configured
        ? configured
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "hyperpdf", "kokoro");

    /// <summary>Makes sure the files are present, downloading them when allowed.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">The files are missing and downloading is not allowed.</exception>
    internal static async Task EnsureAsync()
    {
        if (SpeechModelDownloader.Missing(KokoroModel.Files, Directory).Count == 0)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable(DownloadVariable) != "1")
        {
            throw new TUnit.Core.Exceptions.SkipTestException($"The Kokoro voice files are not in {Directory}; set {DownloadVariable}=1 to download them.");
        }

        await Gate.WaitAsync();
        try
        {
            await SpeechModelDownloader.DownloadAsync(KokoroModel.Files, Directory, new Progress<double>(), CancellationToken.None);
        }
        finally
        {
            _ = Gate.Release();
        }
    }
}
