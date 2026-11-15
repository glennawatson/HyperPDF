// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Engine;

/// <summary>
/// Holds the Noto Sans fonts that ship inside the app for scripts that many machines have no font for, such as Khmer or
/// Myanmar. They are the last choice: an installed font is used first, and these draw the characters only when none has
/// them. Each font is read from the assembly and opened once, when the first character of its script needs it. One instance
/// serves one owner; it is safe to use from many threads.
/// </summary>
[DebuggerDisplay("BundledScriptFonts: {_opened.Length} scripts")]
public sealed class BundledScriptFonts
{
    /// <summary>The start of the resource names.</summary>
    private const string ResourcePrefix = "HyperPdf.Engine.ScriptFonts.";

    /// <summary>The end of the resource names.</summary>
    private const string ResourceSuffix = "-Regular.ttf.z";

    /// <summary>The first code point of each range, in rising order.</summary>
    private static readonly int[] Starts =
    [
        0x0530, 0x0900, 0x0980, 0x0A00, 0x0A80, 0x0B00, 0x0B80, 0x0C00, 0x0C80, 0x0D00, 0x0D80, 0x0E00, 0x0E80, 0x0F00, 0x1000, 0x10A0,
        0x1200, 0x1780, 0x1800, 0x19E0, 0x1C90, 0x2D00, 0x2D80, 0xA8E0,
    ];

    /// <summary>The last code point of each range.</summary>
    private static readonly int[] Ends =
    [
        0x058F, 0x097F, 0x09FF, 0x0A7F, 0x0AFF, 0x0B7F, 0x0BFF, 0x0C7F, 0x0CFF, 0x0D7F, 0x0DFF, 0x0E7F, 0x0EFF, 0x0FFF, 0x109F, 0x10FF,
        0x139F, 0x17FF, 0x18AF, 0x19FF, 0x1CBF, 0x2D2F, 0x2DDF, 0xA8FF,
    ];

    /// <summary>The family name of the font for each script. Noto has no sans design for Tibetan, so that one is a serif.</summary>
    private static readonly string[] Families =
    [
        "Noto Sans Armenian", "Noto Sans Devanagari", "Noto Sans Bengali", "Noto Sans Gurmukhi", "Noto Sans Gujarati", "Noto Sans Oriya", "Noto Sans Tamil",
        "Noto Sans Telugu", "Noto Sans Kannada", "Noto Sans Malayalam", "Noto Sans Sinhala", "Noto Sans Thai", "Noto Sans Lao", "Noto Serif Tibetan",
        "Noto Sans Myanmar", "Noto Sans Georgian", "Noto Sans Ethiopic", "Noto Sans Khmer", "Noto Sans Mongolian",
    ];

    /// <summary>The script of each range, as an index into <see cref="Families"/>.</summary>
    private static readonly int[] Owners =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        16, 17, 18, 17, 15, 15, 16, 1,
    ];

    /// <summary>The opened fonts by script; a slot stays empty until its script is first needed.</summary>
    private readonly ScreenFontFace?[] _opened = new ScreenFontFace?[Families.Length];

    /// <summary>Which scripts were tried, so a font that cannot be read is not read again.</summary>
    private readonly bool[] _tried = new bool[Families.Length];

    /// <summary>Guards opening a font.</summary>
    private readonly Lock _gate = new();

    /// <summary>Finds the range a character is in.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The index of the script, or -1 when no bundled font is for the character's script.</returns>
    public static int IndexOf(int codePoint)
    {
        var index = Array.BinarySearch(Starts, codePoint);
        if (index < 0)
        {
            // Not the start of a range, so the range before the place it would go is the only one that can hold it.
            index = ~index - 1;
        }

        return index >= 0 && codePoint <= Ends[index] ? Owners[index] : -1;
    }

    /// <summary>Gets the name of the font for a script.</summary>
    /// <param name="index">The index from <see cref="IndexOf"/>.</param>
    /// <returns>The font's family name, such as <c>Noto Sans Khmer</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FamilyOf(int index) => Families[index];

    /// <summary>Opens the bundled font for a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The font, or <see langword="null"/> when no bundled font has the character.</returns>
    public ScreenFontFace? Open(int codePoint)
    {
        var index = IndexOf(codePoint);
        if (index < 0)
        {
            return null;
        }

        var face = Volatile.Read(ref _opened[index]) ?? Read(index);
        return face is not null && face.Program.GetGlyphByUnicode(codePoint) > 0 ? face : null;
    }

    /// <summary>Opens the bundled font that has every character of some text, if there is one.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The font for the script of the text's first character that has a bundled font, or <see langword="null"/> when none has all of it.</returns>
    public ScreenFontFace? OpenFor(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ScreenFontFace? face = null;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value <= byte.MaxValue || face is not null)
            {
                continue;
            }

            face = Open(rune.Value);
        }

        return face is not null && Covers(face, text) ? face : null;
    }

    /// <summary>Tells whether a font has every character of a text that is not a space or a control.</summary>
    /// <param name="face">The font.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether it does.</returns>
    private static bool Covers(ScreenFontFace face, string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune) && !Rune.IsControl(rune) && face.Program.GetGlyphByUnicode(rune.Value) <= 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the font of a script from the assembly.</summary>
    /// <param name="family">The font's family name.</param>
    /// <returns>The font file, or <see langword="null"/> when the assembly has none for the script.</returns>
    private static byte[]? Unpack(string family)
    {
        using var packed = typeof(BundledScriptFonts).Assembly.GetManifestResourceStream($"{ResourcePrefix}{family.Replace(" ", string.Empty, StringComparison.Ordinal)}{ResourceSuffix}");
        if (packed is null)
        {
            return null;
        }

        using var zlib = new ZLibStream(packed, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Opens the font of a range once, and keeps it.</summary>
    /// <param name="index">The range.</param>
    /// <returns>The font, or <see langword="null"/> when it cannot be read.</returns>
    private ScreenFontFace? Read(int index)
    {
        lock (_gate)
        {
            if (_opened[index] is { } known || _tried[index])
            {
                return _opened[index];
            }

            _tried[index] = true;
            var face = Unpack(Families[index]) is { } file ? ScreenFontFace.Open(file, 0) : null;
            Volatile.Write(ref _opened[index], face);
            return face;
        }
    }
}
