// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A simple font's encoding: a base encoding and the /Differences glyph names. <see cref="FontEncoding.None"/> as the
/// base means the font program's built-in encoding (ISO 32000-2, 9.6.5.1 and Annex D).
/// </summary>
/// <param name="Base">The base encoding, or <see cref="FontEncoding.None"/> for the built-in one.</param>
/// <param name="Differences">The glyph name of each code /Differences sets, or <see langword="null"/> when it sets none.</param>
[DebuggerDisplay("SimpleEncoding: {Base}")]
internal readonly record struct SimpleEncoding(FontEncoding Base, byte[]?[]? Differences)
{
    /// <summary>Reads a simple font's /Encoding.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="settings">The base encoding chosen so far and the font facts the rules depend on.</param>
    /// <returns>The encoding.</returns>
    internal static SimpleEncoding Read(PdfDictionary font, in EncodingSettings settings)
    {
        var baseEncoding = settings.Initial;
        var value = font.Get(KnownName.Encoding);
        if (value.IsNull)
        {
            return new(ForMissingEncoding(settings), null);
        }

        if (value.TryGetName(out var name))
        {
            return new(ForNamedEncoding(font, name, settings), null);
        }

        if (value.AsDictionary() is not { } dictionary)
        {
            ReportBad(font);
            return new(baseEncoding, null);
        }

        baseEncoding = ReadBase(font, dictionary, baseEncoding, settings);
        if ((!settings.IsEmbedded || settings.IsTrueType) && baseEncoding == FontEncoding.None)
        {
            baseEncoding = FontEncoding.Standard;
        }

        return new(baseEncoding, ReadDifferences(dictionary));
    }

    /// <summary>Gets a code's glyph name: the /Differences name, else the base encoding's name.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The name, or empty when the code has none.</returns>
    internal ReadOnlySpan<byte> GetName(int code)
    {
        if ((uint)code >= FontEncodings.CodeCount)
        {
            return [];
        }

        if (Differences?[code] is { } name)
        {
            return name;
        }

        return Base == FontEncoding.None ? [] : FontEncodings.GetGlyphName(Base, code);
    }

    /// <summary>Chooses the base when the font has no /Encoding.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The base encoding.</returns>
    private static FontEncoding ForMissingEncoding(in EncodingSettings settings)
    {
        if (settings.IsSymbolName)
        {
            // A TrueType font named Symbol is a symbolic font whose (3,0) cmap has no glyph names to look up.
            return settings.IsTrueType ? FontEncoding.None : FontEncoding.Symbol;
        }

        return !settings.IsEmbedded && settings.Initial == FontEncoding.None ? FontEncoding.WinAnsi : settings.Initial;
    }

    /// <summary>Chooses the base for an /Encoding name.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="name">The encoding name.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The base encoding.</returns>
    private static FontEncoding ForNamedEncoding(PdfDictionary font, PdfName name, in EncodingSettings settings)
    {
        if (settings.Initial is FontEncoding.Symbol or FontEncoding.ZapfDingbats)
        {
            return settings.Initial;
        }

        if (settings.IsSymbolic && settings.IsSymbolName)
        {
            return settings.IsTrueType ? settings.Initial : FontEncoding.Symbol;
        }

        // MacExpertEncoding is only valid for Type 1 fonts (Table 109), so a named /Encoding of it reads as WinAnsiEncoding.
        return ToEncoding(PdfNames.Spell(font, name), true) ?? settings.Initial;
    }

    /// <summary>Maps a predefined encoding name. StandardEncoding is not a valid /Encoding name (Table 109), so it keeps the base.</summary>
    /// <param name="name">The name.</param>
    /// <param name="expertAsWinAnsi">Whether MacExpertEncoding becomes WinAnsiEncoding.</param>
    /// <returns>The encoding, or <see langword="null"/> for any other name.</returns>
    private static FontEncoding? ToEncoding(ReadOnlySpan<byte> name, bool expertAsWinAnsi)
    {
        if (name.SequenceEqual("WinAnsiEncoding"u8))
        {
            return FontEncoding.WinAnsi;
        }

        if (name.SequenceEqual("MacRomanEncoding"u8))
        {
            return FontEncoding.MacRoman;
        }

        if (name.SequenceEqual("MacExpertEncoding"u8))
        {
            return expertAsWinAnsi ? FontEncoding.WinAnsi : FontEncoding.MacExpert;
        }

        return name.SequenceEqual("PDFDocEncoding"u8) ? FontEncoding.PdfDoc : null;
    }

    /// <summary>Reads /Differences: a code followed by the names of it and the codes after it.</summary>
    /// <param name="encoding">The encoding dictionary.</param>
    /// <returns>The names by code, or <see langword="null"/> when there are none.</returns>
    private static byte[]?[]? ReadDifferences(PdfDictionary encoding)
    {
        if (encoding.GetArray(KnownName.Differences) is not { } differences)
        {
            return null;
        }

        var names = new byte[]?[FontEncodings.CodeCount];
        long code = 0;
        var damaged = false;
        for (var i = 0; i < differences.Count; i++)
        {
            var item = differences.Get(i);
            if (item.TryGetName(out var name))
            {
                if ((ulong)code < FontEncodings.CodeCount)
                {
                    names[code] = PdfNames.Spell(encoding, name).ToArray();
                }
                else
                {
                    damaged = true;
                }

                code++;
            }
            else if (item.IsNumber)
            {
                code = item.AsInteger();
            }
            else
            {
                damaged = true;
            }
        }

        if (damaged)
        {
            ReportBad(encoding);
        }

        return names;
    }

    /// <summary>Reads /BaseEncoding from an encoding dictionary, reporting a name that is not an encoding.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="dictionary">The encoding dictionary.</param>
    /// <param name="baseEncoding">The base chosen so far.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The base encoding.</returns>
    private static FontEncoding ReadBase(PdfDictionary font, PdfDictionary dictionary, FontEncoding baseEncoding, in EncodingSettings settings)
    {
        if (baseEncoding is FontEncoding.Symbol or FontEncoding.ZapfDingbats)
        {
            return baseEncoding;
        }

        var named = ToEncoding(PdfNames.SpellEntry(dictionary, KnownName.BaseEncoding), settings.IsTrueType);
        if (named is null && dictionary.ContainsKey(KnownName.BaseEncoding))
        {
            ReportBad(font);
        }

        return named ?? baseEncoding;
    }

    /// <summary>Reports a malformed encoding.</summary>
    /// <param name="dictionary">The font or encoding dictionary, which names the document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReportBad(PdfDictionary dictionary) =>
        PdfOpenContext.Report(dictionary.Owner?.Context, PdfDiagnosticCode.BadFontEncoding, "A font's encoding or /Differences is malformed; the usable part is kept.", 0, -1);
}
