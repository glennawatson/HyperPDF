// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>The text of one page as PDFium extracts it, including the spaces and line breaks it generates.</summary>
/// <param name="Text">The page text from <c>FPDFText_GetText</c>.</param>
/// <param name="Characters">Each character, in text order.</param>
internal sealed record PdfiumTextPage(string Text, PdfiumCharacter[] Characters);
