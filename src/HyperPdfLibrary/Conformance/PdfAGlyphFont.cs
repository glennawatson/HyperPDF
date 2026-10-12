// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A used font with what the PDF/A glyph rules read from it, gathered once per font.</summary>
[DebuggerDisplay("PdfAGlyphFont: {Use.Kind}")]
internal sealed class PdfAGlyphFont
{
    /// <summary>Initializes a new instance of the <see cref="PdfAGlyphFont"/> class.</summary>
    /// <param name="use">The font use.</param>
    /// <param name="program">The embedded program.</param>
    /// <param name="store">The objects.</param>
    internal PdfAGlyphFont(PdfAFontUse use, PdfAFontProgram program, PdfObjectStore store)
    {
        Use = use;
        Program = program;
        Store = store;
        Primary = PdfAFontFacts.Primary(use);
        Descriptor = PdfAFontFacts.Descriptor(Primary);
        Symbolic = PdfAFontFacts.IsSymbolic(Descriptor);
    }

    /// <summary>Gets the font use.</summary>
    internal PdfAFontUse Use { get; }

    /// <summary>Gets the embedded program.</summary>
    internal PdfAFontProgram Program { get; }

    /// <summary>Gets the objects.</summary>
    internal PdfObjectStore Store { get; }

    /// <summary>Gets the dictionary veraPDF reads font entries from: the font, or a composite font's CIDFont.</summary>
    internal PdfDictionary Primary { get; }

    /// <summary>Gets the font descriptor, or null.</summary>
    internal PdfDictionary? Descriptor { get; }

    /// <summary>Gets a value indicating whether the descriptor marks the font symbolic.</summary>
    internal bool Symbolic { get; }

    /// <summary>Gets or sets the simple font's encoding.</summary>
    internal PdfASimpleEncoding Encoding { get; set; } = PdfASimpleEncoding.Empty;

    /// <summary>Gets or sets the standard 14 metrics a Type 1 font without a descriptor uses, or null.</summary>
    internal StandardFontMetrics? Metrics { get; set; }

    /// <summary>Gets or sets the code-to-name table a non-symbolic TrueType program builds from /Encoding, or null.</summary>
    internal string[]? TrueTypeNames { get; init; }

    /// <summary>Gets or sets the ToUnicode map.</summary>
    internal PdfAToUnicode ToUnicode { get; set; } = PdfAToUnicode.None;

    /// <summary>Gets or sets the composite font's code reader, or null.</summary>
    internal PdfACidCodes? Codes { get; set; }

    /// <summary>Gets or sets the composite font's CIDFont widths, or null.</summary>
    internal PdfACidWidths? CidWidths { get; set; }

    /// <summary>Gets or sets the glyph of each CID from a /CIDToGIDMap stream, or null when CIDs are glyph ids.</summary>
    internal int[]? CidToGid { get; init; }

    /// <summary>Gets the width a missing /Widths entry or an unknown program width falls back to.</summary>
    internal double MissingWidth => Descriptor?.Get(KnownName.MissingWidth) is { IsNumber: true } width ? width.AsNumber() : 0;

    /// <summary>Gets a value indicating whether the descriptor has a /MissingWidth entry.</summary>
    internal bool HasMissingWidth => Descriptor?.ContainsKey(KnownName.MissingWidth) == true;
}
