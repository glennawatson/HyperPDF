// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The simple-font encodings of ISO 32000-1, Annex D, as 256 glyph names with .notdef for unused codes. MacRomanEncoding
/// is the PDF encoding, without the Mac OS Roman extras such as notequal and apple.
/// </summary>
internal static class PdfAEncodingTables
{
    /// <summary>The name of unused codes.</summary>
    internal const string Notdef = ".notdef";

    /// <summary>The Mac OS Roman codes PDF's MacRomanEncoding leaves unused.</summary>
    private static readonly int[] MacOsExtras = [0xAD, 0xB0, 0xB2, 0xB3, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBD, 0xC3, 0xC5, 0xC6, 0xD7, 0xF0];

    /// <summary>Gets StandardEncoding.</summary>
    internal static string[] Standard { get; } = Build(FontEncoding.Standard, false);

    /// <summary>Gets WinAnsiEncoding.</summary>
    internal static string[] WinAnsi { get; } = Build(FontEncoding.WinAnsi, false);

    /// <summary>Gets MacRomanEncoding.</summary>
    internal static string[] MacRoman { get; } = Build(FontEncoding.MacRoman, true);

    /// <summary>Gets MacExpertEncoding.</summary>
    internal static string[] MacExpert { get; } = Build(FontEncoding.MacExpert, false);

    /// <summary>Finds the Mac OS Roman code of a glyph name, as a TrueType (1, 0) 'cmap' lookup uses it.</summary>
    /// <param name="name">The glyph name.</param>
    /// <returns>The code, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MacOsRomanCode(string name) => FontEncodings.GetCode(FontEncoding.MacRoman, Encoding.ASCII.GetBytes(name));

    /// <summary>Builds a table.</summary>
    /// <param name="encoding">The library encoding.</param>
    /// <param name="removeMacOsExtras">Whether to leave the Mac OS Roman extras unused.</param>
    /// <returns>The 256 names.</returns>
    private static string[] Build(FontEncoding encoding, bool removeMacOsExtras)
    {
        var names = new string[FontEncodings.CodeCount];
        for (var code = 0; code < names.Length; code++)
        {
            var name = FontEncodings.GetGlyphName(encoding, code);
            names[code] = name.IsEmpty || (removeMacOsExtras && Array.IndexOf(MacOsExtras, code) >= 0) ? Notdef : Encoding.ASCII.GetString(name);
        }

        return names;
    }
}
