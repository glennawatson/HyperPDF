// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Writes the objects of an embedded composite font: the Type 0 font, its CIDFont descendant, the font descriptor, the
/// font program, the <c>/ToUnicode</c> CMap and the <c>/CIDSet</c> (ISO 32000-2, 9.6.4, 9.7, 9.8 and 9.10.3).
/// </summary>
internal static class EmbeddedFontWriter
{
    /// <summary>The entries of a font dictionary.</summary>
    private const int FontEntries = 8;

    /// <summary>The entries of a font descriptor.</summary>
    private const int DescriptorEntries = 12;

    /// <summary>Gets the key of a TrueType font file's uncompressed length.</summary>
    private static ReadOnlySpan<byte> Length1 => "Length1"u8;

    /// <summary>Writes the font's objects into the document.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="parts">What the objects are written from.</param>
    /// <returns>The Type 0 font dictionary, not yet added to the document.</returns>
    internal static PdfDictionary Write(IPdfObjectSink sink, in EmbeddedFontParts parts)
    {
        var name = PdfValue.FromName(sink.Names.Intern(parts.BaseFont));
        var descendant = new PdfDictionary(sink.Owner, FontEntries);
        descendant.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        descendant.Set(KnownName.Subtype, PdfValue.FromName(parts.Kind == EmbeddedProgramKind.TrueType ? KnownName.CIDFontType2 : KnownName.CIDFontType0));
        descendant.Set(KnownName.BaseFont, name);
        descendant.Set(KnownName.CIDSystemInfo, PdfValue.FromDictionary(CreateSystemInfo(sink, parts)));
        descendant.Set(KnownName.FontDescriptor, PdfValue.FromReference(sink.Add(PdfValue.FromDictionary(CreateDescriptor(sink, parts, name)))));
        WriteWidths(sink, descendant, parts);
        if (parts.Vertical is { } vertical)
        {
            WriteVerticalWidths(sink, descendant, parts, vertical);
        }

        if (parts.Kind == EmbeddedProgramKind.TrueType)
        {
            descendant.Set(KnownName.CIDToGIDMap, parts.CidToGidMap is { Length: > 0 } map
                ? PdfValue.FromReference(sink.Add(PdfValue.FromStream(FontStreams.Compress(sink, map, default))))
                : PdfValue.FromName(KnownName.Identity));
        }

        var composite = new PdfDictionary(sink.Owner, FontEntries);
        composite.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        composite.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type0));
        composite.Set(KnownName.BaseFont, name);
        composite.Set(KnownName.Encoding, PdfValue.FromName(parts.Vertical is null ? KnownName.IdentityH : KnownName.IdentityV));
        var descendants = new PdfArray(sink.Owner, 1);
        descendants.Add(PdfValue.FromReference(sink.Add(PdfValue.FromDictionary(descendant))));
        composite.Set(KnownName.DescendantFonts, PdfValue.FromArray(descendants));
        if (parts.ToUnicode is { Length: > 0 } toUnicode)
        {
            composite.Set(KnownName.ToUnicode, PdfValue.FromReference(sink.Add(PdfValue.FromStream(FontStreams.Compress(sink, toUnicode, default)))));
        }

        return composite;
    }

    /// <summary>Creates the font dictionary an embedded font starts as, which <see cref="Write"/> replaces when the font is finished.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="name">The font's name.</param>
    /// <param name="mode">The writing direction, which decides the encoding.</param>
    /// <returns>A Type 0 font dictionary with no descendant.</returns>
    internal static PdfDictionary CreatePlaceholder(IPdfObjectSink sink, string name, PdfWritingMode mode)
    {
        var placeholder = new PdfDictionary(sink.Owner, FontEntries);
        placeholder.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        placeholder.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type0));
        placeholder.Set(KnownName.BaseFont, PdfValue.FromName(sink.Names.Intern(name)));
        placeholder.Set(KnownName.Encoding, PdfValue.FromName(mode == PdfWritingMode.Vertical ? KnownName.IdentityV : KnownName.IdentityH));
        return placeholder;
    }

    /// <summary>Writes <c>/DW2</c> and <c>/W2</c>.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="descendant">The CIDFont dictionary.</param>
    /// <param name="parts">The font's parts.</param>
    /// <param name="vertical">The vertical metrics of the CIDs.</param>
    private static void WriteVerticalWidths(IPdfObjectSink sink, PdfDictionary descendant, in EmbeddedFontParts parts, VerticalMetrics vertical)
    {
        var defaultAt = CidVerticalWidths.ChooseDefault(vertical);
        if (CidVerticalWidths.CreateDefault(sink.Owner, vertical, defaultAt) is { } defaults)
        {
            descendant.Set(KnownName.DW2, PdfValue.FromArray(defaults));
        }

        var widths = CidVerticalWidths.Create(sink.Owner, parts.Cids, parts.Widths, vertical, defaultAt);
        if (widths.Count > 0)
        {
            descendant.Set(KnownName.W2, PdfValue.FromArray(widths));
        }
    }

    /// <summary>Writes <c>/DW</c> and <c>/W</c>.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="descendant">The CIDFont dictionary.</param>
    /// <param name="parts">The font's parts.</param>
    private static void WriteWidths(IPdfObjectSink sink, PdfDictionary descendant, in EmbeddedFontParts parts)
    {
        var defaultWidth = CidWidths.ChooseDefault(parts.Widths);
        if (!CidWidths.Same(defaultWidth, CidWidths.StandardDefault))
        {
            descendant.Set(KnownName.DW, PdfNumber.ToValue(defaultWidth));
        }

        var widths = CidWidths.Create(sink.Owner, parts.Cids, parts.Widths, defaultWidth);
        if (widths.Count > 0)
        {
            descendant.Set(KnownName.W, PdfValue.FromArray(widths));
        }
    }

    /// <summary>Creates the CID system information.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="parts">The font's parts.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary CreateSystemInfo(IPdfObjectSink sink, in EmbeddedFontParts parts)
    {
        var info = new PdfDictionary(sink.Owner, FontEntries);
        info.Set(KnownName.Registry, PdfValue.FromString(Encoding.ASCII.GetBytes(parts.Registry)));
        info.Set(KnownName.Ordering, PdfValue.FromString(Encoding.ASCII.GetBytes(parts.Ordering)));
        info.Set(KnownName.Supplement, PdfValue.FromInteger(parts.Supplement));
        return info;
    }

    /// <summary>Creates the font descriptor and adds the font file.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="parts">The font's parts.</param>
    /// <param name="name">The font's name.</param>
    /// <returns>The descriptor.</returns>
    private static PdfDictionary CreateDescriptor(IPdfObjectSink sink, in EmbeddedFontParts parts, PdfValue name)
    {
        var descriptor = new PdfDictionary(sink.Owner, DescriptorEntries);
        descriptor.Set(KnownName.Type, PdfValue.FromName(KnownName.FontDescriptor));
        descriptor.Set(KnownName.FontName, name);
        descriptor.Set(KnownName.Flags, PdfValue.FromInteger(parts.Flags));
        descriptor.Set(KnownName.FontBBox, PdfValue.FromArray(parts.FontBox.ToArray(sink.Owner)));
        descriptor.Set(KnownName.ItalicAngle, PdfNumber.ToValue(parts.ItalicAngle));
        descriptor.Set(KnownName.Ascent, PdfNumber.ToValue(MathF.Round(parts.Ascent)));
        descriptor.Set(KnownName.Descent, PdfNumber.ToValue(MathF.Round(parts.Descent)));
        descriptor.Set(KnownName.CapHeight, PdfNumber.ToValue(MathF.Round(parts.CapHeight)));
        descriptor.Set(KnownName.StemV, PdfValue.FromInteger(parts.StemV));
        if (parts.Kind == EmbeddedProgramKind.TrueType)
        {
            var file = FontStreams.Compress(sink, parts.Program, sink.Names.Intern(Length1));
            descriptor.Set(KnownName.FontFile2, PdfValue.FromReference(sink.Add(PdfValue.FromStream(file))));
        }
        else
        {
            PdfName subtype = parts.Kind == EmbeddedProgramKind.OpenTypeCff ? KnownName.OpenType : KnownName.CIDFontType0C;
            var file = FontStreams.Compress(sink, parts.Program, default, subtype);
            descriptor.Set(KnownName.FontFile3, PdfValue.FromReference(sink.Add(PdfValue.FromStream(file))));
        }

        if (parts.CidSet is { Length: > 0 } cidSet)
        {
            descriptor.Set(KnownName.CIDSet, PdfValue.FromReference(sink.Add(PdfValue.FromStream(FontStreams.Compress(sink, cidSet, default)))));
        }

        return descriptor;
    }
}
