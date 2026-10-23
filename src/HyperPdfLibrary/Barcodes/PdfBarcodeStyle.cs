// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Barcodes;

/// <summary>The colours a barcode is drawn in. Scanners need strong contrast, so keep the bars dark and the background light.</summary>
/// <param name="Bars">The colour of the dark modules.</param>
/// <param name="Background">The colour behind the bars and the quiet zone around them, or <see langword="null"/> to leave the page showing through.</param>
[DebuggerDisplay("PdfBarcodeStyle: {Bars} on {Background}")]
public readonly record struct PdfBarcodeStyle(PdfCanvasColor Bars, PdfCanvasColor? Background)
{
    /// <summary>Gets black bars on a white background.</summary>
    public static PdfBarcodeStyle Default => new(PdfCanvasColor.Black, PdfCanvasColor.White);
}
