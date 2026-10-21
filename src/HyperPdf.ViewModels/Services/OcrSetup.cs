// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using HyperPdf.Http.Ocr;
using HyperPdf.Ocr;

namespace HyperPdf.ViewModels.Services;

/// <summary>
/// How the app recognises text: the Tesseract shipped with the app, the English data shipped beside it, where other
/// downloaded languages live, and how they are downloaded.
/// </summary>
/// <param name="LanguageDirectory">The folder holding downloaded language packs.</param>
/// <param name="BundledLanguageDirectory">The folder holding the language data shipped with the app, or <see langword="null"/> when there is none.</param>
/// <param name="IsEngineInstalled">Determines whether the Tesseract library can be loaded.</param>
/// <param name="FindLanguageData">Finds one folder holding every language in a setting such as <c>eng+deu</c>, given the pack folder; <see langword="null"/> when there is none.</param>
/// <param name="CreateEngine">Creates the recogniser for a language setting, given the pack folder.</param>
/// <param name="DownloadPacks">Downloads the missing packs into the given folder, reporting the fraction done.</param>
[DebuggerDisplay("OcrSetup: {LanguageDirectory}")]
public sealed record OcrSetup(
    string LanguageDirectory,
    string? BundledLanguageDirectory,
    Func<bool> IsEngineInstalled,
    Func<string, string, string?> FindLanguageData,
    Func<string, string, IOcrEngine> CreateEngine,
    Func<IReadOnlyList<OcrLanguagePack>, string, IProgress<double>, CancellationToken, Task> DownloadPacks) : IDisposable
{
    /// <summary>The extension of a language data file.</summary>
    private const string DataExtension = ".traineddata";

    /// <summary>Gets the default pack folder, <c>~/.local/share/hyperpdf/tessdata</c> on Linux, beside the voices.</summary>
    public static string DefaultLanguageDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hyperpdf", "tessdata");

    /// <summary>Gets what to say when the text recogniser cannot start. It never asks the person to install anything.</summary>
    public static string EngineUnavailableText =>
        "Text recognition could not start on this computer, so the pages were left as they are. Please report this so it can be fixed.";

    /// <summary>Gets the downloads and file facts owned by this setup.</summary>
    private OcrLanguagePackDownloader Downloads { get; init; } = new();

    /// <summary>Creates the default setup: the Tesseract and English data shipped with the app, with other languages downloaded when needed.</summary>
    /// <returns>The setup.</returns>
    public static OcrSetup CreateDefault()
    {
        var downloads = new OcrLanguagePackDownloader();
        var paths = new DataFilePaths();
        return new(
            DefaultLanguageDirectory,
            TesseractEngine.BundledDataDirectory,
            TesseractEngine.IsLibraryAvailable,
            paths.FindDirectory,
            static (languages, directory) => new TesseractEngine(languages, directory),
            downloads.DownloadAsync) { Downloads = downloads, };
    }

    /// <summary>Determines whether a pack is downloaded into the pack folder.</summary>
    /// <param name="pack">The pack.</param>
    /// <returns><see langword="true"/> when it is downloaded and current.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDownloaded(OcrLanguagePack pack) => Downloads.IsInstalled(pack, LanguageDirectory);

    /// <summary>Determines whether a language ships with the app.</summary>
    /// <param name="code">The language code, for example <c>eng</c>.</param>
    /// <returns><see langword="true"/> when its data is shipped.</returns>
    public bool IsBundled(string code) =>
        BundledLanguageDirectory is { } bundled && File.Exists(Path.Combine(bundled, code + DataExtension));

    /// <summary>Determines whether a language can be recognised, from a downloaded pack, the app, or the system.</summary>
    /// <param name="code">The language code, for example <c>deu</c>.</param>
    /// <returns><see langword="true"/> when its data was found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasLanguage(string code) => FindLanguageData(code, LanguageDirectory) is not null;

    /// <summary>Deletes a downloaded pack.</summary>
    /// <param name="pack">The pack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(OcrLanguagePack pack) => Downloads.Remove(pack, LanguageDirectory);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Downloads.Dispose();

    /// <summary>Gets the packs to download before a language setting can be recognised.</summary>
    /// <param name="codes">The language codes.</param>
    /// <param name="unknown">Receives codes that are neither on this computer nor offered for download.</param>
    /// <returns>The packs to download; empty when every language is on this computer.</returns>
    public List<OcrLanguagePack> PacksNeededFor(IReadOnlyList<string> codes, List<string> unknown)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(unknown);
        var needed = new List<OcrLanguagePack>();
        if (FindLanguageData(OcrLanguageCatalog.Format(codes), LanguageDirectory) is not null)
        {
            return needed;
        }

        foreach (var code in codes)
        {
            if (HasLanguage(code))
            {
                continue;
            }

            if (OcrLanguageCatalog.Find(code) is { } pack)
            {
                needed.Add(pack);
            }
            else
            {
                unknown.Add(code);
            }
        }

        return needed;
    }

    /// <summary>
    /// Puts every chosen language in the pack folder when they are spread over several folders, for example English
    /// shipped with the app and German downloaded, because Tesseract reads all of a run's languages from one folder.
    /// Files are copied on this computer; nothing is downloaded.
    /// </summary>
    /// <param name="codes">The language codes, each already on this computer.</param>
    /// <exception cref="IOException">Thrown when a file cannot be copied.</exception>
    public void GatherLanguages(IReadOnlyList<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        if (FindLanguageData(OcrLanguageCatalog.Format(codes), LanguageDirectory) is not null)
        {
            return;
        }

        _ = Directory.CreateDirectory(LanguageDirectory);
        foreach (var code in codes)
        {
            if (FindLanguageData(code, LanguageDirectory) is not { } folder || string.Equals(folder, LanguageDirectory, StringComparison.Ordinal))
            {
                continue;
            }

            var name = code + DataExtension;
            File.Copy(Path.Combine(folder, name), Path.Combine(LanguageDirectory, name), true);
        }
    }

    /// <summary>Downloads packs into the pack folder.</summary>
    /// <param name="packs">The packs.</param>
    /// <param name="progress">Receives the fraction done.</param>
    /// <param name="cancellationToken">Cancels the download, keeping packs already finished.</param>
    /// <returns>A task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DownloadAsync(IReadOnlyList<OcrLanguagePack> packs, IProgress<double> progress, CancellationToken cancellationToken) =>
        DownloadPacks(packs, LanguageDirectory, progress, cancellationToken);
}
