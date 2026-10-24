// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Barcodes;
using HyperPdfLibrary.Creation;

namespace HyperPdf.Benchmarks;

/// <summary>Measures encoding the barcodes and drawing one on a page; allocations come from the EventPipe trace.</summary>
public class BarcodeBenchmarks
{
    /// <summary>The text of the two dimensional codes.</summary>
    private const string Message = "https://example.com/invoices/2026-000123?ref=HYPERPDF";

    /// <summary>The text of the linear codes.</summary>
    private const string LinearMessage = "HYPERPDF-12345";

    /// <summary>The digits of the EAN-13 code.</summary>
    private const string EanDigits = "590123412345";

    /// <summary>The digits of the interleaved code.</summary>
    private const string InterleavedDigits = "12345678";

    /// <summary>The side of the box a barcode is drawn into, in points.</summary>
    private const float Side = 120F;

    /// <summary>The page size, in points.</summary>
    private const float PageSide = 200F;

    /// <summary>A QR code, drawn by <see cref="DrawQr"/>.</summary>
    private PdfBarcode _qr = null!;

    /// <summary>The document the QR code is drawn into.</summary>
    private PdfDocumentCreator _creator = null!;

    /// <summary>Encodes the QR code once for drawing.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _qr = PdfBarcode.CreateQr(Message, PdfQrErrorCorrection.Medium);
        _creator = new(PdfDocumentCreatorOptions.Default);
    }

    /// <summary>Encodes a QR code, choosing its version and mask.</summary>
    /// <returns>The barcode.</returns>
    [Benchmark]
    public PdfBarcode EncodeQr() => PdfBarcode.CreateQr(Message, PdfQrErrorCorrection.Medium);

    /// <summary>Encodes a Data Matrix code.</summary>
    /// <returns>The barcode.</returns>
    [Benchmark]
    public PdfBarcode EncodeDataMatrix() => PdfBarcode.CreateDataMatrix(Message);

    /// <summary>Encodes a Code 128 barcode.</summary>
    /// <returns>The barcode.</returns>
    [Benchmark]
    public PdfBarcode EncodeCode128() => PdfBarcode.CreateCode128(LinearMessage);

    /// <summary>Encodes a Code 39 barcode.</summary>
    /// <returns>The barcode.</returns>
    [Benchmark]
    public PdfBarcode EncodeCode39() => PdfBarcode.CreateCode39(LinearMessage);

    /// <summary>Encodes an EAN-13 barcode.</summary>
    /// <returns>The barcode.</returns>
    [Benchmark]
    public PdfBarcode EncodeEan13() => PdfBarcode.CreateEan13(EanDigits);

    /// <summary>Encodes an interleaved two of five barcode.</summary>
    /// <returns>The barcode.</returns>
    [Benchmark]
    public PdfBarcode EncodeInterleaved() => PdfBarcode.CreateInterleaved2Of5(InterleavedDigits);

    /// <summary>Draws the QR code on a new page of a reused document and discards the page.</summary>
    [Benchmark]
    public void DrawQr()
    {
        var canvas = _creator.BeginPage(PageSide, PageSide);
        try
        {
            canvas.DrawBarcode(_qr, PdfBarcodeStyle.Default, new(0, 0, Side, Side));
        }
        finally
        {
            canvas.Dispose();
        }
    }
}
