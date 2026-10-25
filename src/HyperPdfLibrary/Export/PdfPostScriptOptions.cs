// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Export;

/// <summary>How pages are written as PostScript.</summary>
/// <param name="Encapsulated">Whether to write Encapsulated PostScript (EPS): one page, a bounding box, and no <c>showpage</c>, so another document can place it.</param>
[DebuggerDisplay("PdfPostScriptOptions: encapsulated {Encapsulated}")]
public sealed record PdfPostScriptOptions(bool Encapsulated)
{
    /// <summary>Gets options for a PostScript document with a page for each page written.</summary>
    public static PdfPostScriptOptions Document { get; } = new(false);

    /// <summary>Gets options for one page as Encapsulated PostScript.</summary>
    public static PdfPostScriptOptions Eps { get; } = new(true);
}
