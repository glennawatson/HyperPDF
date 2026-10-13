// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine.Tests;

/// <summary>Opens files that HyperPDF encrypted with HyperPDF and with the PDFium reference, and compares what each reports.</summary>
public sealed class EncryptionParityTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The user password.</summary>
    private const string UserPassword = "reader-pass";

    /// <summary>The owner password.</summary>
    private const string OwnerPassword = "owner-pass";

    /// <summary>The revision of AES-128.</summary>
    private const int Revision4 = 4;

    /// <summary>The revision of AES-256.</summary>
    private const int Revision6 = 6;

    /// <summary>Both engines open the file with the user and owner password, count the same pages, report the same permissions and read the same text.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="revision">The revision the algorithm writes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, Revision4)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, Revision6)]
    public async Task BothEnginesOpenTheFileAndAgreeOnPermissions(PdfEncryptionAlgorithm algorithm, int revision)
    {
        var file = Encrypt(algorithm);
        var expected = (uint)PdfPermissionBitsForTest(file);

        foreach (var password in new[] { UserPassword, OwnerPassword })
        {
            using var pair = new EnginePair(file, password);
            await Assert.That(pair.Pdfium.PageCount).IsEqualTo(Pages);
            await Assert.That(pair.Library.PageCount).IsEqualTo(Pages);
            await Assert.That(PdfiumReferenceSecurity.GetRevision(pair.Pdfium)).IsEqualTo(revision);
            await Assert.That(pair.Library.IsEncrypted).IsTrue();
            var pdfiumText = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0).Text;
            await Assert.That(pdfiumText).IsNotEmpty();
            await Assert.That(PdfDocumentText.GetTextPage(pair.Library, 0).Text.Trim()).IsEqualTo(pdfiumText.Trim());
            if (password == UserPassword)
            {
                await Assert.That(PdfiumReferenceSecurity.GetPermissions(pair.Pdfium)).IsEqualTo(expected);
            }
        }
    }

    /// <summary>A wrong or missing password is refused by both engines.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="password">The password.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, "wrong")]
    [Arguments(PdfEncryptionAlgorithm.Aes128, null)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, "wrong")]
    [Arguments(PdfEncryptionAlgorithm.Aes256, null)]
    public async Task WrongPasswordIsRefusedByBothEngines(PdfEncryptionAlgorithm algorithm, string? password)
    {
        var file = Encrypt(algorithm);

        await Assert.That(PdfiumReferenceDocument.GetOpenError(file, password)).IsEqualTo(PdfiumOpenError.Password);
        var exception = await Assert.That(() => PdfDocumentReader.Open(file, password)).Throws<PdfException>();
        await Assert.That(exception!.Error).IsEqualTo(PdfError.Password);
    }

    /// <summary>
    /// The PDFium build the tests use does not open AES-GCM files (revision 7, ISO/TS 32003), because it does not know
    /// version 6; HyperPDF opens them. The difference is recorded here so a PDFium that learns the revision shows up.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfiumDoesNotOpenAesGcmFilesAndHyperPdfDoes()
    {
        if (!System.Security.Cryptography.AesGcm.IsSupported)
        {
            Skip.Test("AES-GCM is not available on this platform.");
        }

        var file = Encrypt(PdfEncryptionAlgorithm.Aes256Gcm);

        await Assert.That(PdfiumReferenceDocument.GetOpenError(file, UserPassword)).IsNotEqualTo(PdfiumOpenError.Success);
        using var document = PdfDocumentReader.Open(file, UserPassword);
        await Assert.That(document.PageCount).IsEqualTo(Pages);
    }

    /// <summary>Encrypts the generated document with print and copy allowed and the other permissions cleared.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The encrypted file.</returns>
    private static byte[] Encrypt(PdfEncryptionAlgorithm algorithm)
    {
        var options = new PdfEncryptionOptions
        {
            Algorithm = algorithm,
            UserPassword = UserPassword,
            OwnerPassword = OwnerPassword,
            Permissions = PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent,
        };
        using var store = StoreOpening.Open(TestPdf.Create(Pages), null);
        return PdfCompactWriter.Save(store, PdfCompactOptions.Default with { Encryption = options });
    }

    /// <summary>Reads the permission word HyperPDF reports when the file is opened with the user password.</summary>
    /// <param name="file">The encrypted file.</param>
    /// <returns>The word.</returns>
    private static int PdfPermissionBitsForTest(byte[] file)
    {
        using var store = StoreOpening.Open(file, UserPassword);
        return store.Security!.Permissions;
    }
}
