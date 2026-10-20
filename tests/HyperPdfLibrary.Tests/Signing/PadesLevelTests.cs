// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Checks the PAdES baseline level report.</summary>
public sealed class PadesLevelTests
{
    /// <summary>Placeholder revocation list bytes; the report counts stored data and does not parse it.</summary>
    private static readonly byte[] StoredCrl = [0x30, 0x03, 0x02, 0x01, 0x00];

    /// <summary>A CAdES signature with the required attributes reaches the basic level, and the next level's gap is listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CadesSignatureReachesBaselineB()
    {
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Format = PdfSignatureFormat.CadesDetached });
        using var document = PdfDocumentReader.Open(signed, null);

        var report = PdfPadesReader.Read(document).Single();

        using (Assert.Multiple())
        {
            await Assert.That(report.Level).IsEqualTo(PdfPadesLevel.BaselineB);
            await Assert.That(report.IsCryptographicallyValid).IsTrue();
            await Assert.That(report.WhyNotNextLevel()[0].Rule).IsEqualTo("TimeStamp");
            await Assert.That(report.NotPerformed.Length).IsGreaterThan(0);
        }
    }

    /// <summary>A plain PKCS#7 signature does not reach any baseline level.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pkcs7SignatureHasNoLevel()
    {
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Format = PdfSignatureFormat.Pkcs7Detached });
        using var document = PdfDocumentReader.Open(signed, null);

        var report = PdfPadesReader.Read(document).Single();

        await Assert.That(report.Level).IsEqualTo(PdfPadesLevel.None);
        await Assert.That(report.IsCryptographicallyValid).IsTrue();
    }

    /// <summary>A signature time stamp lifts the signature to the time stamp level.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TimestampReachesBaselineT(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var options = new PdfSigningOptions { Format = PdfSignatureFormat.CadesDetached, Timestamp = new TimestamperClient(authority) };
        var signed = await SigningFixtures.SignResultAsync(SignatureFixtures.Blank(), SignatureFixtures.Signer, options);
        using var document = PdfDocumentReader.Open(signed.Bytes, null);

        var report = PdfPadesReader.Read(document).Single();

        await Assert.That(report.Level).IsEqualTo(PdfPadesLevel.BaselineT);
        await Assert.That(report.WhyNotNextLevel()[0].Level).IsEqualTo(PdfPadesLevel.BaselineLt);
    }

    /// <summary>A later document time stamp counts as the time stamp, and covers the whole file.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentTimestampCountsAsEvidence(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Format = PdfSignatureFormat.CadesDetached });
        using var document = PdfDocumentReader.Open(signed, null);
        var stamped = await PdfDocumentSigning.AddDocumentTimestampAsync(document, new() { Timestamp = new TimestamperClient(authority) }, cancellationToken);
        using var reopened = PdfDocumentReader.Open(stamped.Bytes, null);

        var reports = PdfPadesReader.Read(reopened);

        await Assert.That(reports.Length).IsEqualTo(1);
        await Assert.That(reports[0].Level).IsEqualTo(PdfPadesLevel.BaselineT);
        await Assert.That(Array.Find(reports[0].Findings, static f => f.Rule == "ArchiveTimeStamp").Passed).IsTrue();
    }

    /// <summary>Stored validation data lifts a time stamped signature to the long-term level, and a later document time stamp to the archive level.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationDataAndArchiveStampReachBaselineLta(CancellationToken cancellationToken)
    {
        using var authority = new TestTimestampAuthority(SigningFixtures.Time);
        var options = new PdfSigningOptions { Format = PdfSignatureFormat.CadesDetached, Timestamp = new TimestamperClient(authority) };
        var signed = await SigningFixtures.SignResultAsync(SignatureFixtures.Blank(), SignatureFixtures.Signer, options);
        using var signedDocument = PdfDocumentReader.Open(signed.Bytes, null);
        var contents = PdfDocumentAttachments.GetSignatures(signedDocument)[0].Contents;
        PdfSignatureValidationData[] data = [new(contents, [SignatureFixtures.Signer.RawData], [], [StoredCrl])];
        var withData = await PdfDocumentSigning.AddValidationDataAsync(signedDocument, data, true, cancellationToken);
        using var longTerm = PdfDocumentReader.Open(withData.Bytes, null);

        var beforeStamp = PdfPadesReader.Read(longTerm).Single();
        var stamped = await PdfDocumentSigning.AddDocumentTimestampAsync(longTerm, new() { Timestamp = new TimestamperClient(authority) }, cancellationToken);
        using var archived = PdfDocumentReader.Open(stamped.Bytes, null);
        var afterStamp = PdfPadesReader.Read(archived).Single();

        await Assert.That(beforeStamp.Level).IsEqualTo(PdfPadesLevel.BaselineLt);
        await Assert.That(afterStamp.Level).IsEqualTo(PdfPadesLevel.BaselineLta);
        await Assert.That(afterStamp.WhyNotNextLevel().Length).IsEqualTo(0);
    }

    /// <summary>A file with no signatures gives no reports.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsignedFileHasNoReports()
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);

        await Assert.That(PdfPadesReader.Read(document).Length).IsEqualTo(0);
    }
}
