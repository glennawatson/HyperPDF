// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Engine;

namespace HyperPdf.Viewer.Text;

/// <summary>
/// Finds a font on the system for a character the bundled screen font lacks, such as Chinese, Arabic or Thai. Font files are
/// read only when such a character first appears, the likeliest ones first, and a font that helped is kept for the next time.
/// Everything here runs on the window thread.
/// </summary>
[DebuggerDisplay("FallbackFonts: {_kept.Count} kept, {_read} files read")]
internal sealed class FallbackFonts
{
    /// <summary>The bits of a code point that tell characters of one small block apart. A font that has one character of a block usually has the rest.</summary>
    private const int BlockShift = 7;

    /// <summary>The most font files read to find one character.</summary>
    private const int FilesPerSearch = 48;

    /// <summary>The most font files read in all, so a system with thousands of fonts cannot stall the window again and again.</summary>
    private const int FilesInAll = 320;

    /// <summary>The largest font file read, in bytes.</summary>
    private const long LargestFile = 64L << 20;

    /// <summary>The most faces of one font collection tried.</summary>
    private const int FacesPerFile = 4;

    /// <summary>The most characters remembered as having no font, before the memory is cleared.</summary>
    private const int MissingLimit = 4096;

    /// <summary>The score of a font with no preferred name, before style penalties.</summary>
    private const int PlainScore = 100;

    /// <summary>The score added for each style word in a name, so the regular face of a family is read before its bold or condensed ones.</summary>
    private const int StylePenalty = 30;

    /// <summary>The file endings of the font formats the library reads.</summary>
    private static readonly string[] Endings = [".ttf", ".otf", ".ttc", ".otc"];

    /// <summary>Parts of font file names that cover many scripts, the broadest first. Fonts named so are read before the rest.</summary>
    private static readonly string[] Preferred =
    [
        "NotoSansCJK", "NotoSans", "DejaVuSans", "msyh", "meiryo", "YuGoth", "malgun", "segoeui", "Nirmala", "seguisym", "PingFang",
        "Hiragino", "AppleSDGothic", "Arial Unicode", "GeezaPro", "Kohinoor", "Thonburi", "Noto", "Droid", "WenQuanYi", "wqy", "unifont", "FreeSans",
    ];

    /// <summary>Words in a font file name that mark a face other than the regular one.</summary>
    private static readonly string[] StyleWords =
    [
        "Bold", "Italic", "Oblique", "Light", "Thin", "Black", "Heavy", "Medium", "Condensed", "Narrow", "Mono", "Serif", "Display",
    ];

    /// <summary>The fonts that have helped so far.</summary>
    private readonly List<UiFont> _kept = [];

    /// <summary>The font that had a character of each block the last time one was looked for.</summary>
    private readonly Dictionary<int, UiFont> _byBlock = [];

    /// <summary>The characters no font was found for.</summary>
    private readonly HashSet<int> _missing = [];

    /// <summary>The font files that were kept, so they are not read again.</summary>
    private readonly HashSet<string> _keptFiles = [with(StringComparer.Ordinal)];

    /// <summary>The system's font files, likeliest first; listed the first time a font is looked for.</summary>
    private string[]? _files;

    /// <summary>How many font files have been read.</summary>
    private int _read;

    /// <summary>Finds a system font that has a character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The font, or <see langword="null"/> when none was found.</returns>
    internal UiFont? Find(Rune rune)
    {
        if (_byBlock.TryGetValue(rune.Value >> BlockShift, out var known) && known.Has(rune))
        {
            return known;
        }

        if (_missing.Contains(rune.Value))
        {
            return null;
        }

        var found = FindKept(rune) ?? Search(rune);
        if (found is null)
        {
            if (_missing.Count >= MissingLimit)
            {
                _missing.Clear();
            }

            _ = _missing.Add(rune.Value);
            return null;
        }

        _byBlock[rune.Value >> BlockShift] = found;
        return found;
    }

    /// <summary>Scores a font file by its name: fonts known to cover many scripts first, and regular faces before styled ones.</summary>
    /// <param name="path">The font file.</param>
    /// <returns>The score, lower for a likelier font.</returns>
    private static int Score(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var preferred = Array.FindIndex(Preferred, part => name.Contains(part, StringComparison.OrdinalIgnoreCase));
        var score = preferred >= 0 ? preferred : PlainScore;
        foreach (var word in StyleWords)
        {
            if (name.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                score += StylePenalty;
            }
        }

        return score;
    }

    /// <summary>Lists the folders the system keeps fonts in.</summary>
    /// <returns>The folders, which may not all exist.</returns>
    private static string[] FontFolders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return [Environment.GetFolderPath(Environment.SpecialFolder.Fonts), Path.Combine(local, "Microsoft", "Windows", "Fonts")];
        }

        return OperatingSystem.IsMacOS()
            ? ["/System/Library/Fonts", "/Library/Fonts", Path.Combine(home, "Library", "Fonts")]
            : ["/usr/share/fonts", "/usr/local/share/fonts", Path.Combine(home, ".local", "share", "fonts"), Path.Combine(home, ".fonts")];
    }

    /// <summary>Lists the system's font files, likeliest first.</summary>
    /// <returns>The files.</returns>
    private static string[] ListFiles()
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var files = new List<string>();
        foreach (var folder in FontFolders())
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*", options))
            {
                if (!Array.Exists(Endings, ending => file.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                files.Add(file);
            }
        }

        var sorted = files.ToArray();
        var scores = Array.ConvertAll(sorted, Score);
        Array.Sort(scores, sorted);
        return sorted;
    }

    /// <summary>Opens the first face of a font file that has a character.</summary>
    /// <param name="path">The font file.</param>
    /// <param name="rune">The character.</param>
    /// <returns>The face, or <see langword="null"/> when the file cannot be read or no face tried has the character.</returns>
    private static UiFont? OpenFor(string path, Rune rune)
    {
        byte[] bytes;
        try
        {
            if (new FileInfo(path).Length > LargestFile)
            {
                return null;
            }

            bytes = File.ReadAllBytes(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // A file past its last face does not open, which ends the loop for a single font after its one face.
        for (var index = 0; index < FacesPerFile; index++)
        {
            if (ScreenFontFace.Open(bytes, index) is not { } face || index >= face.Source.FaceCount)
            {
                return null;
            }

            var font = new UiFont(face);
            if (font.Has(rune))
            {
                return font;
            }
        }

        return null;
    }

    /// <summary>Looks through the fonts that have helped before.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private UiFont? FindKept(Rune rune)
    {
        foreach (var font in _kept)
        {
            if (font.Has(rune))
            {
                return font;
            }
        }

        return null;
    }

    /// <summary>Reads system font files, likeliest first, until one has a character or the search has read enough.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private UiFont? Search(Rune rune)
    {
        var files = _files ??= ListFiles();
        var budget = FilesPerSearch;

        // Fonts named for the character's script come first, so the general search rarely has to run.
        foreach (var name in ScriptFontNames.For(rune.Value))
        {
            if (SearchFiles(files, name, rune, ref budget) is { } named)
            {
                return named;
            }
        }

        return SearchFiles(files, null, rune, ref budget);
    }

    /// <summary>Reads font files in order until one has a character or the search has read enough.</summary>
    /// <param name="files">The system's font files, likeliest first.</param>
    /// <param name="name">A part the file name must contain, or <see langword="null"/> for any file.</param>
    /// <param name="rune">The character.</param>
    /// <param name="budget">The files this search may still read.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private UiFont? SearchFiles(string[] files, string? name, Rune rune, ref int budget)
    {
        foreach (var file in files)
        {
            if (budget <= 0 || _read >= FilesInAll)
            {
                return null;
            }

            if (_keptFiles.Contains(file) || (name is not null && !Path.GetFileName(file).Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            budget--;
            _read++;
            if (OpenFor(file, rune) is not { } font)
            {
                continue;
            }

            _kept.Add(font);
            _ = _keptFiles.Add(file);
            return font;
        }

        return null;
    }
}
