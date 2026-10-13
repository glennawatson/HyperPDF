// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>One character of a PDFium text page.</summary>
/// <param name="Unicode">The value <c>FPDFText_GetUnicode</c> gives.</param>
/// <param name="Box">The glyph box from <c>FPDFText_GetCharBox</c>, or <see langword="null"/> when PDFium gives none.</param>
internal readonly record struct PdfiumCharacter(uint Unicode, PdfiumCharBox? Box);
