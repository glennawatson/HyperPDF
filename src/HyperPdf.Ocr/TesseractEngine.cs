// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.Ocr.Native;

namespace HyperPdf.Ocr;

/// <summary>
/// Recognises text with Tesseract, loaded from the app directory or the system. When Tesseract or its language data is
/// missing the engine reports <see cref="IsAvailable"/> as <see langword="false"/> and recognises nothing.
/// </summary>
[DebuggerDisplay("TesseractEngine: Tesseract {Language} available={IsAvailable}")]
public sealed unsafe class TesseractEngine : IOcrEngine
{
    /// <summary>Tesseract's word level.</summary>
    private const int WordLevel = 3;

    /// <summary>Tesseract's automatic page segmentation, which finds columns and blocks.</summary>
    private const int AutomaticSegmentation = 3;

    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72;

    /// <summary>Words recognised with less confidence than this are noise, not text.</summary>
    private const float MinimumConfidence = 20;

    /// <summary>The environment variable Tesseract itself uses for its language data.</summary>
    private const string DataVariable = "TESSDATA_PREFIX";

    /// <summary>The folder name Tesseract's language data is kept in.</summary>
    private const string DataFolder = "tessdata";

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

    /// <summary>Serialises use of the engine.</summary>
    private readonly Lock _gate = new();

    /// <summary>The engine, or <see langword="null"/> when unavailable.</summary>
    private readonly TesseractHandle? _handle;

    /// <summary>Initializes a new instance of the <see cref="TesseractEngine"/> class, using installed language data.</summary>
    /// <param name="language">The Tesseract language, for example <c>eng</c> or <c>eng+deu</c>.</param>
    public TesseractEngine(string language)
        : this(language, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TesseractEngine"/> class.</summary>
    /// <param name="language">The Tesseract language, for example <c>eng</c> or <c>eng+deu</c>.</param>
    /// <param name="packDirectory">The folder of downloaded language packs, searched first; <see langword="null"/> for none.</param>
    public TesseractEngine(string language, string? packDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Language = language;
        if (!IsLibraryAvailable())
        {
            Status = OcrEngineStatus.LibraryMissing;
            return;
        }

        if (FindDataDirectory(language, packDirectory) is not { } directory)
        {
            Status = OcrEngineStatus.LanguageMissing;
            return;
        }

        DataDirectory = directory;
        _handle = Start(directory, language);
        Status = _handle is null ? OcrEngineStatus.StartFailed : OcrEngineStatus.Ready;
    }

    /// <summary>Gets the folder holding the language data shipped with the app, or <see langword="null"/> when there is none.</summary>
    public static string? BundledDataDirectory { get; } = FindBundledDataDirectory();

    /// <summary>Gets the path or name Tesseract was loaded from, or <see langword="null"/> before it loads.</summary>
    public static string? LibraryPath => TesseractLibraryResolver.LoadedFrom;

    /// <summary>Gets the path of the Tesseract library shipped with the app, or <see langword="null"/> when none ships for this runtime.</summary>
    public static string? BundledLibraryPath => TesseractLibraryResolver.FindBundled();

    /// <summary>Gets the language the engine recognises.</summary>
    public string Language { get; }

    /// <summary>Gets the folder the language data was read from, or <see langword="null"/> when none was found.</summary>
    public string? DataDirectory { get; }

    /// <inheritdoc/>
    public bool IsAvailable => _handle is not null;

    /// <inheritdoc/>
    public OcrEngineStatus Status { get; }

    /// <summary>Determines whether the native Tesseract library can be loaded, preferring the copy shipped with the app.</summary>
    /// <returns><see langword="true"/> when it was found.</returns>
    public static bool IsLibraryAvailable()
    {
        TesseractLibraryResolver.Install();
        return TesseractLibraryResolver.TryLoad();
    }

    /// <summary>Finds the directory holding the language data for every language in a Tesseract language string.</summary>
    /// <param name="language">The language, for example <c>eng+deu</c>.</param>
    /// <returns>The directory, or <see langword="null"/> when the data is not installed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? FindDataDirectory(string language) => FindDataDirectory(language, null);

    /// <summary>
    /// Finds one directory holding the language data for every language in a Tesseract language string, because
    /// Tesseract reads all of a run's languages from one folder. Downloaded packs come first, then the data shipped with
    /// the app, then any the person or their system installed.
    /// </summary>
    /// <param name="language">The language, for example <c>eng+deu</c>.</param>
    /// <param name="packDirectory">The folder of downloaded language packs, or <see langword="null"/>.</param>
    /// <returns>The directory, or <see langword="null"/> when the data is not installed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? FindDataDirectory(string language, string? packDirectory) =>
        FindDataDirectory(language, packDirectory, new());

    /// <inheritdoc/>
    public void Dispose()
    {
        // A recognition in progress holds the gate and reads native memory the handle owns, so it finishes first.
        lock (_gate)
        {
            _handle?.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Recognize(ReadOnlySpan<byte> image, int width, int height, float pixelsPerPoint, List<OcrWord> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerPoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(image.Length, width * height);
        if (_handle is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_handle.IsClosed)
            {
                return;
            }

            fixed (byte* pixels = image)
            {
                NativeMethods.TessBaseAPISetImage(_handle, pixels, width, height, 1, width);
                NativeMethods.TessBaseAPISetSourceResolution(_handle, (int)MathF.Round(pixelsPerPoint * PointsPerInch));
                if (NativeMethods.TessBaseAPIRecognize(_handle, 0) == 0)
                {
                    ReadWords(_handle, pixelsPerPoint, output);
                }

                NativeMethods.TessBaseAPIClear(_handle);
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

    /// <summary>Reads the recognised words.</summary>
    /// <param name="handle">The engine, after recognition.</param>
    /// <param name="pixelsPerPoint">The image resolution.</param>
    /// <param name="output">The list receiving the words.</param>
    private static void ReadWords(TesseractHandle handle, float pixelsPerPoint, List<OcrWord> output)
    {
        var iterator = NativeMethods.TessBaseAPIGetIterator(handle);
        if (iterator == 0)
        {
            return;
        }

        try
        {
            ReadWords(iterator, pixelsPerPoint, output);
        }
        finally
        {
            NativeMethods.TessResultIteratorDelete(iterator);
        }
    }

    /// <summary>Reads the words an iterator walks.</summary>
    /// <param name="iterator">The result iterator.</param>
    /// <param name="pixelsPerPoint">The image resolution.</param>
    /// <param name="output">The list receiving the words.</param>
    private static void ReadWords(nint iterator, float pixelsPerPoint, List<OcrWord> output)
    {
        var page = NativeMethods.TessResultIteratorGetPageIterator(iterator);
        do
        {
            var confidence = NativeMethods.TessResultIteratorConfidence(iterator, WordLevel);
            if (confidence < MinimumConfidence
                || NativeMethods.TessPageIteratorBoundingBox(page, WordLevel, out var left, out var top, out var right, out var bottom) == 0)
            {
                continue;
            }

            var text = NativeMethods.TessResultIteratorGetUTF8Text(iterator, WordLevel);
            if (text is null)
            {
                continue;
            }

            try
            {
                var utf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text).Trim(" \t\r\n"u8);
                if (!utf8.IsEmpty)
                {
                    var bounds = PageRect.FromEdges(left / pixelsPerPoint, top / pixelsPerPoint, right / pixelsPerPoint, bottom / pixelsPerPoint);
                    output.Add(new(Encoding.UTF8.GetString(utf8), bounds, confidence));
                }
            }
            finally
            {
                NativeMethods.TessDeleteText(text);
            }
        }
        while (NativeMethods.TessResultIteratorNext(iterator, WordLevel) != 0);
    }

    /// <summary>Creates and initialises the native engine, treating a library without the expected entry points as unusable.</summary>
    /// <param name="directory">The language data directory.</param>
    /// <param name="language">The language.</param>
    /// <returns>The engine, or <see langword="null"/> when it could not start.</returns>
    private static TesseractHandle? Start(string directory, string language)
    {
        TesseractHandle? handle = null;
        try
        {
            handle = NativeMethods.TessBaseAPICreate();
            if (handle.IsInvalid || !Initialize(handle, directory, language))
            {
                handle.Dispose();
                return null;
            }

            NativeMethods.TessBaseAPISetPageSegMode(handle, AutomaticSegmentation);
            return handle;
        }
        catch (EntryPointNotFoundException)
        {
            // An older or unusual build of the library lacks part of the C API.
            handle?.Dispose();
            return null;
        }
        catch (DllNotFoundException)
        {
            // The library loaded but one of its own dependencies, such as Leptonica, did not.
            handle?.Dispose();
            return null;
        }
    }

    /// <summary>Initialises the engine with a language.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="directory">The language data directory.</param>
    /// <param name="language">The language.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Initialize(TesseractHandle handle, string directory, string language) =>
        NativeMethods.TessBaseAPIInit3(handle, directory, language) == 0;

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
}
