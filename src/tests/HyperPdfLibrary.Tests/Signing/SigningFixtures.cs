// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Shared certificates, files and helpers for the signing tests.</summary>
internal static class SigningFixtures
{
    /// <summary>The page index of the only page of the sample files.</summary>
    internal const int FirstPage = 0;

    /// <summary>The numbers in one byte range pair: offset and length.</summary>
    private const int RangePair = 2;

    /// <summary>Gets a second self-signed RSA signer, made once because RSA key generation is slow.</summary>
    internal static X509Certificate2 SecondSigner { get; } = PdfSigning.CreateCertificate("Second Signer", null, false, TimeProvider.System);

    /// <summary>Gets a fixed signing time so files are repeatable.</summary>
    internal static DateTimeOffset Time => PdfSigning.SigningTime;

    /// <summary>Signs a file with a certificate and returns the new file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="options">The options.</param>
    /// <returns>The signed file.</returns>
    internal static byte[] Sign(byte[] file, X509Certificate2 certificate, PdfSigningOptions options) => SignResult(file, certificate, options).Bytes;

    /// <summary>Signs a file with a certificate.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="options">The options.</param>
    /// <returns>The result.</returns>
    internal static PdfSigningResult SignResult(byte[] file, X509Certificate2 certificate, PdfSigningOptions options)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return PdfDocumentSigning.Sign(document, options with { SigningTime = options.SigningTime ?? Time }, new(certificate));
    }

    /// <summary>Signs a file with a certificate, waiting for a timestamp when the options ask for one.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="options">The options.</param>
    /// <returns>The result.</returns>
    internal static async Task<PdfSigningResult> SignResultAsync(byte[] file, X509Certificate2 certificate, PdfSigningOptions options)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return await PdfDocumentSigning.SignAsync(document, options with { SigningTime = options.SigningTime ?? Time }, new PdfCertificateSigner(certificate), CancellationToken.None);
    }

    /// <summary>Signs a file with a certificate and a digest, as an approval signature with the reason Approved.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The signed file.</returns>
    internal static byte[] SignWithDigest(byte[] file, X509Certificate2 certificate, HyperPdf.Core.Signatures.Signing.SigningDigest digest) =>
        Sign(file, certificate, new() { Reason = "Approved", Digest = TimestamperClient.ToDigest(digest) });

    /// <summary>Validates every signature of a file, trusting some certificates.</summary>
    /// <param name="file">The file.</param>
    /// <param name="roots">The trusted roots.</param>
    /// <returns>The reports.</returns>
    internal static IReadOnlyList<PdfSignatureValidationReport> Validate(byte[] file, params X509Certificate2[] roots)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [.. roots] });
    }

    /// <summary>Builds a one-page file with an AcroForm holding an empty signature field.</summary>
    /// <param name="fieldExtras">Extra entries for the field, such as <c>/Lock</c> or <c>/SV</c> references.</param>
    /// <param name="extraObjects">Extra objects, numbered from 5.</param>
    /// <returns>The file.</returns>
    internal static byte[] WithEmptyField(string fieldExtras, params string[] extraObjects) => MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] /SigFlags 0 >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << >> /Annots [4 0 R] >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Sig /T (Approval) /Rect [20 20 200 80] /F 4 /P 3 0 R {fieldExtras} >>",
            .. extraObjects,
        ]);

    /// <summary>Reads the decoded text of the first stream under /AP /N of a signature field's widget.</summary>
    /// <param name="file">The file.</param>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The stream's content as text, or an empty string when there is none.</returns>
    internal static string AppearanceText(byte[] file, string fieldName)
    {
        using var document = PdfDocumentReader.Open(file, null);
        var field = PdfSigningFields.Find(document.Objects, fieldName);
        var stream = field?.Widget?.GetDictionary(HyperPdfLibrary.Objects.KnownName.AP)?.GetStream(HyperPdfLibrary.Objects.KnownName.N);
        return stream is null ? string.Empty : Encoding.Latin1.GetString(stream.DecodeToArray());
    }

    /// <summary>Gets the signed bytes of the first signature of a file: everything its byte range covers.</summary>
    /// <param name="file">The file.</param>
    /// <param name="contents">Receives the signature container without its padding.</param>
    /// <returns>The signed bytes.</returns>
    internal static byte[] SignedBytes(byte[] file, out byte[] contents)
    {
        using var document = PdfDocumentReader.Open(file, null);
        var field = PdfDocumentAttachments.GetSignatures(document)[0];
        contents = Container(field.Contents);
        var signed = new List<byte>();
        for (var i = 0; i + 1 < field.ByteRange.Length; i += RangePair)
        {
            signed.AddRange(file.AsSpan((int)field.ByteRange[i], (int)field.ByteRange[i + 1]).ToArray());
        }

        return [.. signed];
    }

    /// <summary>
    /// Cuts the signature container out of a zero-padded /Contents value. The container ends where its own DER length says:
    /// a signature can end in zero bytes, so trimming the zeros would cut it short.
    /// </summary>
    /// <param name="padded">The /Contents bytes, container first and zero padding after it.</param>
    /// <returns>The container.</returns>
    internal static byte[] Container(ReadOnlySpan<byte> padded)
    {
        _ = AsnDecoder.ReadEncodedValue(padded, AsnEncodingRules.BER, out _, out _, out var length);
        return padded[..length].ToArray();
    }

    /// <summary>Creates a self-signed elliptic-curve certificate.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="name">The common name.</param>
    /// <returns>The certificate with its key.</returns>
    internal static X509Certificate2 CreateEc(ECCurve curve, string name)
    {
        using var key = ECDsa.Create(curve);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA512);
        return request.CreateSelfSigned(Time.AddDays(-1), Time.AddYears(1));
    }
}
