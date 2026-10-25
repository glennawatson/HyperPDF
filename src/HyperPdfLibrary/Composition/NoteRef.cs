// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A footnote found in a paragraph and the number it was given.</summary>
/// <param name="Footnote">The footnote.</param>
/// <param name="Number">The footnote's number in the document, counted from 1.</param>
/// <param name="Label">The number written in the footnote's format.</param>
[DebuggerDisplay("NoteRef: {Label}")]
internal readonly record struct NoteRef(PdfFootnote Footnote, int Number, string Label);
