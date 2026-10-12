// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Gathers what the PDF/A glyph rules read from a used font: encodings, metrics, ToUnicode, CID widths and maps.</summary>
internal static class PdfAGlyphFonts
{
    /// <summary>The bytes of one /CIDToGIDMap entry.</summary>
    private const int GidBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Builds a font's glyph state.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="use">The font use.</param>
    /// <returns>The state.</returns>
    internal static PdfAGlyphFont Create(PdfAValidationContext context, PdfAFontUse use)
    {
        var store = context.Document.Objects;
        var program = PdfAFontPrograms.Get(context, use);
        var toUnicode = PdfAToUnicode.Read(store, use.Dictionary.Get(KnownName.ToUnicode), context.CancellationToken);
        if (use.Kind == PdfAFontKind.Type0)
        {
            var primary = PdfAFontFacts.Primary(use);
            var map = ReadCidToGid(primary.Get(KnownName.CIDToGIDMap), context.CancellationToken);
            return new(use, program, store) { ToUnicode = toUnicode, Codes = use.Codes, CidWidths = new(primary), CidToGid = map };
        }

        var encoding = use.Dictionary.Get(KnownName.Encoding);
        var symbolic = PdfAFontFacts.IsSymbolic(PdfAFontFacts.Descriptor(use.Dictionary));
        var names = use.Kind == PdfAFontKind.TrueType && !symbolic && !encoding.IsNull ? PdfATrueTypeNames.Build(store, encoding) : null;
        var font = new PdfAGlyphFont(use, program, store) { ToUnicode = toUnicode, Encoding = PdfASimpleEncoding.Read(store, encoding), TrueTypeNames = names };
        if (use.Kind == PdfAFontKind.Type1)
        {
            ApplyStandardMetrics(store, font, encoding);
        }

        return font;
    }

    /// <summary>
    /// Gives a standard 14 Type 1 font without a descriptor its metrics, and its built-in encoding when /Encoding names no
    /// base: StandardEncoding for the text fonts, none for Symbol and ZapfDingbats.
    /// </summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font.</param>
    /// <param name="encoding">The resolved /Encoding.</param>
    private static void ApplyStandardMetrics(PdfObjectStore store, PdfAGlyphFont font, PdfValue encoding)
    {
        if ((font.Descriptor?.Count ?? 0) != 0 || StandardMetrics(store, font.Primary) is not { } metrics)
        {
            return;
        }

        font.Metrics = metrics;
        if (font.Encoding.Base.Length != 0)
        {
            return;
        }

        var differences = PdfASimpleEncoding.ReadDifferences(store, PdfAFontFacts.Dictionary(encoding)?.GetArray(KnownName.Differences));
        var builtIn = metrics.Font is StandardFont.Symbol or StandardFont.ZapfDingbats ? [] : PdfAEncodingTables.Standard;
        font.Encoding = new(builtIn, differences);
    }

    /// <summary>Gets the metrics of a font named exactly as one of the standard 14.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns>The metrics, or null.</returns>
    private static StandardFontMetrics? StandardMetrics(PdfObjectStore store, PdfDictionary font) =>
        PdfAFontFacts.NameText(store, font, KnownName.BaseFont) is { } name && PdfAFontFacts.IsStandardName(name)
        && StandardFonts.TryGet(Encoding.ASCII.GetBytes(name), out var metrics) ? metrics : null;

    /// <summary>Reads a /CIDToGIDMap stream: two bytes per CID, a trailing odd byte ignored.</summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The map, or null when CIDs are glyph ids.</returns>
    private static int[]? ReadCidToGid(PdfValue value, CancellationToken cancellationToken)
    {
        if (value.AsStream() is not { } stream)
        {
            return null;
        }

        if (!PdfAFontStreams.TryDecode(stream, cancellationToken, out var data))
        {
            return [];
        }

        var map = new int[data.Length / GidBytes];
        for (var i = 0; i < map.Length; i++)
        {
            map[i] = (data[i * GidBytes] << ByteBits) | data[(i * GidBytes) + 1];
        }

        return map;
    }
}
