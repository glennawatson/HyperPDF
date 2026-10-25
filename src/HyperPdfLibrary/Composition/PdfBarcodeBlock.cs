// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Barcodes;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A barcode on its own line, with its quiet zone. A tagged document describes it with the text it stands for.</summary>
/// <param name="Barcode">The barcode.</param>
/// <param name="Width">The width in points, quiet zone included.</param>
/// <param name="Height">The height in points, quiet zone included for a square barcode.</param>
[DebuggerDisplay("PdfBarcodeBlock: {Barcode}, {Width} x {Height} pt")]
public sealed record PdfBarcodeBlock(PdfBarcode Barcode, float Width, float Height) : PdfBlock
{
    /// <summary>Gets the colours, or <see langword="null"/> for black on white.</summary>
    public PdfBarcodeStyle? Style { get; init; }

    /// <summary>Gets the description for people who cannot see the barcode, or <see langword="null"/> to use the text it stands for.</summary>
    public string? AlternativeText { get; init; }

    /// <summary>Gets where the barcode sits between the left and right edges.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Gets the space above the barcode in points.</summary>
    public float SpaceBefore { get; init; }

    /// <summary>Gets the space below the barcode in points.</summary>
    public float SpaceAfter { get; init; }
}
