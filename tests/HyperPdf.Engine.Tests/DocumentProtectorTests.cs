// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Protection;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the engine's protector: protected copies open in both engines, restricted files need the owner, and refusals write nothing.</summary>
public sealed class DocumentProtectorTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The user password.</summary>
    private const string UserPassword = "reader-pass";

    /// <summary>The owner password.</summary>
    private const string OwnerPassword = "owner-pass";

    /// <summary>The permission bit that allows printing (ISO 32000-2 Table 22, bit 3).</summary>
    private const uint PrintBit = 1U << 2;

    /// <summary>The permission bit that allows copying text and graphics (bit 5).</summary>
    private const uint CopyBit = 1U << 4;

    /// <summary>A password that opens nothing.</summary>
    private static readonly string MissingPassword = System.Security.Cryptography.RandomNumberGenerator.GetHexString(16);

    /// <summary>A protected copy opens with both passwords in HyperPDF and the PDFium reference, keeps the pages and refuses a wrong password.</summary>
    /// <param name="format">The encryption.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ProtectionFormat.Strong)]
    [Arguments(ProtectionFormat.Compatible)]
    public async Task ProtectedCopyOpensWithBothPasswords(ProtectionFormat format)
    {
        var bytes = await ProtectAsync(format, ProtectionPermissions.Print | ProtectionPermissions.Accessibility);

        foreach (var password in new[] { UserPassword, OwnerPassword })
        {
            using var pair = new EnginePair(bytes, password);
            await Assert.That(pair.Pdfium.PageCount).IsEqualTo(Pages);
            await Assert.That(pair.Library.PageCount).IsEqualTo(Pages);
            await Assert.That(pair.Library.IsEncrypted).IsTrue();
        }

        await Assert.That(PdfiumReferenceDocument.GetOpenError(bytes, MissingPassword)).IsEqualTo(PdfiumOpenError.Password);
        await Assert.That(PdfiumReferenceDocument.GetOpenError(bytes, null)).IsEqualTo(PdfiumOpenError.Password);
    }

    /// <summary>The user password gives the chosen permissions and the owner password gives all of them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PermissionsFollowThePasswordUsed()
    {
        var bytes = await ProtectAsync(ProtectionFormat.Compatible, ProtectionPermissions.Print);

        using var user = new EnginePair(bytes, UserPassword);
        using var owner = new EnginePair(bytes, OwnerPassword);

        var userBits = PdfiumReferenceSecurity.GetPermissions(user.Pdfium);
        await Assert.That(userBits & PrintBit).IsNotEqualTo(0U);
        await Assert.That(userBits & CopyBit).IsEqualTo(0U);
        await Assert.That(PdfiumReferenceSecurity.GetPermissions(owner.Pdfium) & CopyBit).IsNotEqualTo(0U);
    }

    /// <summary>The status says when a document is protected and whether the opener may change it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StatusDescribesTheProtection()
    {
        using var plain = new HyperPdfTestDocument(TestPdf.Create(Pages));
        await Assert.That(DocumentFeature<IDocumentProtector>.Cast(plain.Document)!.GetStatus()).IsEqualTo(new(false, true, false));

        var bytes = await ProtectAsync(ProtectionFormat.Strong, ProtectionPermissions.Print);
        using var user = new EnginePair(bytes, UserPassword);
        using var owner = new EnginePair(bytes, OwnerPassword);

        await Assert.That(DocumentFeature<IDocumentProtector>.Cast(user.HyperPdf)!.GetStatus()).IsEqualTo(new(true, false, false));
        await Assert.That(DocumentFeature<IDocumentProtector>.Cast(owner.HyperPdf)!.GetStatus()).IsEqualTo(new(true, true, false));
    }

    /// <summary>A document opened with the user password cannot be unprotected until the owner password is given.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingProtectionNeedsTheOwner()
    {
        var bytes = await ProtectAsync(ProtectionFormat.Strong, ProtectionPermissions.Print);
        using var user = new EnginePair(bytes, UserPassword);
        var protector = DocumentFeature<IDocumentProtector>.Cast(user.HyperPdf)!;
        await using var refused = new MemoryStream();

        var denied = await protector.RemoveProtectionAsync(null, refused, CancellationToken.None);
        await Assert.That(denied.Written).IsFalse();
        await Assert.That(denied.Refusal).Contains("owner password");
        await Assert.That(refused.Length).IsEqualTo(0);

        await using var plain = new MemoryStream();
        var allowed = await protector.RemoveProtectionAsync(OwnerPassword, plain, CancellationToken.None);
        await Assert.That(allowed.Written).IsTrue().Because(allowed.Refusal ?? string.Empty);
        using var reopened = PdfDocumentReader.Open(plain.ToArray(), null);
        await Assert.That(reopened.IsEncrypted).IsFalse();
        await Assert.That(reopened.PageCount).IsEqualTo(Pages);
        await Assert.That(PdfiumReferenceDocument.GetOpenError(plain.ToArray(), null)).IsEqualTo(PdfiumOpenError.Success);
    }

    /// <summary>An unprotected document has nothing to remove, and a cancelled token writes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingToRemoveAndCancellation()
    {
        using var plain = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var protector = DocumentFeature<IDocumentProtector>.Cast(plain.Document)!;
        await using var output = new MemoryStream();

        var result = await protector.RemoveProtectionAsync(null, output, CancellationToken.None);
        await Assert.That(result.Written).IsFalse();
        await Assert.That(result.Refusal).Contains("not protected");

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(async () => await protector.ProtectAsync(Request(ProtectionFormat.Strong, ProtectionPermissions.All), output, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>Restricting permissions without a separate owner password is refused by the library with a reason, not written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestrictionWithoutAnOwnerPasswordIsRefused()
    {
        using var plain = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var protector = DocumentFeature<IDocumentProtector>.Cast(plain.Document)!;
        await using var output = new MemoryStream();
        var request = new ProtectionRequest { UserPassword = UserPassword, Permissions = ProtectionPermissions.Print };

        await Assert.That(async () => await protector.ProtectAsync(request, output, CancellationToken.None)).Throws<ArgumentException>();
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>Makes a request with both passwords.</summary>
    /// <param name="format">The encryption.</param>
    /// <param name="permissions">What the user password allows.</param>
    /// <returns>The request.</returns>
    private static ProtectionRequest Request(ProtectionFormat format, ProtectionPermissions permissions) =>
        new() { Format = format, UserPassword = UserPassword, OwnerPassword = OwnerPassword, Permissions = permissions };

    /// <summary>Protects a generated document and returns the copy.</summary>
    /// <param name="format">The encryption.</param>
    /// <param name="permissions">What the user password allows.</param>
    /// <returns>The protected file.</returns>
    private static async Task<byte[]> ProtectAsync(ProtectionFormat format, ProtectionPermissions permissions)
    {
        using var source = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var protector = DocumentFeature<IDocumentProtector>.Cast(source.Document)!;
        await using var output = new MemoryStream();
        var result = await protector.ProtectAsync(Request(format, permissions), output, CancellationToken.None);
        await Assert.That(result.Written).IsTrue().Because(result.Refusal ?? string.Empty);
        return output.ToArray();
    }
}
