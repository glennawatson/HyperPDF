// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>What a text field's rich text value (<c>/RV</c>) says, without its markup: its words and which styles it uses.</summary>
/// <param name="PlainText">The words, paragraphs separated by line breaks.</param>
/// <param name="Bold">Whether any of it is bold.</param>
/// <param name="Italic">Whether any of it is italic.</param>
/// <param name="Underline">Whether any of it is underlined.</param>
/// <param name="StrikeOut">Whether any of it is struck out.</param>
/// <param name="ParagraphCount">The number of paragraphs.</param>
[DebuggerDisplay("PdfFormRichTextSummary: {ParagraphCount} paragraphs")]
public sealed record PdfFormRichTextSummary(string PlainText, bool Bold, bool Italic, bool Underline, bool StrikeOut, int ParagraphCount);
