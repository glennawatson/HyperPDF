// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.Core.Signatures;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Shared certificates and helpers for the signature tests.</summary>
internal static class SignatureFixtures
{
    /// <summary>Gets the self-signed signer, made once because RSA key generation is slow.</summary>
    internal static X509Certificate2 Signer { get; } = PdfSigning.CreateCertificate("Tester Signer", null, false, TimeProvider.System);

    /// <summary>Builds a one-page file with no form, for the viewer's signer to add its own field to.</summary>
    /// <returns>The file.</returns>
    internal static byte[] Blank() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>");

    /// <summary>Validates every signature of a file, trusting one root.</summary>
    /// <param name="file">The file.</param>
    /// <param name="root">The trusted root.</param>
    /// <returns>The reports.</returns>
    internal static IReadOnlyList<PdfSignatureValidationReport> Validate(byte[] file, X509Certificate2 root)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [root] });
    }

    /// <summary>Validates the only signature of a file signed by <see cref="Signer"/>.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The report.</returns>
    internal static PdfSignatureValidationReport ValidateSingle(byte[] file) => Validate(file, Signer)[0];

    /// <summary>Checks the first signature of a file with the viewer's verifier, trusting one root.</summary>
    /// <param name="file">The file.</param>
    /// <param name="root">The trusted root.</param>
    /// <returns>The viewer's result.</returns>
    internal static DocumentSignature ViewerCheck(byte[] file, X509Certificate2 root)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-signature-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, file);
        try
        {
            using var document = PdfDocumentReader.Open(file, null);
            var field = PdfDocumentAttachments.GetSignatures(document)[0];
            var raw = new RawSignature(field.Index, field.Contents, field.ByteRange, field.SubFilter, field.Reason, field.SigningTime);
            return SignatureVerifier.Verify(raw, path, [root]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Replaces the first occurrence of some text in a file with text of the same length.</summary>
    /// <param name="file">The file.</param>
    /// <param name="find">The text to find.</param>
    /// <param name="replace">The replacement, the same length.</param>
    /// <returns>The changed copy.</returns>
    internal static byte[] Patch(byte[] file, string find, string replace)
    {
        var copy = file.ToArray();
        var at = Encoding.Latin1.GetString(copy).IndexOf(find, StringComparison.Ordinal);
        Encoding.Latin1.GetBytes(replace).CopyTo(copy, at);
        return copy;
    }
}
