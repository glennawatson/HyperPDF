// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Tests.Signing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>A working copy borrows the open document's decryption, so disposing the copy leaves the document readable.</summary>
public sealed class BorrowedSecurityTests
{
    /// <summary>Disposing a working copy leaves the encrypted document readable.</summary>
    /// <param name="algorithm">The cipher of the file.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128)]
    [Arguments(PdfEncryptionAlgorithm.Aes256)]
    public async Task DisposingAWorkingCopyLeavesTheEncryptedDocumentReadable(PdfEncryptionAlgorithm algorithm)
    {
        var file = EncryptedFile(algorithm);
        using var document = PdfDocumentReader.Open(file, EncryptionTestFiles.OwnerPassword);

        PdfDocumentOptimizing.OpenWorkingCopy(document).Dispose();

        await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).Contains(EncryptionTestFiles.PageText);
    }

    /// <summary>Signing an encrypted document leaves it readable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SigningAnEncryptedDocumentLeavesItReadable()
    {
        var file = EncryptedFile(PdfEncryptionAlgorithm.Aes128);
        using var document = PdfDocumentReader.Open(file, EncryptionTestFiles.OwnerPassword);

        var signed = PdfDocumentSigning.Sign(document, new() { SigningTime = SigningFixtures.Time, VerifyOutput = false }, new(SignatureFixtures.Signer));

        await Assert.That(signed.Bytes.Length).IsGreaterThan(file.Length);
        await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).Contains(EncryptionTestFiles.PageText);
    }

    /// <summary>Encrypts the plain test file with user and owner passwords.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>The encrypted file.</returns>
    private static byte[] EncryptedFile(PdfEncryptionAlgorithm algorithm) =>
        EncryptionTestFiles.Encrypt(
            EncryptionTestFiles.Plain(),
            new() { Algorithm = algorithm, UserPassword = EncryptionTestFiles.UserPassword, OwnerPassword = EncryptionTestFiles.OwnerPassword },
            false);
}
