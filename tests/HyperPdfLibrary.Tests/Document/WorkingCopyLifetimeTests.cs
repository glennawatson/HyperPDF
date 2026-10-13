// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Pdf20;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Tests.Pdf20;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>
/// The private copies and earlier revisions an operation opens are disposed when it ends. They borrow the open document's
/// decryption, drawing services and file, so the open document still renders, reads text, saves and decrypts afterwards.
/// </summary>
public sealed class WorkingCopyLifetimeTests
{
    /// <summary>The x of a pixel inside the MAC sample's blue square.</summary>
    private const int SquareX = 70;

    /// <summary>The y, from the top, of a pixel inside the MAC sample's blue square.</summary>
    private const int SquareY = 130;

    /// <summary>The largest difference allowed in any channel of the square's colour.</summary>
    private const int ColourTolerance = 8;

    /// <summary>The blue the MAC sample's square is drawn in.</summary>
    private static readonly Rgb Blue = new(0, 0, 255);

    /// <summary>Encrypting a copy leaves the open document usable.</summary>
    /// <param name="encrypted">Whether the open document is itself encrypted.</param>
    /// <param name="useAsync">Whether to use the asynchronous form.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ProtectingACopyLeavesTheDocumentUsable(bool encrypted, bool useAsync)
    {
        using var document = Open(encrypted);
        var password = encrypted ? EncryptionTestFiles.OwnerPassword : null;
        var options = new PdfEncryptionOptions { Algorithm = PdfEncryptionAlgorithm.Aes256, UserPassword = "other-user", OwnerPassword = "other-owner" };
        await using var output = new MemoryStream();

        var result = useAsync
            ? await PdfDocumentProtection.ProtectAsync(document, options, password, output, CancellationToken.None)
            : PdfDocumentProtection.Protect(document, options, password, output);

        await Assert.That(result.Refusal).IsNull();
        await Assert.That(output.Length).IsGreaterThan(0);
        await AssertUsable(document, EncryptionTestFiles.PageText, password);
    }

    /// <summary>Writing an unprotected copy of an encrypted document leaves the document decrypting.</summary>
    /// <param name="useAsync">Whether to use the asynchronous form.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RemovingProtectionFromACopyLeavesTheDocumentUsable(bool useAsync)
    {
        using var document = Open(true);
        await using var output = new MemoryStream();

        var result = useAsync
            ? await PdfDocumentProtection.RemoveProtectionAsync(document, EncryptionTestFiles.OwnerPassword, output, CancellationToken.None)
            : PdfDocumentProtection.RemoveProtection(document, EncryptionTestFiles.OwnerPassword, output);

        await Assert.That(result.Refusal).IsNull();
        using var copy = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(copy.IsEncrypted).IsFalse();
        await AssertUsable(document, EncryptionTestFiles.PageText, EncryptionTestFiles.OwnerPassword);
    }

    /// <summary>Optimising a document, which works on a private copy, leaves the document usable.</summary>
    /// <param name="encrypted">Whether the open document is encrypted.</param>
    /// <param name="useAsync">Whether to use the asynchronous form.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task OptimizingLeavesTheDocumentUsable(bool encrypted, bool useAsync)
    {
        using var document = Open(encrypted);
        await using var output = new MemoryStream();

        _ = useAsync
            ? await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.Balanced, null, CancellationToken.None)
            : PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.Balanced);

        await Assert.That(output.Length).IsGreaterThan(0);
        await AssertUsable(document, EncryptionTestFiles.PageText, encrypted ? EncryptionTestFiles.OwnerPassword : null);
    }

    /// <summary>Saving a PDF 2.0 copy leaves the document usable.</summary>
    /// <param name="useAsync">Whether to use the asynchronous form.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConvertingToPdf20LeavesTheDocumentUsable(bool useAsync)
    {
        using var document = Open(false);
        await using var output = new MemoryStream();

        var result = useAsync
            ? await Pdf20Converter.SaveAsPdf20Async(document, output, CancellationToken.None)
            : Pdf20Converter.SaveAsPdf20(document, output);

        await Assert.That(result.Converted).IsTrue();
        await AssertUsable(document, Pdf20TestPdf.SampleText, null);
    }

    /// <summary>Checking a signature against the revision it signed, after later changes, leaves the document usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidatingASignedRevisionLeavesTheDocumentUsable()
    {
        var file = SignatureSamples.AddAnnotation(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty));
        using var document = SkiaTestRegistration.Open(file, null);

        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document, PdfSignatureValidationOptions.Default);

        await Assert.That(reports[0].ModifiedAfterSigning).IsTrue();
        await Assert.That(reports[0].Changes.Count).IsGreaterThan(0);
        await AssertUsable(document, null, null);
    }

    /// <summary>Checking the PDF MAC of every revision of an encrypted document leaves the document decrypting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckingThePdfMacLeavesTheDocumentDecrypting()
    {
        var file = MacPdf.Create(MacProtection.Standard(false, MacProtection.MacRequired), MacToken.Sha256, true);
        using var document = SkiaTestRegistration.Open(file, AesGcmPdf.UserPassword);

        var report = PdfDocumentMac.GetIntegrity(document);

        await Assert.That(report.Status).IsEqualTo(PdfMacStatus.Valid);
        await AssertUsable(document, null, AesGcmPdf.UserPassword);
        await Assert.That(Render(document).IsNear(SquareX, SquareY, Blue, ColourTolerance)).IsTrue();
    }

    /// <summary>Opens the sample file, encrypted with both passwords or plain.</summary>
    /// <param name="encrypted">Whether to encrypt it.</param>
    /// <returns>The open document, with its own drawing services.</returns>
    private static PdfDocument Open(bool encrypted) => encrypted
        ? SkiaTestRegistration.Open(
            EncryptionTestFiles.Encrypt(
                EncryptionTestFiles.Plain(),
                new() { Algorithm = PdfEncryptionAlgorithm.Aes256, UserPassword = EncryptionTestFiles.UserPassword, OwnerPassword = EncryptionTestFiles.OwnerPassword },
                false),
            EncryptionTestFiles.OwnerPassword)
        : SkiaTestRegistration.Open(EncryptionTestFiles.Plain(), null);

    /// <summary>Renders the first page at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static RenderedImage Render(PdfDocument document)
    {
        using var renderer = new PdfPageRenderer(document);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, 0), 0, 1, out var width, out var height);
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        return renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel))
            ? new(pixels, width, height)
            : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Checks that a document still reads text, renders, saves and, when encrypted, decrypts.</summary>
    /// <param name="document">The document.</param>
    /// <param name="expectedText">The text the first page holds, or <see langword="null"/> when it holds none to compare.</param>
    /// <param name="password">The password the saved file opens with, or <see langword="null"/>.</param>
    /// <returns>A task.</returns>
    private static async Task AssertUsable(PdfDocument document, string? expectedText, string? password)
    {
        await Assert.That(document.IsDisposed).IsFalse();
        if (expectedText is not null)
        {
            await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).Contains(expectedText);
        }

        var image = Render(document);
        await Assert.That(image.Width).IsGreaterThan(0);

        // A signed file is saved as an update, because rewriting it would break the signatures.
        var saved = PdfDocumentAttachments.GetSignatures(document).Count > 0
            ? PdfIncrementalWriter.Save(document.Objects)
            : PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        using var reopened = PdfDocumentReader.Open(saved, password);
        await Assert.That(reopened.PageCount).IsEqualTo(document.PageCount);
        if (expectedText is not null)
        {
            await Assert.That(PdfDocumentText.GetTextPage(reopened, 0).Text).Contains(expectedText);
        }
    }
}
