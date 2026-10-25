// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>The barcode types <see cref="PdfBarcode"/> makes.</summary>
public enum PdfBarcodeSymbology
{
    /// <summary>Code 39: capital letters, digits and a few signs, as bars of two widths.</summary>
    Code39 = 0,

    /// <summary>Code 128: all of ASCII, packed densely, with a check character.</summary>
    Code128 = 1,

    /// <summary>Interleaved 2 of 5: digits in pairs, with the pair shared between a bar and a space.</summary>
    Interleaved2Of5 = 2,

    /// <summary>EAN-13: thirteen digits for retail goods, the last a check digit; a UPC-A code is an EAN-13 code with a leading zero.</summary>
    Ean13 = 3,

    /// <summary>QR Code: a square of modules that holds text, with error correction.</summary>
    QrCode = 4,

    /// <summary>Data Matrix (ECC 200): a small square of modules that holds text, with error correction.</summary>
    DataMatrix = 5,
}
