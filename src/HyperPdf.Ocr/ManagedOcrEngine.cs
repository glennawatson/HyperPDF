// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using HyperPdf.Ocr.Layout;
using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr;

/// <summary>
/// Recognises text in managed code, with the recurrent networks stored in Tesseract language data files. When the
/// language data is missing or unusable the engine reports <see cref="IsAvailable"/> as <see langword="false"/> and
/// recognises nothing.
/// </summary>
[DebuggerDisplay("ManagedOcrEngine: {Language} available={IsAvailable}")]
public sealed class ManagedOcrEngine : IOcrEngine
{
    /// <summary>Words recognised with less confidence than this are noise, not text.</summary>
    private const float MinimumConfidence = 20;

    /// <summary>The environment variable the language data folder can be named in.</summary>
    private const string DataVariable = "TESSDATA_PREFIX";

    /// <summary>The folder name the language data is kept in.</summary>
    private const string DataFolder = "tessdata";

    /// <summary>The extension of a language data file.</summary>
    private const string DataExtension = ".traineddata";

    /// <summary>Where distributions and installers put the language data, on this kind of computer.</summary>
    private static readonly string[] DataDirectories = FullyQualifiedDirectories(
    [
        "/usr/share/tesseract-ocr/5/tessdata",
        "/usr/share/tesseract/tessdata",
        "/usr/share/tessdata",
        "/usr/share/tesseract-ocr/4.00/tessdata",
        "/usr/local/share/tessdata",
        "/opt/homebrew/share/tessdata",
        @"C:\Program Files\Tesseract-OCR\tessdata",
    ]);

    /// <summary>The models, one for each language.</summary>
    private readonly List<LstmModel> _models = [];

    /// <summary>The working memory, kept between pages so reading a page does not allocate.</summary>
    private readonly RecognitionScratch _scratch = new();

    /// <summary>The words found on the page being read, before the unsure ones are dropped.</summary>
    private readonly List<OcrWord> _found = [];

    /// <summary>Initializes a new instance of the <see cref="ManagedOcrEngine"/> class, using installed language data.</summary>
    /// <param name="language">The language, for example <c>eng</c> or <c>eng+deu</c>.</param>
    public ManagedOcrEngine(string language)
        : this(language, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ManagedOcrEngine"/> class.</summary>
    /// <param name="language">The language, for example <c>eng</c> or <c>eng+deu</c>.</param>
    /// <param name="packDirectory">The folder of downloaded language packs, searched first; <see langword="null"/> for none.</param>
    public ManagedOcrEngine(string language, string? packDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Language = language;
        if (FindDataDirectory(language, packDirectory) is not { } directory)
        {
            Status = OcrEngineStatus.LanguageMissing;
            return;
        }

        DataDirectory = directory;
        Status = TryLoad(directory, language) ? OcrEngineStatus.Ready : OcrEngineStatus.StartFailed;
    }

    /// <summary>Gets the folder holding the language data shipped with the app, or <see langword="null"/> when there is none.</summary>
    public static string? BundledDataDirectory { get; } = FindBundledDataDirectory();

    /// <summary>Gets the language the engine recognises.</summary>
    public string Language { get; }

    /// <summary>Gets the folder the language data was read from, or <see langword="null"/> when none was found.</summary>
    public string? DataDirectory { get; }

    /// <inheritdoc/>
    public bool IsAvailable => _models.Count > 0;

    /// <inheritdoc/>
    public OcrEngineStatus Status { get; }

    /// <summary>Finds the directory holding the language data for every language in a language string.</summary>
    /// <param name="language">The language, for example <c>eng+deu</c>.</param>
    /// <returns>The directory, or <see langword="null"/> when the data is not installed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? FindDataDirectory(string language) => FindDataDirectory(language, null);

    /// <summary>
    /// Finds one directory holding the language data for every language in a language string. Downloaded packs come
    /// first, then the data shipped with the app, then any the person or their system installed.
    /// </summary>
    /// <param name="language">The language, for example <c>eng+deu</c>.</param>
    /// <param name="packDirectory">The folder of downloaded language packs, or <see langword="null"/>.</param>
    /// <returns>The directory, or <see langword="null"/> when the data is not installed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? FindDataDirectory(string language, string? packDirectory) =>
        FindDataDirectory(language, packDirectory, new());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _models.Clear();

    /// <inheritdoc/>
    public void Recognize(ReadOnlySpan<byte> image, int width, int height, float pixelsPerPoint, List<OcrWord> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerPoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(image.Length, width * height);
        if (_models.Count == 0)
        {
            return;
        }

        _found.Clear();
        PageReader.Read(_models, image, width, height, new(pixelsPerPoint, _found, _scratch));
        foreach (var word in _found)
        {
            if (word.Confidence >= MinimumConfidence)
            {
                output.Add(word);
            }
        }
    }

    /// <summary>Finds installed language data using paths held by the caller.</summary>
    /// <param name="language">The languages separated by a plus sign.</param>
    /// <param name="packDirectory">The downloaded pack folder, or null.</param>
    /// <param name="paths">The caller's path cache.</param>
    /// <returns>The folder, or null when none holds every language.</returns>
    internal static string? FindDataDirectory(string language, string? packDirectory, DataFilePaths paths)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (!string.IsNullOrEmpty(packDirectory) && HasLanguages(packDirectory, language, paths))
        {
            return packDirectory;
        }

        if (BundledDataDirectory is { } bundled && HasLanguages(bundled, language, paths))
        {
            return bundled;
        }

        var configured = Environment.GetEnvironmentVariable(DataVariable);
        if (!string.IsNullOrEmpty(configured) && HasLanguages(configured, language, paths))
        {
            return configured;
        }

        foreach (var directory in DataDirectories)
        {
            if (HasLanguages(directory, language, paths))
            {
                return directory;
            }
        }

        return null;
    }

    /// <summary>
    /// Keeps the folders that are complete paths on this kind of computer. A Windows path on Linux or macOS is a name
    /// relative to the current folder, so checking it would build a full path from the current folder on every check.
    /// </summary>
    /// <param name="candidates">The folders.</param>
    /// <returns>The ones that are complete paths here.</returns>
    private static string[] FullyQualifiedDirectories(string[] candidates)
    {
        var kept = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (Path.IsPathFullyQualified(candidate))
            {
                kept.Add(candidate);
            }
        }

        return [.. kept];
    }

    /// <summary>Determines whether a directory holds the data for every language in a language string.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="language">The languages separated by <c>+</c>.</param>
    /// <param name="paths">The caller's cached paths.</param>
    /// <returns><see langword="true"/> when all are present.</returns>
    private static bool HasLanguages(string directory, string language, DataFilePaths paths)
    {
        foreach (var range in language.AsSpan().Split('+'))
        {
            // File.Exists takes a string, so each path is built once and kept.
            if (!File.Exists(paths.Of(directory, language.AsSpan()[range])))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the language data shipped with the app: beside it, or in the bundle's resources on macOS.</summary>
    /// <returns>The folder, or <see langword="null"/> when the app ships none.</returns>
    private static string? FindBundledDataDirectory()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, DataFolder);
        if (Directory.Exists(beside))
        {
            return beside;
        }

        // A macOS bundle keeps data in Contents/Resources, next to Contents/MacOS where the app runs.
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", DataFolder));
        return OperatingSystem.IsMacOS() && Directory.Exists(resources) ? resources : null;
    }

    /// <summary>Loads the model of every language in a language string.</summary>
    /// <param name="directory">The folder holding the data.</param>
    /// <param name="language">The languages separated by <c>+</c>.</param>
    /// <returns><see langword="true"/> when every model loaded.</returns>
    private bool TryLoad(string directory, string language)
    {
        try
        {
            foreach (var range in language.AsSpan().Split('+'))
            {
                var path = Path.Combine(directory, string.Concat(language.AsSpan()[range], DataExtension));
                _models.Add(LstmModel.Load(TrainedData.Open(File.ReadAllBytes(path))));
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _models.Clear();
            return false;
        }
    }
}
