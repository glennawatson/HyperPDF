// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// One font dictionary that text-showing operators select during a PDF/A validation: the rendering modes it is shown
/// with and every character code it shows. The font rules check each used font once from this record.
/// </summary>
[DebuggerDisplay("PdfAFontUse: {Kind}, {Glyphs.Count} codes")]
internal sealed class PdfAFontUse
{
    /// <summary>Initializes a new instance of the <see cref="PdfAFontUse"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="kind">The font kind.</param>
    /// <param name="codes">The CMap that splits strings into codes, for composite fonts; null for simple fonts.</param>
    internal PdfAFontUse(PdfDictionary dictionary, PdfAFontKind kind, PdfACidCodes? codes)
    {
        Dictionary = dictionary;
        Kind = kind;
        Codes = codes;
    }

    /// <summary>Gets the font dictionary.</summary>
    internal PdfDictionary Dictionary { get; }

    /// <summary>Gets the font kind.</summary>
    internal PdfAFontKind Kind { get; }

    /// <summary>Gets the CMap that splits shown strings into codes for a composite font; null for simple fonts.</summary>
    internal PdfACidCodes? Codes { get; }

    /// <summary>Gets or sets the rendering modes the font is shown with, one bit per mode 0 to 7.</summary>
    internal int Modes { get; set; }

    /// <summary>Gets or sets the embedded program, once the rules have loaded it.</summary>
    internal PdfAFontProgram? Program { get; set; }

    /// <summary>Gets every character code shown with the font and how it is shown.</summary>
    internal Dictionary<int, PdfAGlyphUse> Glyphs { get; } = [];
}
