// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The embedded program of a used font as the PDF/A rules see it: whether one is embedded in a form veraPDF reads,
/// whether it parses, and the parsed TrueType tables, Type 1 program or CFF program.
/// </summary>
/// <param name="Present">Whether the descriptor embeds a program of a form the font type reads.</param>
/// <param name="Parsed">Whether the program parses.</param>
[DebuggerDisplay("PdfAFontProgram: present {Present}, parsed {Parsed}")]
internal sealed record PdfAFontProgram(bool Present, bool Parsed)
{
    /// <summary>Gets the program of a font that embeds none.</summary>
    internal static PdfAFontProgram Missing { get; } = new(false, false);

    /// <summary>Gets the program of a font whose program is embedded but does not parse.</summary>
    internal static PdfAFontProgram Broken { get; } = new(true, false);

    /// <summary>Gets the tables of a TrueType program, or of an OpenType program read as TrueType.</summary>
    internal PdfATrueTypeTables? TrueType { get; init; }

    /// <summary>Gets a Type 1 program.</summary>
    internal Type1Program? Type1 { get; init; }

    /// <summary>Gets a CFF program, bare or from an OpenType 'CFF ' table.</summary>
    internal CffProgram? Cff { get; init; }

    /// <summary>Gets a value indicating whether the program is embedded and parses.</summary>
    internal bool IsEmbedded => Present && Parsed;
}
