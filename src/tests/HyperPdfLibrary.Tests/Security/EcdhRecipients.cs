// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// ECDH public-key recipients made once by OpenSSL 4.0.3, a CMS writer independent of the reader. The files are in
/// <c>Security/Data</c>, embedded in the test assembly; the keys are test-only. Each envelope holds the twenty 0x5A seed
/// bytes <see cref="PublicKeyPdf"/> uses, then the permissions, big-endian: <see cref="PrintingDenied"/> in
/// <c>printing.bin</c> and <see cref="AllAllowed"/> in <c>all.bin</c>. <c>openssl cms -decrypt</c> gave both back.
/// The files were made with these commands (the P-384 key and certificate the same way with <c>P-384</c>):
/// <code>
/// openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -nodes -keyout p256.key -out p256.crt
///     -subj "/CN=ECDH P-256 Recipient" -days 36500
/// openssl cms -encrypt -binary -in printing.bin -outform DER -out p256-default.der -aes-256-cbc -recip p256.crt
/// openssl cms -encrypt -binary -in all.bin -outform DER -out p384-keyid.der -aes-128-cbc -keyid -recip p384.crt
///     -keyopt ecdh_kdf_md:sha384
/// openssl cms -encrypt -binary -in printing.bin -outform DER -out both-cofactor.der -aes-192-cbc
///     -recip p256.crt -keyopt ecdh_kdf_md:sha256 -keyopt ecdh_cofactor_mode:1
///     -recip p384.crt -keyopt ecdh_kdf_md:sha256 -keyopt ecdh_cofactor_mode:1
/// openssl cms -encrypt -binary -in printing.bin -outform DER -out p256-des3.der -des3 -recip p256.crt
/// </code>
/// </summary>
internal static class EcdhRecipients
{
    /// <summary>The permissions in the P-256 envelopes: printing and copying denied.</summary>
    internal const int PrintingDenied = -3904;

    /// <summary>The permissions in the P-384 envelope: everything allowed.</summary>
    internal const int AllAllowed = -4;

    /// <summary>
    /// Gets the P-256 envelope with OpenSSL's defaults: issuer and serial number, SHA-1 X9.63 KDF, AES-256 key wrap and
    /// AES-256-CBC content.
    /// </summary>
    internal static byte[] P256Default => Read("p256-default.der");

    /// <summary>Gets the P-384 envelope: subject key identifier, SHA-384 KDF, AES-128 key wrap and AES-128-CBC content.</summary>
    internal static byte[] P384KeyId => Read("p384-keyid.der");

    /// <summary>Gets one envelope for both recipients: cofactor ECDH, SHA-256 KDF, AES-192 key wrap and AES-192-CBC content.</summary>
    internal static byte[] BothCofactor => Read("both-cofactor.der");

    /// <summary>Gets the P-256 envelope with Triple DES content, which OpenSSL wraps with the Triple DES key wrap.</summary>
    internal static byte[] P256TripleDes => Read("p256-des3.der");

    /// <summary>Loads the P-256 recipient with its private key.</summary>
    /// <returns>The certificate.</returns>
    internal static X509Certificate2 P256() => WithKey("p256");

    /// <summary>Loads the P-384 recipient with its private key.</summary>
    /// <returns>The certificate.</returns>
    internal static X509Certificate2 P384() => WithKey("p384");

    /// <summary>Loads the P-256 recipient's certificate without its private key.</summary>
    /// <returns>The certificate.</returns>
    internal static X509Certificate2 P256WithoutKey() => X509Certificate2.CreateFromPem(ReadText("p256.crt"));

    /// <summary>Attaches a private key to a certificate as an ECDH key, which every platform can use for key agreement.</summary>
    /// <param name="name">The file name before <c>.crt</c> and <c>.key</c>.</param>
    /// <returns>The certificate with its key.</returns>
    private static X509Certificate2 WithKey(string name)
    {
        using var publicOnly = X509Certificate2.CreateFromPem(ReadText($"{name}.crt"));
        using var ecdh = ECDiffieHellman.Create();
        ecdh.ImportFromPem(ReadText($"{name}.key"));
        return publicOnly.CopyWithPrivateKey(ecdh);
    }

    /// <summary>Reads an embedded text file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The text.</returns>
    private static string ReadText(string name) => Encoding.ASCII.GetString(Read(name));

    /// <summary>Reads an embedded file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidOperationException">The file is not embedded in the assembly.</exception>
    private static byte[] Read(string name)
    {
        using var stream = typeof(EcdhRecipients).Assembly.GetManifestResourceStream($"HyperPdfLibrary.Tests.Security.Ecdh.{name}")
            ?? throw new InvalidOperationException($"The ECDH fixture '{name}' is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
