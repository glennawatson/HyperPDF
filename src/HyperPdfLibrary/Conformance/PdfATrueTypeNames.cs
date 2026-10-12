// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Builds the code-to-name table veraPDF's TrueType program reads for a non-symbolic font with /Encoding. A name must be
/// MacRomanEncoding or WinAnsiEncoding; a dictionary starts from its /BaseEncoding (StandardEncoding otherwise), applies
/// /Differences, then fills .notdef codes from StandardEncoding. Any other /Encoding fails the program.
/// </summary>
internal static class PdfATrueTypeNames
{
    /// <summary>Determines whether a non-symbolic TrueType program can build its table from an /Encoding value.</summary>
    /// <param name="encoding">The resolved /Encoding.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    internal static bool CanBuild(PdfValue encoding) =>
        encoding.IsName(KnownName.MacRomanEncoding) || encoding.IsName(KnownName.WinAnsiEncoding) || PdfAFontFacts.Dictionary(encoding) is not null;

    /// <summary>Builds the table.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="encoding">The resolved /Encoding.</param>
    /// <returns>The 256 names, or null when the encoding fails the program.</returns>
    internal static string[]? Build(PdfObjectStore store, PdfValue encoding)
    {
        if (encoding.IsName(KnownName.MacRomanEncoding))
        {
            return [.. PdfAEncodingTables.MacRoman];
        }

        if (encoding.IsName(KnownName.WinAnsiEncoding))
        {
            return [.. PdfAEncodingTables.WinAnsi];
        }

        if (PdfAFontFacts.Dictionary(encoding) is not { } dictionary)
        {
            return null;
        }

        string[] names = [.. dictionary.GetName(KnownName.BaseEncoding).ToKnownName() switch
        {
            KnownName.WinAnsiEncoding => PdfAEncodingTables.WinAnsi,
            KnownName.MacRomanEncoding => PdfAEncodingTables.MacRoman,
            KnownName.MacExpertEncoding => PdfAEncodingTables.MacExpert,
            _ => PdfAEncodingTables.Standard,
        }];
        foreach (var (code, name) in PdfASimpleEncoding.ReadDifferences(store, dictionary.GetArray(KnownName.Differences)))
        {
            if (code >= 0 && code < names.Length)
            {
                names[code] = name;
            }
        }

        for (var code = 0; code < names.Length; code++)
        {
            if (names[code] == PdfAEncodingTables.Notdef)
            {
                names[code] = PdfAEncodingTables.Standard[code];
            }
        }

        return names;
    }
}
