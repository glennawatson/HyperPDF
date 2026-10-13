// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Pdf20;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures writing an encrypted copy with each cipher, including the password key derivation (ISO 32000-2 algorithm 2.B for
/// AES-256), and writing the copy without encryption again.
/// </summary>
public class HyperPdfEncryptionWriteBenchmarks
{
    /// <summary>The user password.</summary>
    private const string UserPassword = "reader-pass";

    /// <summary>The owner password.</summary>
    private const string OwnerPassword = "owner-pass";

    /// <summary>The plain document.</summary>
    private PdfDocument _plain = null!;

    /// <summary>The same document, already protected, opened with the owner password.</summary>
    private PdfDocument _protected = null!;

    /// <summary>Opens the plain document and a protected one.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = PdfDocumentReader.Open(Pdf20TestPdf.Plain(), null);
        using var encrypted = new MemoryStream();
        _ = PdfDocumentProtection.Protect(_plain, Options(PdfEncryptionAlgorithm.Aes256), encrypted);
        _protected = PdfDocumentReader.Open(encrypted.ToArray(), OwnerPassword);
    }

    /// <summary>Releases both documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _plain.Dispose();
        _protected.Dispose();
    }

    /// <summary>Writes a copy with AES-256 (revision 6), which derives the file key with the 2.B hash.</summary>
    /// <returns>The copy's length.</returns>
    [Benchmark(Baseline = true)]
    public long ProtectAes256() => Protect(_plain, PdfEncryptionAlgorithm.Aes256);

    /// <summary>Writes a copy with AES-128 (revision 4), which derives the file key with MD5.</summary>
    /// <returns>The copy's length.</returns>
    [Benchmark]
    public long ProtectAes128() => Protect(_plain, PdfEncryptionAlgorithm.Aes128);

    /// <summary>Writes a copy with AES-256 GCM (ISO/TS 32003).</summary>
    /// <returns>The copy's length.</returns>
    [Benchmark]
    public long ProtectAes256Gcm() => Protect(_plain, PdfEncryptionAlgorithm.Aes256Gcm);

    /// <summary>Decrypts every stream and string of a protected document and writes a copy without protection.</summary>
    /// <returns>The copy's length.</returns>
    [Benchmark]
    public long RemoveProtection()
    {
        using var output = new MemoryStream();
        _ = PdfDocumentProtection.RemoveProtection(_protected, output);
        return output.Length;
    }

    /// <summary>Makes the options for a cipher with both passwords.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>The options.</returns>
    private static PdfEncryptionOptions Options(PdfEncryptionAlgorithm algorithm) =>
        new() { Algorithm = algorithm, UserPassword = UserPassword, OwnerPassword = OwnerPassword };

    /// <summary>Writes a protected copy of a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>The copy's length.</returns>
    private static long Protect(PdfDocument document, PdfEncryptionAlgorithm algorithm)
    {
        using var output = new MemoryStream();
        _ = PdfDocumentProtection.Protect(document, Options(algorithm), output);
        return output.Length;
    }
}
