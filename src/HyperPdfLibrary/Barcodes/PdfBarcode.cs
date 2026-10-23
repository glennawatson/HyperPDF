// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Barcodes;

/// <summary>
/// A barcode as a grid of dark and light modules, made in managed code from text. A linear barcode is one row of modules; a QR
/// Code or Data Matrix is a square. Draw one with <c>DrawBarcode</c> on a page canvas.
/// </summary>
[DebuggerDisplay("PdfBarcode: {Symbology} {Columns}x{Rows}")]
public sealed class PdfBarcode
{
    /// <summary>The modules row by row; true is dark.</summary>
    private readonly bool[] _modules;

    /// <summary>Initializes a new instance of the <see cref="PdfBarcode"/> class.</summary>
    /// <param name="symbology">The barcode type.</param>
    /// <param name="text">The text it stands for.</param>
    /// <param name="columns">The modules across.</param>
    /// <param name="modules">The modules row by row; true is dark.</param>
    internal PdfBarcode(PdfBarcodeSymbology symbology, string text, int columns, bool[] modules)
    {
        Symbology = symbology;
        Text = text;
        Columns = columns;
        Rows = modules.Length / columns;
        _modules = modules;
    }

    /// <summary>Gets the barcode type.</summary>
    public PdfBarcodeSymbology Symbology { get; }

    /// <summary>Gets the text the barcode stands for. For an EAN-13 code this includes the check digit.</summary>
    public string Text { get; }

    /// <summary>Gets the modules across, not counting the quiet zone.</summary>
    public int Columns { get; }

    /// <summary>Gets the modules down, which is one for a linear barcode.</summary>
    public int Rows { get; }

    /// <summary>Gets a value indicating whether the barcode is one row of bars that stretch to any height.</summary>
    public bool IsLinear => Rows == 1;

    /// <summary>Makes a Code 39 barcode.</summary>
    /// <param name="text">The text: capital letters, digits, space and <c>- . $ / + %</c>.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or has a character Code 39 cannot hold.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfBarcode CreateCode39(string text) => Code39Encoder.Encode(text);

    /// <summary>Makes a Code 128 barcode.</summary>
    /// <param name="text">The text: ASCII characters 0 to 127.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or has a character above 127.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfBarcode CreateCode128(string text) => Code128Encoder.Encode(text);

    /// <summary>Makes an Interleaved 2 of 5 barcode.</summary>
    /// <param name="digits">The digits; an odd count gets a zero in front.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or has a character that is not a digit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfBarcode CreateInterleaved2Of5(string digits) => InterleavedEncoder.Encode(digits);

    /// <summary>Makes an EAN-13 barcode.</summary>
    /// <param name="digits">Twelve digits, whose check digit is added, or thirteen digits whose check digit is verified.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is not twelve or thirteen digits, or the thirteenth digit is not the check digit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfBarcode CreateEan13(string digits) => Ean13Encoder.Encode(digits);

    /// <summary>Makes a QR Code.</summary>
    /// <param name="text">The text, which is stored as numbers, capital letters or UTF-8, whichever is smallest.</param>
    /// <param name="errorCorrection">How much damage the code can take.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or too long for the largest QR Code at this error correction level.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfBarcode CreateQr(string text, PdfQrErrorCorrection errorCorrection) => QrEncoder.Encode(text, errorCorrection);

    /// <summary>Makes a Data Matrix (ECC 200) barcode in the smallest square symbol that holds the text.</summary>
    /// <param name="text">The text, stored as ASCII, with digit pairs packed and other bytes as UTF-8.</param>
    /// <returns>The barcode.</returns>
    /// <exception cref="ArgumentException">The text is empty or too long for the largest Data Matrix.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfBarcode CreateDataMatrix(string text) => DataMatrixEncoder.Encode(text);

    /// <summary>Determines whether a module is dark.</summary>
    /// <param name="column">The module's column from the left.</param>
    /// <param name="row">The module's row from the top.</param>
    /// <returns><see langword="true"/> for a dark module.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The module is outside the barcode.</exception>
    public bool IsDark(int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        return _modules[(row * Columns) + column];
    }
}
