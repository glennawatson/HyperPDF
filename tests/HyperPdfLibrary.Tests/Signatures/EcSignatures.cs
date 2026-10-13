// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Elliptic-curve and EdDSA signatures made once by OpenSSL 4.0.3, a signer independent of HyperPDF, over the signed bytes
/// of <c>sha3-unsigned.pdf</c> (see <see cref="Sha3Signatures"/>), for ISO/TS 32002:2022. Each signer is a self-signed
/// certificate with a fresh key; the keys were thrown away. The files are in <c>Signatures/EcData</c>, embedded in the
/// test assembly. <c>signed.bin</c> is the byte range <see cref="Sha3Signatures.Prepare"/> computes. The commands were:
/// <code>
/// openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -pkeyopt ec_param_enc:named_curve -nodes
///     -keyout p256.key -out p256.crt -subj "/CN=EC Test P-256" -days 36500 -sha512
/// (the same for P-384, P-521, brainpoolP256r1, brainpoolP384r1 and brainpoolP512r1, as p384, p521, bp256, bp384 and bp512)
/// openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:P-256 -pkeyopt ec_param_enc:explicit -nodes
///     -keyout explicit.key -out explicit.crt -subj "/CN=EC Test explicit P-256" -days 36500 -sha256
/// openssl req -x509 -newkey ed25519 -nodes -keyout ed25519.key -out ed25519.crt -subj "/CN=EC Test ed25519" -days 36500
/// openssl req -x509 -newkey ed448 -nodes -keyout ed448.key -out ed448.crt -subj "/CN=EC Test ed448" -days 36500
/// openssl cms -sign -binary -nosmimecap -signer p256.crt -inkey p256.key -in signed.bin -outform DER -md sha256 -out p256-sha256.p7s
/// (the same for each file name, with the key and digest it names: p256-sha3-256, p256-sha3-512, p256-sha512, p384-sha384,
///  p384-sha256, p521-sha512, p521-sha3-512, bp256-sha256, bp256-sha512, bp384-sha384, bp512-sha512, bp512-sha256 and
///  explicit-sha256)
/// openssl cms -sign -binary -nosmimecap -signer ed25519.crt -inkey ed25519.key -in signed.bin -outform DER -out ed25519.p7s
/// </code>
/// OpenSSL's <c>cms -sign</c> cannot sign with Ed448 ("no default digest"), so for <c>ed448.p7s</c> OpenSSL computed the
/// digest and the signature and a throwaway System.Formats.Asn1 program only laid out the DER: content-type, signing-time
/// and message-digest attributes, id-shake256 with no parameters and id-Ed448. <c>ed448-len.p7s</c> is the same signature
/// with id-shake256-len and 512 as the digest identifier, which the signature does not cover.
/// <code>
/// openssl dgst -shake256 -xoflen 64 -binary -out ed448-digest.bin signed.bin
/// openssl pkeyutl -sign -rawin -inkey ed448.key -in ed448-attrs.der -out ed448-sig.bin
/// </code>
/// </summary>
internal static class EcSignatures
{
    /// <summary>Reads a signature made by OpenSSL.</summary>
    /// <param name="name">The signature file name, such as <c>p256-sha256.p7s</c>.</param>
    /// <returns>The DER.</returns>
    internal static byte[] Signature(string name) => Read(name);

    /// <summary>Reads a signer's certificate.</summary>
    /// <param name="name">The certificate file name, such as <c>p256.crt</c>.</param>
    /// <returns>The certificate.</returns>
    internal static X509Certificate2 Certificate(string name) => X509Certificate2.CreateFromPem(Encoding.ASCII.GetString(Read(name)));

    /// <summary>Puts a signature into the file the signatures were made for.</summary>
    /// <param name="signature">The DER signature.</param>
    /// <returns>The signed file.</returns>
    internal static byte[] Sign(byte[] signature) => Sha3Signatures.Sign(signature);

    /// <summary>Replaces every occurrence of some bytes in a copy of a signature with bytes of the same length.</summary>
    /// <param name="signature">The DER signature.</param>
    /// <param name="find">The bytes to find.</param>
    /// <param name="replace">The replacement, the same length.</param>
    /// <returns>The changed copy.</returns>
    /// <exception cref="InvalidOperationException">The bytes are not in the signature.</exception>
    internal static byte[] Replace(byte[] signature, byte[] find, byte[] replace)
    {
        var copy = signature.ToArray();
        var found = false;
        var from = 0;
        for (var index = copy.AsSpan().IndexOf(find); index >= 0; index = copy.AsSpan(from).IndexOf(find))
        {
            replace.CopyTo(copy, from + index);
            found = true;
            from += index + find.Length;
            if (from > copy.Length - find.Length)
            {
                break;
            }
        }

        return found ? copy : throw new InvalidOperationException("The bytes to replace are not in the signature.");
    }

    /// <summary>Determines whether this computer's cryptography library implements a Brainpool curve.</summary>
    /// <param name="name">The curve's name, such as <c>brainpoolP256r1</c>.</param>
    /// <returns><see langword="true"/> when it can create keys on the curve.</returns>
    internal static bool PlatformHasCurve(string name)
    {
        try
        {
            using var key = ECDsa.Create(ECCurve.CreateFromFriendlyName(name));
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Reads an embedded file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidOperationException">The file is not embedded in the assembly.</exception>
    private static byte[] Read(string name)
    {
        using var stream = typeof(EcSignatures).Assembly.GetManifestResourceStream($"HyperPdfLibrary.Tests.Signatures.Ec.{name}")
            ?? throw new InvalidOperationException($"The elliptic-curve fixture '{name}' is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
