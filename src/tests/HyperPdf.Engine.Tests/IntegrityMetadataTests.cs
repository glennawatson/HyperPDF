// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Security;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests that the engine passes the PDF MAC check of ISO/TS 32004 to the viewer, and that a file without one says nothing.</summary>
public sealed class IntegrityMetadataTests
{
    /// <summary>The user password of the sample.</summary>
    private const string UserPassword = "user";

    /// <summary>The owner password of the sample.</summary>
    private const string OwnerPassword = "owner";

    /// <summary>Every result of the library maps to the viewer's, and the sentence is passed on.</summary>
    /// <param name="status">The library result.</param>
    /// <param name="expected">The viewer's result.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfMacStatus.Valid, DocumentIntegrityStatus.Intact)]
    [Arguments(PdfMacStatus.Invalid, DocumentIntegrityStatus.Invalid)]
    [Arguments(PdfMacStatus.Missing, DocumentIntegrityStatus.Missing)]
    [Arguments(PdfMacStatus.Unsupported, DocumentIntegrityStatus.Unverified)]
    public async Task ResultsMapToTheViewersResults(PdfMacStatus status, DocumentIntegrityStatus expected)
    {
        var report = new PdfMacReport(status, []);
        var integrity = HyperPdfNavigation.IntegrityOf(report);

        await Assert.That(integrity!.Status).IsEqualTo(expected);
        await Assert.That(integrity.Summary).IsEqualTo(report.Summary);
        await Assert.That(integrity.Summary).IsNotEmpty();
    }

    /// <summary>A document with no integrity protection gives no result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoProtectionGivesNoResult() =>
        await Assert.That(HyperPdfNavigation.IntegrityOf(PdfMacReport.NotApplicable)).IsNull();

    /// <summary>
    /// A file whose permissions clear bit 13 (so it requires a PDF MAC) but that has none is reported as missing it, never as intact,
    /// and a save that cannot make a valid token stops and says why instead of writing a file with a false claim.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RequiredButMissingTokenIsReportedAndStopsTheSave()
    {
        var file = UserPasswordPdf.Create(UserPassword, OwnerPassword);

        // Same length as "/P -4": the value 1 has bit 13 clear.
        "/P  1"u8.CopyTo(file.AsSpan(file.AsSpan().IndexOf("/P -4"u8)));
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-mac-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, file);
        try
        {
            using var document = new HyperPdfEngine().Open(path, UserPassword);
            var integrity = document.GetMetadata().Integrity;
            await Assert.That(integrity!.Status).IsEqualTo(DocumentIntegrityStatus.Missing);
            await Assert.That(integrity.NeedsWarning).IsTrue();
            await Assert.That(IntegrityWarnings.Describe(integrity)).Contains("requires integrity protection");

            var report = new DocumentSaveReport();
            await using var output = new MemoryStream();
            var saved = DocumentFeature<IAnnotationEditor>.Cast(document)!.Save(output, report);
            await Assert.That(saved).IsFalse();
            await Assert.That(report.Failure).Contains("KDFSalt");
            await Assert.That(output.Length).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>An ordinary encrypted file without a PDF MAC opens as before and reports no integrity result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryEncryptedFileHasNoIntegrityResult()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-encrypted-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, UserPasswordPdf.Create(UserPassword, OwnerPassword));
        try
        {
            using var document = new HyperPdfEngine().Open(path, UserPassword);
            var metadata = document.GetMetadata();

            await Assert.That(metadata.IsEncrypted).IsTrue();
            await Assert.That(metadata.Integrity).IsNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
