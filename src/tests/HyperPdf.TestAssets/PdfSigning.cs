// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes signature dictionaries with placeholders, fills in their byte range, and writes CMS signatures over it, so tests
/// can sign hand-built incremental updates.
/// </summary>
public static class PdfSigning
{
    /// <summary>The hex digits reserved for a signature.</summary>
    private const int ContentsHexLength = 16_384;

    /// <summary>The SHA-256 OID.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The width each byte range number is padded to.</summary>
    private const int ByteRangeWidth = 10;

    /// <summary>The angle brackets around the signature's hex digits.</summary>
    private const int Brackets = 2;

    /// <summary>The RSA key size of generated certificates.</summary>
    private const int KeyBits = 2048;

    /// <summary>Gets the signing time written into signed attributes and /M.</summary>
    public static DateTimeOffset SigningTime { get; } = DateTimeOffset.Parse("2026-01-02T03:04:05Z", CultureInfo.InvariantCulture);

    /// <summary>Gets the byte range placeholder.</summary>
    public static string ByteRangePlaceholder { get; } = $"[0 {new string('0', ByteRangeWidth)} {new string('0', ByteRangeWidth)} {new string('0', ByteRangeWidth)}]";

    /// <summary>Writes a signature dictionary with an empty byte range and contents.</summary>
    /// <param name="subFilter">The sub-filter, for example <c>adbe.pkcs7.detached</c>.</param>
    /// <param name="extra">Extra entries, such as a /Reference array.</param>
    /// <returns>The dictionary.</returns>
    public static string SignatureDictionary(string subFilter, string extra) =>
        $"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /{subFilter} /Name (Tester) /Reason (Approved) /M (D:20260102030405Z) {extra} "
        + $"/ByteRange {ByteRangePlaceholder} /Contents <{new string('0', ContentsHexLength)}> >>";

    /// <summary>Fills in the byte range of the last signature placeholder in a file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The file and the bytes to sign.</returns>
    public static PreparedSignature Prepare(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var bytes = file.ToArray();
        var text = Encoding.Latin1.GetString(bytes);
        var contentsStart = text.LastIndexOf("/Contents <", StringComparison.Ordinal) + "/Contents ".Length;
        var contentsEnd = contentsStart + ContentsHexLength + Brackets;
        var byteRange = $"[0 {Pad(contentsStart)} {Pad(contentsEnd)} {Pad(bytes.Length - contentsEnd)}]";
        Encoding.ASCII.GetBytes(byteRange).CopyTo(bytes, text.LastIndexOf(ByteRangePlaceholder, StringComparison.Ordinal));
        byte[] signed = [.. bytes.AsSpan(0, contentsStart), .. bytes.AsSpan(contentsEnd)];
        return new(bytes, signed, contentsStart);
    }

    /// <summary>Writes a signature into a prepared file's /Contents.</summary>
    /// <param name="prepared">The prepared file.</param>
    /// <param name="signature">The DER signature.</param>
    /// <returns>The signed file.</returns>
    public static byte[] WriteContents(PreparedSignature prepared, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(signature);
        var hex = Convert.ToHexString(signature).PadRight(ContentsHexLength, '0');
        Encoding.ASCII.GetBytes(hex).CopyTo(prepared.File, prepared.ContentsStart + 1);
        return prepared.File;
    }

    /// <summary>Signs the last placeholder with a detached SHA-256 CMS signature.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The signer, with its private key.</param>
    /// <returns>The signed file.</returns>
    public static byte[] SignDetached(byte[] file, X509Certificate2 certificate)
    {
        var prepared = Prepare(file);
        return WriteContents(prepared, CreateCms(prepared.SignedBytes, certificate));
    }

    /// <summary>Creates a detached SHA-256 CMS signature with a signing time.</summary>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="certificate">The signer.</param>
    /// <returns>The DER.</returns>
    public static byte[] CreateCms(byte[] signed, X509Certificate2 certificate)
    {
        var cms = new SignedCms(new ContentInfo(signed), true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate) { DigestAlgorithm = new(Sha256Oid) };
        _ = signer.SignedAttributes.Add(new Pkcs9SigningTime(SigningTime.UtcDateTime));
        cms.ComputeSignature(signer, true);
        return cms.Encode();
    }

    /// <summary>Creates an RSA certificate, self-signed or issued by another.</summary>
    /// <param name="name">The common name.</param>
    /// <param name="issuer">The issuer with its private key, or <see langword="null"/> for self-signed.</param>
    /// <param name="authority">Whether the certificate may issue others.</param>
    /// <param name="clock">The clock a self-signed certificate's validity starts from; issued certificates share the issuer's.</param>
    /// <returns>The certificate with its private key.</returns>
    public static X509Certificate2 CreateCertificate(string name, X509Certificate2? issuer, bool authority, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        using var key = RSA.Create(KeyBits);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(authority, false, 0, true));
        var usage = authority ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (issuer is null)
        {
            var now = clock.GetUtcNow();
            return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        using var issued = request.Create(issuer, issuer.NotBefore, issuer.NotAfter, RandomNumberGenerator.GetBytes(sizeof(long)));
        return issued.CopyWithPrivateKey(key);
    }

    /// <summary>Pads a byte range number to the placeholder's width.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The padded number.</returns>
    private static string Pad(int value) => value.ToString(CultureInfo.InvariantCulture).PadLeft(ByteRangeWidth, '0');
}
