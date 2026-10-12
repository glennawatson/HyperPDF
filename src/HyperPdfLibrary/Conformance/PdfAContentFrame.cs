// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Where the PDF/A colour walk is inside a content stream.</summary>
/// <param name="Scope">The resources the stream reads.</param>
/// <param name="Paint">The graphics state at this point.</param>
/// <param name="Initial">The graphics state the stream started with, which tiling patterns painted from it start with.</param>
internal readonly record struct PdfAContentFrame(PdfAColourResourceScope Scope, PdfAColourPaintState Paint, PdfAColourPaintState Initial);
