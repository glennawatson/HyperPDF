// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A simple font's encoding as veraPDF maps codes to glyph names. A base of MacRoman, MacExpert or WinAnsi gives names;
/// StandardEncoding and other names give none. An encoding dictionary adds its /Differences. A code the base does not
/// name reads .notdef when there is a base, and null when there is none and no difference names it.
/// </summary>
[DebuggerDisplay("PdfASimpleEncoding: {Base.Length} base names")]
internal sealed class PdfASimpleEncoding
{
    /// <summary>Initializes a new instance of the <see cref="PdfASimpleEncoding"/> class.</summary>
    /// <param name="baseNames">The base encoding's names, or empty.</param>
    /// <param name="differences">The differences, or null when the encoding is not a dictionary.</param>
    internal PdfASimpleEncoding(string[] baseNames, Dictionary<int, string>? differences)
    {
        Base = baseNames;
        Differences = differences;
    }

    /// <summary>Gets an encoding with no names.</summary>
    internal static PdfASimpleEncoding Empty { get; } = new([], null);

    /// <summary>Gets the base encoding's names, or empty.</summary>
    internal string[] Base { get; }

    /// <summary>Gets the differences, or null.</summary>
    internal Dictionary<int, string>? Differences { get; }

    /// <summary>Reads a font's /Encoding.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="encoding">The resolved /Encoding.</param>
    /// <returns>The encoding.</returns>
    internal static PdfASimpleEncoding Read(PdfObjectStore store, PdfValue encoding)
    {
        if (encoding.TryGetName(out var name))
        {
            return new(BaseNames(name.ToKnownName()), null);
        }

        return PdfAFontFacts.Dictionary(encoding) is not { } dictionary
            ? Empty
            : new(BaseNames(dictionary.GetName(KnownName.BaseEncoding).ToKnownName()), ReadDifferences(store, dictionary.GetArray(KnownName.Differences)));
    }

    /// <summary>Reads a /Differences array: numbers set the next code, names name it.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="differences">The array, or null.</param>
    /// <returns>The names by code.</returns>
    internal static Dictionary<int, string> ReadDifferences(PdfObjectStore store, PdfArray? differences)
    {
        var names = new Dictionary<int, string>();
        var code = 0;
        for (var i = 0; differences is not null && i < differences.Count; i++)
        {
            var item = differences.Get(i);
            if (item.Kind == PdfKind.Integer)
            {
                code = (int)item.AsInteger();
            }
            else if (item.TryGetName(out var glyph) && code != -1)
            {
                names[code] = store.Names.GetString(glyph);
                code++;
            }
        }

        return names;
    }

    /// <summary>Gets a code's glyph name.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The name, or null.</returns>
    internal string? Name(int code)
    {
        if (code < 0)
        {
            return null;
        }

        if (Differences is not null && Differences.TryGetValue(code, out var name))
        {
            return name;
        }

        if (Base.Length == 0)
        {
            return null;
        }

        return code < Base.Length ? Base[code] : PdfAEncodingTables.Notdef;
    }

    /// <summary>Gets the names of a base encoding.</summary>
    /// <param name="name">The encoding name.</param>
    /// <returns>The names, or empty for StandardEncoding and unknown names.</returns>
    private static string[] BaseNames(KnownName name) => name switch
    {
        KnownName.MacRomanEncoding => PdfAEncodingTables.MacRoman,
        KnownName.MacExpertEncoding => PdfAEncodingTables.MacExpert,
        KnownName.WinAnsiEncoding => PdfAEncodingTables.WinAnsi,
        _ => [],
    };
}
