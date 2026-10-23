// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Saves a 200 page document plainly, with AES-128 encryption and with page content normalised, to show what the extra work costs.</summary>
public class HyperPdfEncryptedSaveBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 200;

    /// <summary>The lines of text on each page.</summary>
    private const int LinesPerPage = 40;

    /// <summary>The password of the encrypted file.</summary>
    private const string Password = "benchmark";

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(TestPdf.CreateTextFilled(Pages, LinesPerPage), null);

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Saves the document with a classic table and no encryption.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark(Baseline = true)]
    public int SavePlain() => PdfCompactWriter.Save(_document.Objects, PdfCompactOptions.Classic).Length;

    /// <summary>Saves the document encrypted with AES-128.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int SaveEncrypted() => PdfCompactWriter.Save(
        _document.Objects,
        PdfCompactOptions.Classic with { Encryption = new() { Algorithm = PdfEncryptionAlgorithm.Aes128, UserPassword = Password, OwnerPassword = Password } }).Length;

    /// <summary>Saves the document looking in every page's content for inline images to move out.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int SaveWithImageExternalisation() => PdfCompactWriter.Save(
        _document.Objects,
        PdfCompactOptions.Classic with { Normalisation = PdfContentNormalisation.ExternaliseInlineImages }).Length;
}
