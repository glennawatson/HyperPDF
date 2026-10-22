// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Decrypts a 4 KiB AES-128 stream that is padded correctly, and one a writer padded wrongly, which the reader decrypts again without padding and drops a plausible pad from.</summary>
public class HyperPdfAesPaddingBenchmarks
{
    /// <summary>The bytes of the stream.</summary>
    private const int StreamBytes = 4096;

    /// <summary>The password of the document.</summary>
    private const string Password = "benchmark";

    /// <summary>The object whose key the stream is encrypted for.</summary>
    private static readonly PdfObjectId Id = new(7, 0);

    /// <summary>The document, which owns the security handler.</summary>
    private PdfDocument _document = null!;

    /// <summary>The encrypted stream with correct padding.</summary>
    private byte[] _good = [];

    /// <summary>The encrypted stream whose padding is wrong.</summary>
    private byte[] _bad = [];

    /// <summary>Encrypts a document, takes its handler and makes the two streams.</summary>
    /// <exception cref="InvalidOperationException">The document has no handler, or the damaged stream still decrypts with its padding.</exception>
    [GlobalSetup]
    public void Setup()
    {
        using var plain = PdfDocumentReader.Open(TestPdf.CreateTextFilled(1, 1), null);
        var options = PdfCompactOptions.Default with { Encryption = new() { Algorithm = PdfEncryptionAlgorithm.Aes128, UserPassword = Password, OwnerPassword = Password } };
        _document = PdfDocumentReader.Open(PdfCompactWriter.Save(plain.Objects, options), Password);
        var handler = _document.Objects.Security ?? throw new InvalidOperationException("The document is not encrypted.");
        var data = new byte[StreamBytes];
        data.AsSpan().Fill(1);
        _good = handler.EncryptStream(Id, data);

        // Changing the last cipher byte scrambles the last plain block, whose last byte is then not a valid pad.
        _bad = [.. _good];
        _bad[^1] ^= 1;
        if (DecryptBad() is null)
        {
            throw new InvalidOperationException("The damaged stream did not decrypt.");
        }
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Decrypts the correctly padded stream.</summary>
    /// <returns>The plain bytes.</returns>
    [Benchmark(Baseline = true)]
    public byte[] DecryptGoodPadding() => _document.Objects.Security!.DecryptStream(Id, _good);

    /// <summary>Decrypts the wrongly padded stream.</summary>
    /// <returns>The plain bytes.</returns>
    [Benchmark]
    public byte[] DecryptBadPadding() => DecryptBad();

    /// <summary>Decrypts the damaged stream.</summary>
    /// <returns>The plain bytes.</returns>
    private byte[] DecryptBad() => _document.Objects.Security!.DecryptStream(Id, _bad);
}
