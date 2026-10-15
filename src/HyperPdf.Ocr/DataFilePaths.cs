// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr;

/// <summary>
/// Gives the path of each language's data file in a folder. File checks take a path string, so each path is built once
/// and kept, instead of once per check.
/// </summary>
[DebuggerDisplay("OCR data paths: {_folders.Count} folders")]
public sealed class DataFilePaths
{
    /// <summary>The most folders kept; past this the list starts again, as only a few folders are ever checked.</summary>
    private const int MaxFolders = 16;

    /// <summary>The extension Tesseract expects on a language file.</summary>
    private const string Extension = ".traineddata";

    /// <summary>The separator placed between a folder and a file name that the folder does not end with.</summary>
    private static readonly string Separator = Path.DirectorySeparatorChar.ToString();

    /// <summary>Guards <see cref="_folders"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>The paths found so far, by folder and then by language code.</summary>
    private readonly Dictionary<string, Dictionary<string, string>> _folders = [with(StringComparer.Ordinal)];

    /// <summary>Finds a folder holding every language in a setting, reusing this owner's path strings.</summary>
    /// <param name="language">The languages separated by a plus sign.</param>
    /// <param name="packDirectory">The downloaded language folder, or null.</param>
    /// <returns>The folder, or null when none holds every language.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? FindDirectory(string language, string? packDirectory) => TesseractEngine.FindDataDirectory(language, packDirectory, this);

    /// <summary>Gets the path of a language's data file in a folder.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="code">The language code, for example <c>eng</c>.</param>
    /// <returns>The path.</returns>
    internal string Of(string directory, ReadOnlySpan<char> code)
    {
        lock (_gate)
        {
            if (_folders.Count >= MaxFolders && !_folders.ContainsKey(directory))
            {
                _folders.Clear();
            }

            ref var paths = ref CollectionsMarshal.GetValueRefOrAddDefault(_folders, directory, out _);
            paths ??= [with(StringComparer.Ordinal)];
            var lookup = paths.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(code, out var path))
            {
                return path;
            }

            path = string.Concat(directory, Path.EndsInDirectorySeparator(directory) ? ReadOnlySpan<char>.Empty : Separator.AsSpan(), code, Extension);
            lookup[code] = path;
            return path;
        }
    }
}
