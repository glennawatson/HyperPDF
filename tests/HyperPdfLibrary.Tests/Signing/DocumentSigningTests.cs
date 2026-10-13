// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Tests for the library's signer: a field is added to a file or an empty one is signed, and the result validates.</summary>
public sealed class DocumentSigningTests
{
    /// <summary>The signing-certificate-v2 attribute.</summary>
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";

    /// <summary>The signing-time attribute.</summary>
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";

    /// <summary>The ESIC extension prefix.</summary>
    private const string EsicPrefix = "ESIC";

    /// <summary>A reason.</summary>
    private const string Approved = "Approved";

    /// <summary>A location.</summary>
    private const string Sydney = "Sydney";

    /// <summary>The first generated field name.</summary>
    private const string FirstField = "Signature1";

    /// <summary>The field of the sample with an empty signature field.</summary>
    private const string ApprovalField = "Approval";

    /// <summary>The most bytes a signature without a timestamp is given room for.</summary>
    private const int RoomLimit = 8_000;

    /// <summary>The most bytes left unused in the room reserved.</summary>
    private const int SlackLimit = 2_000;

    /// <summary>The width of the visible signature.</summary>
    private const float VisibleWidth = 200F;

    /// <summary>The signature flags a signed form carries.</summary>
    private const int SignedFlags = 3;

    /// <summary>The number of signatures in the two-signature file.</summary>
    private const int TwoSignatures = 2;

    /// <summary>The ESIC level of the PAdES CAdES extension.</summary>
    private const int CadesLevel = 2;

    /// <summary>The rectangle of the visible signature.</summary>
    private static readonly PdfRectangle VisibleBox = new(20, 20, 220, 90);

    /// <summary>An approval signature with PKCS #7 validates, its digest and signature are valid and the byte range covers the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ApprovalSignatureValidates()
    {
        var signed = SignBlank(new() { Reason = Approved, Location = Sydney });
        var report = SignatureFixtures.ValidateSingle(signed.Bytes);

        using (Assert.Multiple())
        {
            await Assert.That(signed.FieldName).IsEqualTo(FirstField);
            await Assert.That(signed.Format).IsEqualTo(PdfSignatureFormat.Pkcs7Detached);
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.ByteRange.IsValid).IsTrue();
            await Assert.That(report.CoversWholeDocument).IsTrue();
            await Assert.That(report.ModifiedAfterSigning).IsFalse();
            await Assert.That(report.Signature.Location).IsEqualTo(Sydney);
            await Assert.That(signed.ContentsUsed).IsLessThanOrEqualTo(signed.ContentsCapacity);
            await Assert.That(signed.Attempts).IsEqualTo(1);
        }
    }

    /// <summary>The room reserved follows the estimate and is not the old fixed 32,768 digits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReservedRoomFollowsTheEstimate()
    {
        var signed = SignBlank(PdfSigningOptions.Default);

        using (Assert.Multiple())
        {
            await Assert.That(signed.ContentsCapacity).IsLessThan(RoomLimit);
            await Assert.That(signed.ContentsCapacity - signed.ContentsUsed).IsLessThan(SlackLimit);
        }
    }

    /// <summary>A visible signature draws its appearance stream with the signer, reason and location, and the field has a description for screen readers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibleSignatureDrawsAppearance()
    {
        var appearance = new PdfSignatureAppearance { PageIndex = 0, Rectangle = VisibleBox };
        var signed = SignBlank(new() { Reason = Approved, Location = Sydney, Appearance = appearance });
        var text = SigningFixtures.AppearanceText(signed.Bytes, FirstField);
        using var document = PdfDocumentReader.Open(signed.Bytes, null);
        var field = PdfSigningFields.Find(document.Objects, FirstField)!;

        using (Assert.Multiple())
        {
            await Assert.That(text).Contains("Tj");
            await Assert.That(text).Contains("Tester Signer");
            await Assert.That(PdfText.Decode(field.Dictionary.GetStringBytes(KnownName.TU))).Contains("signed by Tester Signer");
            await Assert.That(field.Dictionary.TryGetRectangle(KnownName.Rect, out var rectangle)).IsTrue();
            await Assert.That(rectangle.Width).IsEqualTo(VisibleWidth);
            await Assert.That(SignatureFixtures.ValidateSingle(signed.Bytes).SignatureValid).IsTrue();
        }
    }

    /// <summary>An existing empty signature field is signed in place and no field is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignsExistingEmptyField()
    {
        var source = SigningFixtures.WithEmptyField(string.Empty);
        var signed = SigningFixtures.SignResult(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField, Reason = Approved });
        var report = SignatureFixtures.ValidateSingle(signed.Bytes);
        using var document = PdfDocumentReader.Open(signed.Bytes, null);

        using (Assert.Multiple())
        {
            await Assert.That(signed.FieldName).IsEqualTo(ApprovalField);
            await Assert.That(report.Signature.FieldName).IsEqualTo(ApprovalField);
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
            await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.GetInt32(KnownName.SigFlags, 0)).IsEqualTo(SignedFlags);
        }
    }

    /// <summary>A field that is already signed, and a field that does not exist, are refused with a plain reason before anything is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesSignedAndMissingFields()
    {
        var source = SigningFixtures.WithEmptyField(string.Empty);
        var once = SigningFixtures.Sign(source, SignatureFixtures.Signer, new() { FieldName = ApprovalField });
        var again = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.Sign(once, SignatureFixtures.Signer, new() { FieldName = ApprovalField }));
        var missing = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.Sign(source, SignatureFixtures.Signer, new() { FieldName = "Nothing" }));

        using (Assert.Multiple())
        {
            await Assert.That(again!.Message).Contains("already signed");
            await Assert.That(missing!.Message).Contains("no signature field named");
        }
    }

    /// <summary>A second signature by another signer keeps the first valid, and the file's first revision is untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondSignatureKeepsTheFirstValid()
    {
        var blank = SignatureFixtures.Blank();
        var first = SigningFixtures.Sign(blank, SignatureFixtures.Signer, new() { Reason = "First" });
        var second = SigningFixtures.Sign(first, SigningFixtures.SecondSigner, new() { Reason = "Second" });
        var reports = SigningFixtures.Validate(second, SignatureFixtures.Signer, SigningFixtures.SecondSigner);

        using (Assert.Multiple())
        {
            await Assert.That(second.AsSpan(0, first.Length).SequenceEqual(first)).IsTrue();
            await Assert.That(reports.Count).IsEqualTo(TwoSignatures);
            await Assert.That(reports[0].SignatureValid).IsTrue();
            await Assert.That(reports[0].DigestValid).IsTrue();
            await Assert.That(reports[0].PermittedByMdp).IsTrue();
            await Assert.That(reports[1].SignatureValid).IsTrue();
            await Assert.That(reports[1].Signature.FieldName).IsEqualTo("Signature2");
        }
    }

    /// <summary>Each digest and key pair signs, reopens and validates, with the ISO/TS 32001 and 32002 extensions recorded as needed.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="curve">The curve name, or an empty string for RSA.</param>
    /// <param name="levels">The ISO_ extension levels expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfDigestMethod.Sha256, "", new int[0])]
    [Arguments(PdfDigestMethod.Sha384, "", new int[0])]
    [Arguments(PdfDigestMethod.Sha512, "", new int[0])]
    [Arguments(PdfDigestMethod.Sha3_256, "", new[] { 32_001 })]
    [Arguments(PdfDigestMethod.Sha3_512, "", new[] { 32_001 })]
    [Arguments(PdfDigestMethod.Sha256, "nistP256", new[] { 32_002 })]
    [Arguments(PdfDigestMethod.Sha384, "nistP384", new[] { 32_002 })]
    [Arguments(PdfDigestMethod.Sha512, "nistP521", new[] { 32_002 })]
    [Arguments(PdfDigestMethod.Sha3_256, "nistP256", new[] { 32_001, 32_002 })]
    [Arguments(PdfDigestMethod.Shake256, "nistP256", new[] { 32_001, 32_002 })]
    public async Task DigestAndKeyPairsSignAndValidate(PdfDigestMethod digest, string curve, int[] levels)
    {
        Sha3SignatureValidationTests.RequireDigest(digest);
        using var ec = curve.Length == 0 ? null : SigningFixtures.CreateEc(ECCurve.CreateFromFriendlyName(curve), $"EC {curve}");
        var certificate = ec ?? SignatureFixtures.Signer;
        var file = SigningFixtures.Sign(SignatureFixtures.Blank(), certificate, new() { Digest = digest });
        var report = SigningFixtures.Validate(file, certificate)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Cms.Digest).IsEqualTo(digest);
            await Assert.That(Sha3SigningTests.IsoLevels(file)).IsEquivalentTo(levels);
            await Assert.That(Sha3SigningTests.CatalogVersion(file)).IsEqualTo(levels.Length > 0 ? "2.0" : string.Empty);
        }
    }

    /// <summary>A digest the curve may not use, and SHAKE256 with an RSA key, are refused with a plain reason before anything is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDigestsTheKeyCannotUse()
    {
        using var ec = SigningFixtures.CreateEc(ECCurve.NamedCurves.nistP384, "EC P-384");
        var pair = Assert.Throws<PdfSigningRefusedException>(() => SigningFixtures.Sign(SignatureFixtures.Blank(), ec, new() { Digest = PdfDigestMethod.Sha256 }));
        var shake = Assert.Throws<PdfSigningRefusedException>(static () => SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Digest = PdfDigestMethod.Shake256 }));
        var weak = Assert.Throws<PdfSigningRefusedException>(static () => SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Digest = PdfDigestMethod.Sha1 }));

        using (Assert.Multiple())
        {
            await Assert.That(pair!.Message).Contains("cannot sign with");
            await Assert.That(shake!.Message).Contains(Shake256.IsSupported ? "elliptic-curve" : "cannot compute the SHAKE256 digest");
            await Assert.That(weak!.Message).Contains("not used for new signatures");
        }
    }

    /// <summary>The default digest for an elliptic-curve key is the one ISO/TS 32002 pairs with its curve.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultDigestSuitsTheCurve()
    {
        using var ec = SigningFixtures.CreateEc(ECCurve.NamedCurves.nistP384, "EC P-384");
        var result = SigningFixtures.SignResult(SignatureFixtures.Blank(), ec, PdfSigningOptions.Default);

        await Assert.That(result.Digest).IsEqualTo(PdfDigestMethod.Sha384);
    }

    /// <summary>An RSA key can sign with PSS padding, and the container validates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RsaPssSigns()
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var result = PdfDocumentSigning.Sign(document, new() { SigningTime = SigningFixtures.Time }, new(SignatureFixtures.Signer, true));
        var report = SigningFixtures.Validate(result.Bytes, SignatureFixtures.Signer)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.DigestValid).IsTrue();
        }
    }

    /// <summary>A CAdES signature uses the ETSI sub-filter, carries the signing-certificate-v2 attribute and no signing time, and declares the ESIC extension in a PDF 1.7 file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CadesSignatureFollowsPades()
    {
        var signed = SignBlank(new() { Format = PdfSignatureFormat.CadesDetached });
        var report = SignatureFixtures.ValidateSingle(signed.Bytes);
        _ = SigningFixtures.SignedBytes(signed.Bytes, out var contents);
        var cms = new SignedCms();
        cms.Decode(contents);
        var attributes = cms.SignerInfos[0].SignedAttributes.Cast<CryptographicAttributeObject>().Select(static attribute => attribute.Oid.Value).ToArray();
        using var document = PdfDocumentReader.Open(signed.Bytes, null);

        using (Assert.Multiple())
        {
            await Assert.That(report.Signature.Format).IsEqualTo(PdfSignatureFormat.CadesDetached);
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(attributes).Contains(SigningCertificateV2Oid);
            await Assert.That(attributes).DoesNotContain(SigningTimeOid);
            await Assert.That(PdfDocumentExtensionEditing.GetExtensionLevel(document, EsicPrefix)).IsEqualTo(CadesLevel);
            await Assert.That(report.RecordedTime).IsNotNull();
        }
    }

    /// <summary>A PKCS #7 signature carries the signing time and the signing-certificate-v2 attribute.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pkcs7SignatureKeepsTheSigningTime()
    {
        var signed = SignBlank(PdfSigningOptions.Default);
        _ = SigningFixtures.SignedBytes(signed.Bytes, out var contents);
        var cms = new SignedCms();
        cms.Decode(contents);
        var attributes = cms.SignerInfos[0].SignedAttributes.Cast<CryptographicAttributeObject>().Select(static a => a.Oid.Value).ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(attributes).Contains(SigningTimeOid);
            await Assert.That(attributes).Contains(SigningCertificateV2Oid);
        }
    }

    /// <summary>A CAdES signature made with SHA3-256 raises the file to PDF 2.0 and does not declare the ESIC extension.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CadesInAPdf20FileHasNoEsicExtension()
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Sha3_256);
        var signed = SignBlank(new() { Format = PdfSignatureFormat.CadesDetached, Digest = PdfDigestMethod.Sha3_256 });
        using var document = PdfDocumentReader.Open(signed.Bytes, null);

        using (Assert.Multiple())
        {
            await Assert.That(PdfDocumentExtensionEditing.GetExtensionLevel(document, EsicPrefix)).IsEqualTo(0);
            await Assert.That(Sha3SigningTests.CatalogVersion(signed.Bytes)).IsEqualTo("2.0");
            await Assert.That(SignatureFixtures.ValidateSingle(signed.Bytes).SignatureValid).IsTrue();
        }
    }

    /// <summary>The preview names the field, page, format, digest and what a certification allows, and lists the reasons signing would be refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewDescribesWhatWouldHappen()
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var good = PdfDocumentSigning.Preview(document, new() { Certification = HyperPdfLibrary.Signatures.PdfMdpPermission.FormFillAndSign, Reason = Approved });
        var bad = PdfDocumentSigning.Preview(document, new() { FieldName = "Nothing" });

        using (Assert.Multiple())
        {
            await Assert.That(good.CanSign).IsTrue();
            await Assert.That(good.FieldName).IsEqualTo(FirstField);
            await Assert.That(good.IsNewField).IsTrue();
            await Assert.That(good.PageNumber).IsEqualTo(1);
            await Assert.That(good.Digest).IsEqualTo(PdfDigestMethod.Sha256);
            await Assert.That(good.LaterChanges).Contains("fill in form fields");
            await Assert.That(bad.CanSign).IsFalse();
            await Assert.That(bad.Problems[0]).Contains("no signature field");
        }
    }

    /// <summary>The document is left as it was after signing, so it can be signed again or saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SigningLeavesTheDocumentUnchanged()
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var signer = new PdfCertificateSigner(SignatureFixtures.Signer);
        var first = PdfDocumentSigning.Sign(document, new() { SigningTime = SigningFixtures.Time }, signer);
        var second = PdfDocumentSigning.Sign(document, new() { SigningTime = SigningFixtures.Time }, signer);

        using (Assert.Multiple())
        {
            await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
            await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(0);
            await Assert.That(first.FieldName).IsEqualTo(second.FieldName);
            await Assert.That(first.ContentsCapacity).IsEqualTo(second.ContentsCapacity);
        }
    }

    /// <summary>Signs a blank one-page file with the shared RSA certificate.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The result.</returns>
    internal static PdfSigningResult SignBlank(PdfSigningOptions options) => SigningFixtures.SignResult(SignatureFixtures.Blank(), SignatureFixtures.Signer, options);
}
