// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A character's glyph box in PDF page space, as <c>FPDFText_GetCharBox</c> gives it.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Top">The top edge.</param>
internal readonly record struct PdfiumCharBox(double Left, double Right, double Bottom, double Top);
