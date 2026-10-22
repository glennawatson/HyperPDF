// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on font subsets (ISO 19005-1, 6.3.5; ISO 19005-2 and -3, 6.2.11.4.2): a subset's /CharSet or
/// /CIDSet lists its glyphs. PDF/A-1 requires them and checks the glyphs shown; PDF/A-2 and -3 check an optional one
/// against every glyph of the program.
/// </summary>
internal static class PdfAFontSubsetRules
{
    /// <summary>The letters of a subset tag before its plus sign.</summary>
    private const int SubsetTagLength = 6;

    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The most /CIDSet bytes veraPDF reads.</summary>
    private const int MaxCidSetBytes = 16_384;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The high bit of a byte.</summary>
    private const int HighBit = 0x80;

    /// <summary>Gets the rules this class checks.</summary>
    internal static PdfARule[] Rules => [PdfARule.CharSetComplete, PdfARule.CharSetCompletePdfA1, PdfARule.CidSetComplete, PdfARule.CidSetCompletePdfA1];

    /// <summary>Determines whether a CID's bit is set, the high bit of the first byte being CID 0.</summary>
    /// <param name="bits">The /CIDSet bytes.</param>
    /// <param name="cid">The CID.</param>
    /// <returns><see langword="true"/> when it is set.</returns>
    internal static bool IsSet(byte[] bits, int cid) => cid / ByteBits < bits.Length && (bits[cid / ByteBits] & (HighBit >> (cid % ByteBits))) != 0;

    /// <summary>Checks a used font's /CharSet or /CIDSet.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    internal static void Check(PdfAValidationContext context, PdfAFontUse font)
    {
        // Only subsets are checked, and a font's glyph state is costly to build, so a font that is no subset never builds it.
        var store = context.Document.Objects;
        if (font.Kind == PdfAFontKind.Type1 && IsSubset(store, PdfAFontFacts.Primary(font)))
        {
            CheckCharSet(context, PdfAGlyphFonts.Create(context, font));
        }
        else if (font.Kind == PdfAFontKind.Type0 && PdfAFontFacts.Descendant(font.Dictionary) is { } descendant && IsSubset(store, descendant))
        {
            CheckCidSet(context, PdfAGlyphFonts.Create(context, font));
        }
    }

    /// <summary>Checks a Type 1 subset's /CharSet.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    private static void CheckCharSet(PdfAValidationContext context, PdfAGlyphFont font)
    {
        var where = PdfAFontFacts.Context(context.Document.Objects, font.Primary);
        var charSet = font.Descriptor?.Get(KnownName.CharSet) is { Kind: PdfKind.String } value ? value : default;
        var program = ProgramNames(font.Program);
        if (context.Part == Part1)
        {
            context.Require(!charSet.IsNull && font.Program.IsEmbedded && ShownNamesListed(font, program, ListedNames(charSet)), PdfARule.CharSetCompletePdfA1, where);
            return;
        }

        if (font.Program.IsEmbedded && !charSet.IsNull)
        {
            context.Require(ListsAll(ListedNames(charSet), program), PdfARule.CharSetComplete, where);
        }
    }

    /// <summary>Checks a CIDFont subset's /CIDSet.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The composite font.</param>
    private static void CheckCidSet(PdfAValidationContext context, PdfAGlyphFont font)
    {
        var where = PdfAFontFacts.Context(context.Document.Objects, font.Primary);
        var stream = font.Descriptor?.GetStream(KnownName.CIDSet);
        var bits = stream is null ? [] : ReadBits(stream, context.CancellationToken);
        if (context.Part == Part1)
        {
            context.Require(stream is not null && font.Program.IsEmbedded && ShownCidsListed(font, bits), PdfARule.CidSetCompletePdfA1, where);
            return;
        }

        if (font.Program.IsEmbedded && stream is not null)
        {
            context.Require(PdfAFontCids.ListsAll(font, bits), PdfARule.CidSetComplete, where);
        }
    }

    /// <summary>Determines whether a font name starts with a subset tag such as ABCDEF+.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font's primary dictionary.</param>
    /// <returns><see langword="true"/> for a subset.</returns>
    private static bool IsSubset(PdfObjectStore store, PdfDictionary font)
    {
        if (PdfAFontFacts.NameText(store, font, KnownName.BaseFont) is not { Length: > SubsetTagLength } name || name[SubsetTagLength] != '+')
        {
            return false;
        }

        foreach (var letter in name.AsSpan(0, SubsetTagLength))
        {
            if (!char.IsAsciiLetterUpper(letter))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every shown glyph the program has is in /CharSet, as PDF/A-1 checks.</summary>
    /// <param name="font">The font.</param>
    /// <param name="program">The program's glyph names.</param>
    /// <param name="listed">The /CharSet names.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool ShownNamesListed(PdfAGlyphFont font, HashSet<string> program, HashSet<string> listed)
    {
        foreach (var code in font.Use.Glyphs.Keys)
        {
            if (font.Encoding.Name(code) is { } name && program.Contains(name) && !listed.Contains(name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether every shown CID the program has is set in /CIDSet, as PDF/A-1 checks.</summary>
    /// <param name="font">The composite font.</param>
    /// <param name="bits">The /CIDSet bytes.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool ShownCidsListed(PdfAGlyphFont font, byte[] bits)
    {
        foreach (var code in font.Use.Glyphs.Keys)
        {
            var cid = font.Codes?.ToCid(code) ?? 0;
            if (cid != 0 && PdfAFontCids.Contains(font, cid) && !IsSet(bits, cid))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether /CharSet lists exactly the program's glyphs, .notdef aside: the counts may differ by .notdef
    /// alone, and each list holds the other's names.
    /// </summary>
    /// <param name="listed">The /CharSet names.</param>
    /// <param name="program">The program's glyph names.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool ListsAll(HashSet<string> listed, HashSet<string> program)
    {
        if (listed.Count != program.Count && listed.Count != program.Count - 1)
        {
            return false;
        }

        foreach (var name in listed)
        {
            if (name != PdfAEncodingTables.Notdef && !program.Contains(name))
            {
                return false;
            }
        }

        foreach (var name in program)
        {
            if (name != PdfAEncodingTables.Notdef && !listed.Contains(name))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the names of a /CharSet string.</summary>
    /// <param name="charSet">The string value.</param>
    /// <returns>The names.</returns>
    private static HashSet<string> ListedNames(PdfValue charSet)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (charSet.IsNull)
        {
            return names;
        }

        var lexer = new PdfLexer(charSet.AsStringBytes());
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            if (kind == PdfTokenKind.Name)
            {
                _ = names.Add(Encoding.Latin1.GetString(lexer.Lexeme));
            }
        }

        return names;
    }

    /// <summary>Gets the glyph names of a Type 1 or CFF program.</summary>
    /// <param name="program">The program.</param>
    /// <returns>The names; empty for other programs.</returns>
    private static HashSet<string> ProgramNames(PdfAFontProgram program)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (program.Type1 is { } type1)
        {
            AddNames(type1, names);
        }
        else if (program.Cff is { IsCidKeyed: false } cff)
        {
            AddNames(cff, names);
        }

        return names;
    }

    /// <summary>Adds every glyph name of a program.</summary>
    /// <param name="program">The program.</param>
    /// <param name="names">Receives the names.</param>
    private static void AddNames(FontProgram program, HashSet<string> names)
    {
        for (var glyph = 0; glyph < program.GlyphCount; glyph++)
        {
            _ = names.Add(Encoding.Latin1.GetString(program.GetGlyphName(glyph)));
        }
    }

    /// <summary>Reads a /CIDSet stream, at most as many bytes as veraPDF reads.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The bytes.</returns>
    private static byte[] ReadBits(PdfStream stream, CancellationToken cancellationToken) =>
        PdfAFontStreams.TryDecode(stream, cancellationToken, out var data) && data.Length > MaxCidSetBytes ? data[..MaxCidSetBytes] : data;
}
