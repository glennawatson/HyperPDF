// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>
/// Tests that signing honours certification (DocMDP, ISO 32000-2, 12.8.2.2), field locks (12.8.2.4) and the seed values of a
/// signature field (12.7.5.5, Table 237).
/// </summary>
public sealed class SigningRulesTests
{
    /// <summary>The name of the empty signature field of the sample form.</summary>
    private const string SampleField = "Signature1";

    /// <summary>The name of the empty signature field of the field sample.</summary>
    private const string ApprovalField = "Approval";

    /// <summary>The signatures in a file that was signed twice.</summary>
    private const int TwoSignatures = 2;

    /// <summary>The /Ff bit that makes the sub-filter a required constraint.</summary>
    private const int SubFilterRequired = 0x2;

    /// <summary>The /Ff bit that makes the digest method a required constraint.</summary>
    private const int DigestRequired = 0x40;

    /// <summary>The /Ff bit that makes the signature handler a required constraint.</summary>
    private const int FilterRequired = 0x1;

    /// <summary>A certified file keeps its certification valid for the changes it allows, and flags the ones it does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertifiedFileFlagsOnlyChangesItDoesNotAllow()
    {
        var certified = SigningFixtures.Sign(SignatureSamples.Unsigned(), SignatureFixtures.Signer, new() { FieldName = SampleField, Certification = PdfMdpPermission.FormFillAndSign });
        var filled = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(certified));
        var commented = SignatureFixtures.ValidateSingle(SignatureSamples.AddAnnotation(certified));
        var rotated = SignatureFixtures.ValidateSingle(SignatureSamples.ChangePage(certified));

        using (Assert.Multiple())
        {
            await Assert.That(filled.Signature.IsCertification).IsTrue();
            await Assert.That(filled.Signature.DocMdp).IsEqualTo(PdfMdpPermission.FormFillAndSign);
            await Assert.That(filled.SignatureValid).IsTrue();
            await Assert.That(filled.PermittedByMdp).IsTrue();
            await Assert.That(commented.PermittedByMdp).IsFalse();
            await Assert.That(rotated.PermittedByMdp).IsFalse();
        }
    }

    /// <summary>A certification that allows annotations allows a comment but still flags a page change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationCertificationAllowsComments()
    {
        var certified = SigningFixtures.Sign(SignatureSamples.Unsigned(), SignatureFixtures.Signer, new() { FieldName = SampleField, Certification = PdfMdpPermission.AnnotateFormFillAndSign });
        var commented = SignatureFixtures.ValidateSingle(SignatureSamples.AddAnnotation(certified));
        var rotated = SignatureFixtures.ValidateSingle(SignatureSamples.ChangePage(certified));

        using (Assert.Multiple())
        {
            await Assert.That(commented.PermittedByMdp).IsTrue();
            await Assert.That(rotated.PermittedByMdp).IsFalse();
        }
    }

    /// <summary>A file certified for form filling and signing takes a second signature, and the certification stays valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertifiedFileTakesLaterSignatures()
    {
        var certified = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Certification = PdfMdpPermission.FormFillAndSign });
        var twice = SigningFixtures.Sign(certified, SigningFixtures.SecondSigner, new() { Reason = "Countersigned" });
        var reports = SigningFixtures.Validate(twice, SignatureFixtures.Signer, SigningFixtures.SecondSigner);

        using (Assert.Multiple())
        {
            await Assert.That(reports.Count).IsEqualTo(TwoSignatures);
            await Assert.That(reports[0].SignatureValid).IsTrue();
            await Assert.That(reports[0].PermittedByMdp).IsTrue();
            await Assert.That(reports[1].SignatureValid).IsTrue();
        }
    }

    /// <summary>A file certified for no changes cannot be signed again, and the reason is plain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoChangesCertificationRefusesAnotherSignature()
    {
        var certified = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Certification = PdfMdpPermission.NoChanges });
        var refused = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.Sign(certified, SigningFixtures.SecondSigner, new()));

        await Assert.That(refused!.Message).Contains("allows no further changes");
    }

    /// <summary>A certification signature must come first, so a file that already has a signature refuses it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertificationMustBeTheFirstSignature()
    {
        var approved = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new());
        var refused = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.Sign(approved, SigningFixtures.SecondSigner, new() { Certification = PdfMdpPermission.FormFillAndSign }));

        await Assert.That(refused!.Message).Contains("must be the first signature");
    }

    /// <summary>A signature that locks the document makes a later change to a field invalid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LockedDocumentFlagsALaterFormFill()
    {
        var locked = SigningFixtures.Sign(SignatureSamples.Unsigned(), SignatureFixtures.Signer, new() { FieldName = SampleField, LockDocument = true });
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(locked));

        using (Assert.Multiple())
        {
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.PermittedByMdp).IsFalse();
        }
    }

    /// <summary>A field locked by an earlier signature's /Lock cannot be signed, and the reason names the field.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LockedFieldRefusesItsSignature()
    {
        var source = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] /SigFlags 0 >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << >> /Annots [4 0 R 5 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Sig /T (First) /Rect [20 20 200 80] /F 4 /P 3 0 R /Lock << /Type /SigFieldLock /Action /Include /Fields [(Second)] >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Second) /Rect [20 100 200 160] /F 4 /P 3 0 R >>");
        var first = SigningFixtures.Sign(source, SignatureFixtures.Signer, new() { FieldName = "First" });
        var refused = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.Sign(first, SigningFixtures.SecondSigner, new() { FieldName = "Second" }));

        await Assert.That(refused!.Message).Contains("locked the field \"Second\"");
    }

    /// <summary>A required sub-filter in the field's seed values decides the format when none is asked for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedSubFilterChoosesTheFormat()
    {
        var source = Seeded($"<< /Type /SV /Ff {SubFilterRequired} /SubFilter [/ETSI.CAdES.detached] >>");
        var signed = SigningFixtures.SignResult(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField });

        await Assert.That(signed.Format).IsEqualTo(PdfSignatureFormat.CadesDetached);
    }

    /// <summary>A required sub-filter that the caller contradicts is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedSubFilterRefusesAnotherFormat()
    {
        var source = Seeded($"<< /Type /SV /Ff {SubFilterRequired} /SubFilter [/ETSI.CAdES.detached] >>");
        var options = new PdfSigningOptions { FieldName = ApprovalField, Format = PdfSignatureFormat.Pkcs7Detached };
        var refused = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.SignResult(source, SignatureFixtures.Signer, options));

        await Assert.That(refused!.Reasons.Count).IsGreaterThan(0);
    }

    /// <summary>A required digest in the field's seed values decides the digest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedDigestChoosesTheDigest()
    {
        var source = Seeded($"<< /Type /SV /Ff {DigestRequired} /DigestMethod [/SHA384] >>");
        var signed = SigningFixtures.SignResult(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField });

        await Assert.That(signed.Digest).IsEqualTo(PdfDigestMethod.Sha384);
    }

    /// <summary>A required signature handler the library does not provide is refused with its name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedHandlerThatIsNotProvidedIsRefused()
    {
        var source = Seeded($"<< /Type /SV /Ff {FilterRequired} /Filter /Entrust.PPKEF >>");
        var refused = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.SignResult(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField }));

        await Assert.That(refused!.Message).Contains("Entrust.PPKEF");
    }

    /// <summary>A required timestamp is refused without a timestamp client and done with one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedTimestampNeedsAClient()
    {
        var source = Seeded("<< /Type /SV /TimeStamp << /URL (http://timestamps.example.test) /Ff 1 >> >>");
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var refused = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.SignResult(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField }));
        var signed = await SigningFixtures.SignResultAsync(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField, Timestamp = new TimestamperClient(authority) });

        using (Assert.Multiple())
        {
            await Assert.That(refused!.Message).Contains("requires a timestamp");
            await Assert.That(SignatureFixtures.Validate(signed.Bytes, SignatureFixtures.Signer)[0].HasTimestamp).IsTrue();
        }
    }

    /// <summary>The preview names every seed value that would refuse the signature, without writing anything.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewListsSeedValueProblems()
    {
        var source = Seeded($"<< /Type /SV /Ff {FilterRequired} /Filter /Entrust.PPKEF >>");
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(source, null);
        var preview = PdfDocumentSigning.Preview(document, new() { FieldName = ApprovalField });

        using (Assert.Multiple())
        {
            await Assert.That(preview.CanSign).IsFalse();
            await Assert.That(preview.Problems[0]).Contains("Entrust.PPKEF");
        }
    }

    /// <summary>Builds a file with an empty signature field whose seed values are the given dictionary.</summary>
    /// <param name="seed">The seed value dictionary.</param>
    /// <returns>The file.</returns>
    private static byte[] Seeded(string seed) => SigningFixtures.WithEmptyField("/SV 5 0 R", seed);
}
