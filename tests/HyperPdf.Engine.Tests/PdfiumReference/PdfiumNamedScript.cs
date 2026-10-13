// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A document-level JavaScript action as PDFium reads it. PDFium does not run it here.</summary>
/// <param name="Index">The entry's place in the <c>/JavaScript</c> name tree.</param>
/// <param name="Name">The name tree key.</param>
/// <param name="Script">The <c>/JS</c> text.</param>
internal sealed record PdfiumNamedScript(int Index, string Name, string Script);
