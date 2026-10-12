// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Writes the font dictionary, descriptor, program and ToUnicode map of an embedded substitute font.</summary>
internal static class PdfAConversionFontWriter
{
    /// <summary>The glyph space of PDF widths.</summary>
    private const float GlyphSpace = 1000;

    /// <summary>The letters of a subset tag.</summary>
    private const int TagLength = 6;

    /// <summary>The letters a subset tag draws from.</summary>
    private const int Letters = 26;

    /// <summary>The FNV-1a offset basis.</summary>
    private const uint HashBasis = 2_166_136_261;

    /// <summary>The FNV-1a prime.</summary>
    private const uint HashPrime = 16_777_619;

    /// <summary>The entries of a font dictionary.</summary>
    private const int FontEntries = 8;

    /// <summary>The entries of a font descriptor.</summary>
    private const int DescriptorEntries = 10;

    /// <summary>Names a subset the way PDF does: six capital letters from the subset's contents, a plus sign and the font's name.</summary>
    /// <param name="faceName">The face's name.</param>
    /// <param name="glyphs">The glyph of each code.</param>
    /// <returns>The name.</returns>
    internal static string SubsetName(string faceName, SortedDictionary<int, int> glyphs)
    {
        var hash = HashBasis;
        foreach (var (code, glyph) in glyphs)
        {
            hash = (hash ^ (uint)code) * HashPrime;
            hash = (hash ^ (uint)glyph) * HashPrime;
        }

        Span<char> tag = stackalloc char[TagLength];
        for (var i = 0; i < TagLength; i++)
        {
            tag[i] = (char)('A' + (hash % Letters));
            hash /= Letters;
        }

        return string.Concat(tag, "+", faceName);
    }

    /// <summary>Writes the substitute and replaces the font object.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="id">The font object.</param>
    /// <param name="substitution">The substitute.</param>
    internal static void Write(PdfObjectStore store, PdfObjectId id, PdfAConversionSubstitution substitution)
    {
        var plan = substitution.Plan;
        var baseFont = PdfValue.FromName(store.Names.Intern(Encoding.UTF8.GetBytes(substitution.Name)));
        var programDictionary = new PdfDictionary(store, 1);
        programDictionary.Set(store.Names.Intern("Length1"u8), PdfValue.FromInteger(substitution.Subset.Length));
        var programRef = StoreEditing.Add(store, PdfValue.FromStream(new(programDictionary, substitution.Subset)));
        var descriptor = Descriptor(store, baseFont, substitution, programRef);
        var font = new PdfDictionary(store, FontEntries);
        font.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        font.Set(KnownName.Subtype, PdfValue.FromName(KnownName.TrueType));
        font.Set(KnownName.BaseFont, baseFont);
        var first = int.MaxValue;
        var last = int.MinValue;
        foreach (var code in plan.Glyphs.Keys)
        {
            first = Math.Min(first, code);
            last = Math.Max(last, code);
        }

        font.Set(KnownName.FirstChar, PdfValue.FromInteger(first));
        font.Set(KnownName.LastChar, PdfValue.FromInteger(last));
        var widths = new PdfArray(store, last - first + 1);
        for (var code = first; code <= last; code++)
        {
            widths.Add(plan.Widths.TryGetValue(code, out var width) ? Number(width) : PdfValue.FromInteger(0));
        }

        font.Set(KnownName.Widths, PdfValue.FromArray(widths));
        font.Set(KnownName.FontDescriptor, PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromDictionary(descriptor))));
        if (plan.Texts.Count > 0)
        {
            var map = PdfValue.FromStream(new(new PdfDictionary(store, 0), PdfAConversionToUnicode.Write(plan.Texts)));
            font.Set(KnownName.ToUnicode, PdfValue.FromReference(StoreEditing.Add(store, map)));
        }

        StoreEditing.Replace(store, id, PdfValue.FromDictionary(font));
    }

    /// <summary>Builds the font descriptor from the face's metrics and the standard font's.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="baseFont">The font's name.</param>
    /// <param name="substitution">The substitute.</param>
    /// <param name="programRef">The embedded program.</param>
    /// <returns>The descriptor.</returns>
    private static PdfDictionary Descriptor(PdfObjectStore store, PdfValue baseFont, PdfAConversionSubstitution substitution, PdfObjectId programRef)
    {
        var program = substitution.Program;
        var face = substitution.Face;
        var scale = GlyphSpace / program.UnitsPerEm;
        var flags = FontFlags.Symbolic;
        flags |= face.Family == BundledFamily.Fixed ? FontFlags.FixedPitch : FontFlags.None;
        flags |= face.Family == BundledFamily.Serif ? FontFlags.Serif : FontFlags.None;
        flags |= face.Italic ? FontFlags.Italic : FontFlags.None;
        var box = program.BoundingBox;
        float[] bounds = [MathF.Round(box.Left * scale), MathF.Round(box.Bottom * scale), MathF.Round(box.Right * scale), MathF.Round(box.Top * scale)];
        var descriptor = new PdfDictionary(store, DescriptorEntries);
        descriptor.Set(KnownName.Type, PdfValue.FromName(KnownName.FontDescriptor));
        descriptor.Set(KnownName.FontName, baseFont);
        descriptor.Set(KnownName.Flags, PdfValue.FromInteger((int)flags));
        descriptor.Set(KnownName.FontBBox, PdfValue.FromArray(PdfArray.FromNumbers(store, bounds)));
        descriptor.Set(KnownName.ItalicAngle, Number(StandardFonts.GetItalicAngle(substitution.Standard)));
        descriptor.Set(KnownName.Ascent, Number(MathF.Round(program.Ascent * scale)));
        descriptor.Set(KnownName.Descent, Number(MathF.Round(program.Descent * scale)));
        descriptor.Set(KnownName.CapHeight, Number(StandardFonts.GetCapHeight(substitution.Standard)));
        descriptor.Set(KnownName.StemV, Number(StandardFonts.GetStemV(substitution.Standard)));
        descriptor.Set(KnownName.FontFile2, PdfValue.FromReference(programRef));
        return descriptor;
    }

    /// <summary>Writes a number as an integer when it is one.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    private static PdfValue Number(float value) => float.IsInteger(value) ? PdfValue.FromInteger((long)value) : PdfValue.FromReal(value);
}
