// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Security;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures public-key PDF opening with a ready certificate and through the viewer's PKCS #12 file path.</summary>
public class HyperPdfCertificateOpenBenchmarks
{
    /// <summary>The permissions in the encrypted PDF.</summary>
    private const int Permissions = -3904;

    /// <summary>The test PKCS #12 password.</summary>
    private const string CertificatePassword = "benchmark-password";

    /// <summary>The engine under test.</summary>
    private readonly HyperPdfEngine _engine = new();

    /// <summary>The recipient certificate kept ready for the library baseline.</summary>
    private X509Certificate2 _certificate = null!;

    /// <summary>The benchmark's temporary folder.</summary>
    private string _folder = string.Empty;

    /// <summary>The encrypted PDF path.</summary>
    private string _pdfPath = string.Empty;

    /// <summary>The PKCS #12 path.</summary>
    private string _certificatePath = string.Empty;

    /// <summary>Creates the same PDF and certificate files for both benchmark methods.</summary>
    /// <returns>A task for file creation.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"pdf-certificate-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        _pdfPath = Path.Combine(_folder, "encrypted.pdf");
        _certificatePath = Path.Combine(_folder, "recipient.pfx");
        _certificate = PdfSigning.CreateCertificate("Benchmark Recipient", null, false, TimeProvider.System);
        await File.WriteAllBytesAsync(_pdfPath, PublicKeyPdf.Encrypted(PublicKeyCipher.Aes256, Permissions, true, _certificate));
        await File.WriteAllBytesAsync(_certificatePath, _certificate.Export(X509ContentType.Pkcs12, CertificatePassword));
    }

    /// <summary>Releases the generated certificate and files.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _certificate.Dispose();
        Directory.Delete(_folder, true);
    }

    /// <summary>Opens the encrypted file when its certificate is already imported.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int LibraryReadyCertificate()
    {
        using var document = PdfDocumentReader.OpenWith(_pdfPath, new PdfOpenOptions { Certificate = _certificate });
        return document.PageCount;
    }

    /// <summary>Imports a PKCS #12 file and opens the encrypted file through the viewer.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int ViewerCertificateFile()
    {
        using var document = _engine.OpenWithCertificate(_pdfPath, _certificatePath, CertificatePassword);
        return document.PageCount;
    }
}
