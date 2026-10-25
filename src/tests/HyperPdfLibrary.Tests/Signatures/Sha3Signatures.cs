// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// SHA-3 and SHAKE256 signatures made once by OpenSSL 4.0.3, a signer independent of HyperPDF, over the signed bytes of
/// <c>sha3-unsigned.pdf</c>. That file is the signature sample form whose field has a seed value
/// <c>/DigestMethod [/SHA3-256 /SHA3-384 /SHA3-512 /SHAKE256] /Ff 64</c> and whose signature has a FieldMDP reference with
/// <c>/DigestMethod /SHA3-256</c>, with its byte range not yet filled in; <see cref="PdfSigning.Prepare"/> fills it in
/// and gives the signed bytes, saved as <c>signed.bin</c>. The files are in <c>Signatures/Data</c>, embedded in the test
/// assembly; the keys were thrown away. The SHA-3 signatures were made with:
/// <code>
/// openssl req -x509 -newkey rsa:2048 -nodes -keyout rsa.key -out rsa.crt -subj "/CN=SHA-3 Test RSA" -days 36500 -sha256
/// openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -keyout p256.key -out p256.crt
///     -subj "/CN=SHA-3 Test P-256" -days 36500
/// openssl cms -sign -binary -md sha3-256 -nosmimecap -signer rsa.crt -inkey rsa.key -in signed.bin -outform DER
///     -out rsa-sha3-256.p7s
/// openssl cms -sign -binary -md sha3-384 -nosmimecap -signer rsa.crt -inkey rsa.key -in signed.bin -outform DER
///     -out rsa-sha3-384.p7s
/// </code>
/// ECDSA with SHA3-512 is covered by <see cref="EcSignatures"/>, on P-521, since ISO/TS 32002 pairs P-256 only with SHA-256
/// and SHA3-256.
/// OpenSSL's <c>cms -sign</c> refuses SHAKE256 ("unsupported signature algorithm"), so for <c>p256-shake256.p7s</c>
/// OpenSSL computed the digests and the signature and a throwaway System.Formats.Asn1 program only laid out the DER: the
/// content-type, signing-time (<see cref="PdfSigning.SigningTime"/>) and message-digest signed attributes, id-shake256
/// with no parameters and id-ecdsa-with-shake256. <c>openssl pkeyutl -verify</c> accepted the signature.
/// <code>
/// openssl dgst -shake256 -xoflen 64 -binary -out shake-digest.bin signed.bin
/// openssl dgst -shake256 -xoflen 64 -binary -out shake-attrs-hash.bin shake-attrs.der
/// openssl pkeyutl -sign -inkey p256.key -in shake-attrs-hash.bin -out shake-sig.der
/// </code>
/// </summary>
internal static class Sha3Signatures
{
    /// <summary>Gets the RSA signature with SHA3-256.</summary>
    internal static byte[] RsaSha3x256 => Read("rsa-sha3-256.p7s");

    /// <summary>Gets the RSA signature with SHA3-384.</summary>
    internal static byte[] RsaSha3x384 => Read("rsa-sha3-384.p7s");

    /// <summary>Gets the P-256 ECDSA signature with SHAKE256 (id-ecdsa-with-shake256).</summary>
    internal static byte[] P256Shake256 => Read("p256-shake256.p7s");

    /// <summary>Gets the RSA signer's certificate.</summary>
    /// <returns>The certificate.</returns>
    internal static X509Certificate2 RsaCertificate() => X509Certificate2.CreateFromPem(Encoding.ASCII.GetString(Read("rsa.crt")));

    /// <summary>Gets the P-256 signer's certificate.</summary>
    /// <returns>The certificate.</returns>
    internal static X509Certificate2 P256Certificate() => X509Certificate2.CreateFromPem(Encoding.ASCII.GetString(Read("p256.crt")));

    /// <summary>Gets the file the signatures were made for, with its byte range filled in.</summary>
    /// <returns>The prepared file.</returns>
    internal static PreparedSignature Prepare() => PdfSigning.Prepare(Read("sha3-unsigned.pdf"));

    /// <summary>Writes a signature into the prepared file.</summary>
    /// <param name="signature">The DER signature.</param>
    /// <returns>The signed file.</returns>
    internal static byte[] Sign(byte[] signature) => PdfSigning.WriteContents(Prepare(), signature);

    /// <summary>Reads an embedded file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidOperationException">The file is not embedded in the assembly.</exception>
    internal static byte[] Read(string name)
    {
        using var stream = typeof(Sha3Signatures).Assembly.GetManifestResourceStream($"HyperPdfLibrary.Tests.Signatures.Sha3.{name}")
            ?? throw new InvalidOperationException($"The SHA-3 fixture '{name}' is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
