// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Optimizing;
using HyperPdf.Http.Speech;
using HyperPdf.Vision;

namespace HyperPdf.ViewModels.Services;

/// <summary>How the app describes pictures: where the on-device model lives, how it is made, and how it is downloaded.</summary>
/// <param name="ModelDirectory">The folder holding the model files.</param>
/// <param name="MissingBytes">Gets about how many bytes of the model are still to download; zero when it is installed.</param>
/// <param name="CreateDescriber">Creates the describer, which the caller disposes when it is <see cref="IDisposable"/>.</param>
/// <param name="Download">Downloads the missing files into the model folder, reporting the fraction done.</param>
[DebuggerDisplay("PictureDescriptionSetup: {ModelDirectory}")]
public sealed record PictureDescriptionSetup(
    string ModelDirectory,
    Func<long> MissingBytes,
    Func<IFigureDescriber> CreateDescriber,
    Func<IProgress<double>, CancellationToken, Task> Download) : IDisposable
{
    /// <summary>Gets the default model folder, <c>~/.local/share/hyperpdf/vision</c> on Linux, beside the voices.</summary>
    public static string DefaultModelDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hyperpdf", "vision");

    /// <summary>Gets the downloads and file facts owned by this setup.</summary>
    private SpeechModelDownloader Downloads { get; init; } = new();

    /// <summary>Creates the default setup: the distilvit model on this computer, downloaded once.</summary>
    /// <returns>The setup.</returns>
    public static PictureDescriptionSetup CreateDefault()
    {
        var downloads = new SpeechModelDownloader();
        return new(
            DefaultModelDirectory,
            () => SpeechModelDownloader.TotalBytes(downloads.Missing(DistilVitModel.Files, DefaultModelDirectory)),
            static () => new DistilVitCaptioner(DefaultModelDirectory),
            (progress, cancellationToken) => downloads.DownloadAsync(DistilVitModel.Files, DefaultModelDirectory, progress, cancellationToken)) { Downloads = downloads, };
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Downloads.Dispose();
}
