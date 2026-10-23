// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A piece of the text of a <see cref="PdfParagraph"/>: a run of text, a tab, a line break, a field, a footnote, a link or a bookmark.</summary>
[DebuggerDisplay("PdfInline")]
public abstract record PdfInline
{
    /// <summary>Initializes a new instance of the <see cref="PdfInline"/> class; only the inlines in this library derive from it.</summary>
    private protected PdfInline()
    {
    }
}
