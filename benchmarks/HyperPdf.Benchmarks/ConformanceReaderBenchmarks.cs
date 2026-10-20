// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading the PDF/X, PDF/VT and PAdES claims of a file; allocations come from the EventPipe trace.</summary>
public class ConformanceReaderBenchmarks
{
    /// <summary>A blank page object for the small files.</summary>
    private const string BlankPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>";

    /// <summary>The PDF/X file.</summary>
    private PdfDocument _pdfX = null!;

    /// <summary>The PDF/VT file.</summary>
    private PdfDocument _pdfVt = null!;

    /// <summary>The signed file.</summary>
    private PdfDocument _signed = null!;

    /// <summary>The signing certificate, kept so it is released.</summary>
    private X509Certificate2 _certificate = null!;

    /// <summary>Makes the files.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdfX = PdfDocumentReader.Open(
            MiniPdf.BuildWithTrailer(
                "/Info 4 0 R",
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /TrimBox [10 10 602 782] /BleedBox [5 5 607 787] >>",
                "<< /GTS_PDFXVersion (PDF/X-4) /Title (Job) >>"),
            null);
        _pdfVt = PdfDocumentReader.Open(
            MiniPdf.BuildWithTrailer(
                "/Info 9 0 R",
                "<< /Type /Catalog /Pages 2 0 R /DPartRoot 8 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
                BlankPage,
                BlankPage,
                BlankPage,
                "<< /Start 3 0 R /End 5 0 R >>",
                "<< /DPartRootNode 6 0 R /RecordLevel 1 >>",
                "<< /GTS_PDFVTVersion (PDF/VT-2) >>"),
            null);
        _certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var plain = PdfDocumentReader.Open(TestPdf.Create(1), null);
        PdfSigningOptions options = new() { Format = PdfSignatureFormat.CadesDetached, SigningTime = TimeProvider.System.GetUtcNow() };
        PdfCertificateSigner signer = new(_certificate);
        _signed = PdfDocumentReader.Open(PdfDocumentSigning.Sign(plain, options, signer).Bytes, null);
    }

    /// <summary>Releases the files and the certificate.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pdfX.Dispose();
        _pdfVt.Dispose();
        _signed.Dispose();
        _certificate.Dispose();
    }

    /// <summary>Reads the PDF/X claim.</summary>
    /// <returns>The count of findings.</returns>
    [Benchmark]
    public int ReadPdfX() => PdfXReader.Read(_pdfX).Findings.Length;

    /// <summary>Reads the PDF/VT claim.</summary>
    /// <returns>The count of findings.</returns>
    [Benchmark]
    public int ReadPdfVt() => PdfVtReader.Read(_pdfVt).Findings.Length;

    /// <summary>Reads the PAdES level of the signature.</summary>
    /// <returns>The count of signatures.</returns>
    [Benchmark]
    public int ReadPades() => PdfPadesReader.Read(_signed).Length;
}
