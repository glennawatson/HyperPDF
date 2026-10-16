// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Examples;

/// <summary>Holds the output produced by <see cref="EndToEndExample.RunAsync"/>.</summary>
/// <param name="PdfBytes">The saved PDF document bytes.</param>
/// <param name="PageCount">The total page count of the generated PDF document.</param>
/// <param name="ExtractedText">The text extracted from the first page.</param>
/// <param name="FirstPagePngBytes">The rendered first page encoded as a PNG image.</param>
/// <param name="RenderedWidth">The width of the rendered first page in pixels.</param>
/// <param name="RenderedHeight">The height of the rendered first page in pixels.</param>
[DebuggerDisplay("EndToEndExampleReport: {PageCount} page(s), {RenderedWidth}x{RenderedHeight} px")]
public sealed record EndToEndExampleReport(
    byte[] PdfBytes,
    int PageCount,
    string ExtractedText,
    byte[] FirstPagePngBytes,
    int RenderedWidth,
    int RenderedHeight);
