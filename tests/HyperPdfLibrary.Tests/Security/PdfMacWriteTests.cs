// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.Cms;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests that the writers give every saved revision of an encrypted document a fresh PDF MAC (ISO/TS 32004:2024), or stop the save.</summary>
public sealed class PdfMacWriteTests
{
    /// <summary>The ordinary digest.</summary>
    private const string Sha256 = MacToken.Sha256;

    /// <summary>The words the messages of a save that cannot keep a PDF MAC share.</summary>
    private const string MacWords = "PDF MAC";

    /// <summary>The number of tokens in a file with one revision.</summary>
    private const int OneToken = 1;

    /// <summary>The number of tokens in a file with two revisions.</summary>
    private const int TwoTokens = 2;

    /// <summary>The number of tokens in a file with a signed revision between two unsigned ones.</summary>
    private const int ThreeTokens = 3;

    /// <summary>The index of the second revision.</summary>
    private const int SecondRevision = 1;

    /// <summary>The index of the third revision.</summary>
    private const int ThirdRevision = 2;

    /// <summary>The digest of a tiny signature value.</summary>
    private const byte TinySignature = 4;

    /// <summary>Gets the signer, made once because RSA key generation is slow.</summary>
    private static X509Certificate2 Signer { get; } = PdfSigning.CreateCertificate("Mac Write Signer", null, false, TimeProvider.System);

    /// <summary>Gets the fixed bytes some tests digest.</summary>
    private static byte[] Sample { get; } = [1, TwoTokens, ThreeTokens];

    /// <summary>An incremental save writes a new revision with its own valid token, and leaves the original bytes and token alone.</summary>
    /// <param name="gcm">Whether the file uses AES-GCM.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IncrementalSaveWritesFreshToken(bool gcm)
    {
        RequireGcm(gcm);
        var protection = MacProtection.Standard(gcm, MacProtection.MacRequired);
        var original = MacPdf.Create(protection, Sha256, true);
        using var source =StoreOpening.Open(original, AesGcmPdf.UserPassword);
        Edit(source);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        PdfIncrementalWriter.Save(source, output, report);
        var saved = output.ToArray();

        await Assert.That(saved[..original.Length]).IsEquivalentTo(original);
        await Assert.That(report.PdfMacWritten).IsTrue();
        await AssertAllTokensHold(saved, protection, TwoTokens);
        await AssertLibraryAgrees(saved);
    }

    /// <summary>An incremental save of a file whose last revision uses a cross-reference stream writes a stream revision with a valid token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveToCrossReferenceStreamWritesToken()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        using var first =StoreOpening.Open(MacPdf.Create(protection, Sha256, true), AesGcmPdf.UserPassword);
        var streamed = PdfCompactWriter.Save(first, PdfCompactOptions.Default);
        await Assert.That(Encoding.Latin1.GetString(streamed)).Contains("/Type /XRef");
        using var source =StoreOpening.Open(streamed, AesGcmPdf.UserPassword);
        Edit(source);
        var saved = PdfIncrementalWriter.Save(source);

        await AssertAllTokensHold(saved, protection, TwoTokens);
        await AssertLibraryAgrees(saved);
    }

    /// <summary>A compact save writes one token that covers the whole file, with a classic table or a cross-reference stream.</summary>
    /// <param name="useObjectStreams">Whether the file uses object streams.</param>
    /// <param name="gcm">Whether the file uses AES-GCM.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task CompactSaveWritesToken(bool useObjectStreams, bool gcm)
    {
        RequireGcm(gcm);
        var protection = MacProtection.Standard(gcm, MacProtection.MacRequired);
        using var source =StoreOpening.Open(MacPdf.Create(protection, Sha256, true), AesGcmPdf.UserPassword);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        PdfCompactWriter.Save(source, new(useObjectStreams, false), output, report);
        var saved = output.ToArray();

        await Assert.That(Encoding.ASCII.GetString(saved, 0, "%PDF-2.0".Length)).IsEqualTo("%PDF-2.0");
        await AssertAllTokensHold(saved, protection, OneToken);
        await AssertLibraryAgrees(saved);
        using var reopened =StoreOpening.Open(saved, AesGcmPdf.UserPassword);
        await Assert.That(PdfMacExtension.IsDeclared(reopened.Catalog, reopened.Names)).IsTrue();
        await Assert.That(reopened.Trailer.GetDictionary(KnownName.Encrypt)!.GetStringBytes(reopened.Names.Intern("KDFSalt")).ToArray()).IsEquivalentTo(MacProtection.Salt);
    }

    /// <summary>The asynchronous saves write valid tokens too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsynchronousSavesWriteTokens()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        using var source =StoreOpening.Open(MacPdf.Create(protection, Sha256, true), AesGcmPdf.UserPassword);
        Edit(source);
        await using var incremental = new MemoryStream();
        await PdfIncrementalWriter.SaveAsync(source, incremental, CancellationToken.None);
        await using var compact = new MemoryStream();
        await PdfCompactWriter.SaveAsync(source, PdfCompactOptions.Default, compact, CancellationToken.None);

        await AssertAllTokensHold(incremental.ToArray(), protection, TwoTokens);
        await AssertAllTokensHold(compact.ToArray(), protection, OneToken);
    }

    /// <summary>A document that is not changed is saved as its original bytes, so its token stays valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnchangedDocumentKeepsItsBytes()
    {
        var original = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true);
        using var source =StoreOpening.Open(original, AesGcmPdf.UserPassword);

        await Assert.That(PdfIncrementalWriter.Save(source)).IsEquivalentTo(original);
    }

    /// <summary>A stale token that an unaware program left in the trailer is not copied into the new revision.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StaleTokenIsNotCarriedForward()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var stale = MacPdf.AddUnsignedRevision(MacPdf.Create(protection, Sha256, true), protection, "Unaware update", false);
        using var source =StoreOpening.Open(stale, AesGcmPdf.UserPassword);
        Edit(source);
        var saved = PdfIncrementalWriter.Save(source);
        var checks = MacChecker.Check(saved, protection.FileKey, MacProtection.Salt);

        await Assert.That(checks.Count).IsEqualTo(ThreeTokens);
        await Assert.That(checks[0].Holds).IsTrue();
        await Assert.That(checks[SecondRevision].Holds).IsFalse();
        await Assert.That(checks[ThirdRevision].Holds).IsTrue();
        using var document = PdfDocumentReader.Open(saved, AesGcmPdf.UserPassword);
        var report = PdfDocumentMac.GetIntegrity(document);
        await Assert.That(report.Revisions[ThirdRevision].Status).IsEqualTo(PdfMacStatus.Valid);
        await Assert.That(report.Revisions[SecondRevision].Status).IsEqualTo(PdfMacStatus.Invalid);
    }

    /// <summary>Changing a byte of saved output makes its token fail, in the library's check and in the independent one.</summary>
    /// <param name="incremental">Whether the file was saved incrementally.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TamperedSaveFailsValidation(bool incremental)
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        using var source =StoreOpening.Open(MacPdf.Create(protection, Sha256, true), AesGcmPdf.UserPassword);
        Edit(source);
        var saved = SaveBytes(source, incremental);
        var digit = saved.AsSpan().LastIndexOf("/Subject <"u8) + "/Subject <"u8.Length;
        saved[digit] = (byte)(saved[digit] == '0' ? '1' : '0');
        using var document = PdfDocumentReader.Open(saved, AesGcmPdf.UserPassword);

        await Assert.That(PdfDocumentMac.GetIntegrity(document).Status).IsEqualTo(PdfMacStatus.Invalid);
        await Assert.That(MacChecker.Check(saved, protection.FileKey, MacProtection.Salt).Any(static check => !check.Holds)).IsTrue();
    }

    /// <summary>A save declares the PDF MAC extension when the file lacks it, and raises an older header to 2.0.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SaveAddsMissingExtension(bool incremental)
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var original = MacPdf.Create(protection, Sha256, true);
        "/ExtensionLevel 32005"u8.CopyTo(original.AsSpan(MacPdf.Find(original, "/ExtensionLevel 32004")));
        using var source =StoreOpening.Open(original, AesGcmPdf.UserPassword);
        await Assert.That(PdfMacExtension.IsDeclared(source.Catalog, source.Names)).IsFalse();
        if (incremental)
        {
            Edit(source);
        }

        var saved = SaveBytes(source, incremental);
        using var reopened =StoreOpening.Open(saved, AesGcmPdf.UserPassword);
        var checks = MacChecker.Check(saved, protection.FileKey, MacProtection.Salt);

        await Assert.That(PdfMacExtension.IsDeclared(reopened.Catalog, reopened.Names)).IsTrue();
        await Assert.That(checks[^1].Holds).IsTrue();
    }

    /// <summary>A document that requires a PDF MAC but has no usable salt stops the save with a clear message.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SaveStopsWhenNoValidTokenCanBeMade(bool incremental)
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var noSalt = protection with { EncryptText = protection.EncryptText.Replace($"/KDFSalt <{Convert.ToHexString(MacProtection.Salt)}>", string.Empty, StringComparison.Ordinal) };
        using var source =StoreOpening.Open(MacPdf.Create(noSalt, Sha256, false), AesGcmPdf.UserPassword);
        Edit(source);
        var exception = await Assert.That(() => SaveBytes(source, incremental)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Security);
        await Assert.That(exception.Message).Contains("KDFSalt");
        await Assert.That(exception.Message).Contains("not saved");
    }

    /// <summary>Linearizing and optimizing cannot place a PDF MAC, so they stop with a clear message; removing encryption lets the optimizer run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizingAndOptimizingStop()
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true);
        using var document = SkiaTestRegistration.Open(file, AesGcmPdf.UserPassword);
        var linearized = await Assert.That(() => PdfLinearizedWriter.Save(document)).Throws<PdfException>();
        await using var output = new MemoryStream();
        var optimized = await Assert.That(() => PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.Balanced)).Throws<PdfException>();

        await Assert.That(linearized!.Message).Contains(MacWords);
        await Assert.That(optimized!.Message).Contains(MacWords);
        await using var plain = new MemoryStream();
        _ = PdfOptimizer.Optimize(document, plain, PdfOptimizeOptions.Balanced with { RemoveEncryption = true });
        await Assert.That(plain.Length).IsGreaterThan(0);
    }

    /// <summary>Reproducible output cannot keep a PDF MAC (its token and AES use random values), so it stops and says so; decrypted output is allowed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReproducibleSaveStopsForPdfMacDocument()
    {
        using var source = StoreOpening.Open(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), Sha256, true), AesGcmPdf.UserPassword);
        var reproducible = PdfCompactOptions.Default with { Reproducible = true };
        var exception = await Assert.That(() => PdfCompactWriter.Save(source, reproducible)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Security);
        await Assert.That(exception.Message).Contains(MacWords);
        await Assert.That(PdfCompactWriter.Save(source, reproducible with { RemoveEncryption = true })).IsNotEmpty();
    }

    /// <summary>An ordinary encrypted save is unchanged: no token, no extension, no report.</summary>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OrdinaryEncryptedSaveHasNoToken(bool incremental)
    {
        using var source =StoreOpening.Open(MacPdf.Create(MacProtection.Standard(false, MacProtection.MacNotRequired), Sha256, false), AesGcmPdf.UserPassword);
        Edit(source);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        if (incremental)
        {
            PdfIncrementalWriter.Save(source, output, report);
        }
        else
        {
            PdfCompactWriter.Save(source, PdfCompactOptions.Classic, output, report);
        }

        await Assert.That(Encoding.Latin1.GetString(output.ToArray())).DoesNotContain("AuthCode");
        await Assert.That(report.PdfMacWritten).IsFalse();
        await Assert.That(report.Failure).IsNull();
    }

    /// <summary>A file with a signed revision keeps that revision's attached token and signature, and the save adds an unsigned revision with its own token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveAfterSignedRevisionKeepsEveryTokenValid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var signed = MacPdf.AddSignedRevision(MacPdf.Create(protection, Sha256, true), protection, Signer, true);
        using var source =StoreOpening.Open(signed, AesGcmPdf.UserPassword);
        Edit(source);
        var saved = PdfIncrementalWriter.Save(source);

        await Assert.That(saved[..signed.Length]).IsEquivalentTo(signed);
        await AssertAllTokensHold(saved, protection, ThreeTokens);
        await AssertLibraryAgrees(saved);
        await Assert.That(VerifiesSignature(signed)).IsTrue();
        _ = await Assert.That(() => PdfCompactWriter.Save(source, PdfCompactOptions.Classic)).Throws<PdfException>();
    }

    /// <summary>The writer's token for a signature goes in as an unsigned attribute and leaves the signature and its coverage valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachedTokenLeavesSignatureValid()
    {
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var plain = MacPdf.AddSignedRevision(MacPdf.Create(protection, Sha256, true), protection, Signer, false);
        var contents = MacPdf.Find(plain, MacPdf.ContentsMarker) + MacPdf.ContentsMarker.Length;
        var container = Convert.FromHexString(Encoding.ASCII.GetString(plain, contents, MacPdf.SignatureDigits)).AsSpan().TrimEnd((byte)0).ToArray();
        var cms = new SignedCms();
        cms.Decode(container);
        byte[] covered = [.. plain.AsSpan(0, contents - 1), .. plain.AsSpan(contents + MacPdf.SignatureDigits + 1)];
        var token = PdfMacTokenWriter.Create(
            protection.FileKey,
            MacProtection.Salt,
            CmsDigestAlgorithm.Sha256,
            MacToken.Hash(Sha256, covered),
            MacToken.Hash(Sha256, cms.SignerInfos[0].GetSignature()));
        var attached = PdfMacSignatureAttachment.Attach(container, token);
        var file = plain.ToArray();
        Encoding.ASCII.GetBytes(Convert.ToHexString(attached).PadRight(MacPdf.SignatureDigits, '0')).CopyTo(file, contents);
        var lookup = PdfMacSignature.Find(attached, out var found, out _);

        await Assert.That(lookup).IsEqualTo(PdfMacSignatureLookup.Found);
        await Assert.That(attached[found]).IsEquivalentTo(token);
        await Assert.That(VerifiesSignature(file)).IsTrue();
        await AssertAllTokensHold(file, protection, TwoTokens);
        await AssertLibraryAgrees(file);
    }

    /// <summary>Tokens made by the writer for every digest of Table 8 hold under the independent check, and their length is known beforehand.</summary>
    /// <param name="digest">The digest name.</param>
    /// <param name="withSignature">Whether the token carries a signature digest.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("SHA-256", false)]
    [Arguments("SHA-384", false)]
    [Arguments("SHA-512", true)]
    [Arguments("SHA3-256", false)]
    [Arguments("SHA3-384", true)]
    [Arguments("SHA3-512", false)]
    public async Task WriterTokensHoldForEveryDigest(string digest, bool withSignature)
    {
        if (digest.StartsWith("SHA3", StringComparison.Ordinal) && !SHA3_256.IsSupported)
        {
            Skip.Test("SHA-3 is not available on this platform.");
        }

        var algorithm = CmsDigests.FromOid(MacToken.AlgorithmIdentifier(digest));
        var data = MacToken.Hash(digest, Sample);
        byte[] signature = [TinySignature];
        var key = AesGcmPdf.FileKey;
        var token = PdfMacTokenWriter.Create(key, MacProtection.Salt, algorithm, data, withSignature ? MacToken.Hash(digest, signature) : default);

        await Assert.That(token.Length).IsEqualTo(PdfMacTokenWriter.Length(algorithm, withSignature));
        await Assert.That(MacChecker.VerifyToken(token, key, MacProtection.Salt, Sample, withSignature ? signature : null)).IsEqualTo(string.Empty);
        await Assert.That(PdfMacTokenReader.Verify(token, key, MacProtection.Salt).Status).IsEqualTo(PdfMacTokenStatus.Valid);
    }

    /// <summary>Each token has its own random MAC key, so two tokens over the same bytes differ.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TokensUseFreshMacKeys()
    {
        var data = MacToken.Hash(Sha256, Sample);
        var first = PdfMacTokenWriter.Create(AesGcmPdf.FileKey, MacProtection.Salt, CmsDigestAlgorithm.Sha256, data, default);
        var second = PdfMacTokenWriter.Create(AesGcmPdf.FileKey, MacProtection.Salt, CmsDigestAlgorithm.Sha256, data, default);

        await Assert.That(first).IsNotEquivalentTo(second);
    }

    /// <summary>A token cannot be made with a short salt or a digest of the wrong size, or with a digest the standard does not allow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadInputsAreRefused()
    {
        var data = MacToken.Hash(Sha256, Sample);

        _ = await Assert.That(() => PdfMacTokenWriter.Create(AesGcmPdf.FileKey, MacProtection.Salt.AsSpan(1), CmsDigestAlgorithm.Sha256, data, default)).Throws<ArgumentException>();
        _ = await Assert.That(() => PdfMacTokenWriter.Create(AesGcmPdf.FileKey, MacProtection.Salt, CmsDigestAlgorithm.Sha256, data.AsSpan(1), default)).Throws<ArgumentException>();
        _ = await Assert.That(() => PdfMacTokenWriter.Create(AesGcmPdf.FileKey, MacProtection.Salt, CmsDigestAlgorithm.Unknown, data, default)).Throws<ArgumentException>();
    }

    /// <summary>
    /// Writes the builder's files and the library's saved files to the folder named by <c>HYPERPDF_MAC_OUTPUT</c>, for checking
    /// with other PDF tools. It does nothing when the variable is not set.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesFilesForOtherTools()
    {
        var folder = Environment.GetEnvironmentVariable("HYPERPDF_MAC_OUTPUT");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        _ = Directory.CreateDirectory(folder);
        var protection = MacProtection.Standard(false, MacProtection.MacRequired);
        var builder = MacPdf.Create(protection, Sha256, true);
        var signed = MacPdf.AddSignedRevision(builder, protection, Signer, true);
        await File.WriteAllBytesAsync(Path.Combine(folder, "builder-unsigned.pdf"), builder);
        await File.WriteAllBytesAsync(Path.Combine(folder, "builder-signed.pdf"), signed);
        using var source = StoreOpening.Open(signed, AesGcmPdf.UserPassword);
        Edit(source);
        await File.WriteAllBytesAsync(Path.Combine(folder, "saved-incremental-after-signed.pdf"), PdfIncrementalWriter.Save(source));
        using var plain = StoreOpening.Open(builder, AesGcmPdf.UserPassword);
        Edit(plain);
        await File.WriteAllBytesAsync(Path.Combine(folder, "saved-incremental.pdf"), PdfIncrementalWriter.Save(plain));
        await File.WriteAllBytesAsync(Path.Combine(folder, "saved-compact-classic.pdf"), PdfCompactWriter.Save(plain, PdfCompactOptions.Classic));
        await File.WriteAllBytesAsync(Path.Combine(folder, "saved-compact-object-streams.pdf"), PdfCompactWriter.Save(plain, PdfCompactOptions.Default));
    }

    /// <summary>Skips a test on a platform without AES-GCM.</summary>
    /// <param name="gcm">Whether the test uses AES-GCM.</param>
    private static void RequireGcm(bool gcm)
    {
        if (gcm && !AesGcm.IsSupported)
        {
            Skip.Test("AES-GCM is not available on this platform.");
        }
    }

    /// <summary>Saves a document incrementally or compactly into an array.</summary>
    /// <param name="source">The document's objects.</param>
    /// <param name="incremental">Whether to save incrementally.</param>
    /// <returns>The saved file.</returns>
    private static byte[] SaveBytes(PdfObjectStore source, bool incremental) =>
        incremental ? PdfIncrementalWriter.Save(source) : PdfCompactWriter.Save(source, PdfCompactOptions.Classic);

    /// <summary>Changes the document information dictionary, so a save has something to write.</summary>
    /// <param name="source">The document's objects.</param>
    private static void Edit(PdfObjectStore source)
    {
        var info = source.Trailer.GetDictionary(KnownName.Info)!.Clone();
        info.Set(source.Names.Intern("Subject"), PdfValue.FromString("edited subject"u8.ToArray()));
        StoreEditing.Replace(source, new(MacPdf.InfoNumber, 0), PdfValue.FromDictionary(info));
    }

    /// <summary>Checks, independently of the library, that a saved file has the tokens it should and that each holds.</summary>
    /// <param name="saved">The saved file.</param>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="count">The tokens the file should have.</param>
    /// <returns>A task.</returns>
    private static async Task AssertAllTokensHold(byte[] saved, MacProtection protection, int count)
    {
        var checks = MacChecker.Check(saved, protection.FileKey, MacProtection.Salt);

        await Assert.That(checks.Count).IsEqualTo(count);
        await Assert.That(string.Concat(checks.Select(static check => check.Failure))).IsEqualTo(string.Empty);
    }

    /// <summary>Checks that the library's reader finds every revision valid.</summary>
    /// <param name="saved">The saved file.</param>
    /// <returns>A task.</returns>
    private static async Task AssertLibraryAgrees(byte[] saved)
    {
        using var document = PdfDocumentReader.Open(saved, AesGcmPdf.UserPassword);
        var report = PdfDocumentMac.GetIntegrity(document);

        await Assert.That(string.Concat(report.Revisions.Where(static revision => revision.Status != PdfMacStatus.Valid).Select(static revision => revision.Message))).IsEqualTo(string.Empty);
        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
    }

    /// <summary>Verifies the signature of the first signed revision of a file as a CMS signature over its byte range.</summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> when the signature checks.</returns>
    private static bool VerifiesSignature(byte[] file)
    {
        var contents = MacPdf.Find(file, MacPdf.ContentsMarker) + MacPdf.ContentsMarker.Length;
        var container = Convert.FromHexString(Encoding.ASCII.GetString(file, contents, MacPdf.SignatureDigits)).AsSpan().TrimEnd((byte)0).ToArray();
        var after = contents + MacPdf.SignatureDigits + 1;
        byte[] covered = [.. file.AsSpan(0, contents - 1), .. file.AsSpan(after, MacPdf.SignedEnd(file) - after)];
        var cms = new SignedCms(new(covered), true);
        cms.Decode(container);
        cms.CheckSignature(true);
        return true;
    }
}
