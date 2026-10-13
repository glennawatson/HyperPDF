// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>One PDFium search match.</summary>
/// <param name="Start">The first character index.</param>
/// <param name="Length">The character count.</param>
internal readonly record struct PdfiumMatch(int Start, int Length);
