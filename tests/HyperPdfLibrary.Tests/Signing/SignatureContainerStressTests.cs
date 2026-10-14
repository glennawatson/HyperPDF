// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>
/// Signs a blank file again and again with fresh keys and certificates of every shape, and checks that every container
/// is strict DER and that the framework decodes it and checks its signature.
/// </summary>
public sealed class SignatureContainerStressTests
{
    /// <summary>The name of the variable that sets the number of signatures per configuration.</summary>
    private const string IterationsVariable = "HYPERPDF_SIGNING_STRESS";

    /// <summary>The number of signatures per configuration when the variable is not set.</summary>
    private const int DefaultIterations = 24;

    /// <summary>The number of signatures per RSA configuration when the variable is not set: RSA key generation is slow.</summary>
    private const int DefaultRsaIterations = 4;

    /// <summary>The longest serial number the certificate rules allow, in bytes.</summary>
    private const int MaxSerialBytes = 20;

    /// <summary>The bit that makes a serial number's first byte look negative.</summary>
    private const byte HighBit = 0x80;

    /// <summary>The serial number shape with the high bit set.</summary>
    private const int HighBitSerial = 1;

    /// <summary>The serial number shape with a leading zero byte.</summary>
    private const int LeadingZeroSerial = 2;

    /// <summary>The serial number shape of one byte.</summary>
    private const int SingleByteSerial = 3;

    /// <summary>The name shape of a few characters.</summary>
    private const int ShortNameShape = 1;

    /// <summary>The name shape close to the one-byte length limit.</summary>
    private const int NearLimitShape = 2;

    /// <summary>The name shape past the one-byte length limit.</summary>
    private const int LongNameShape = 3;

    /// <summary>The fewest bytes in a random serial number.</summary>
    private const int MinSerialBytes = 2;

    /// <summary>The seconds in a year, the widest the signing time moves from the fixed time.</summary>
    private const int SecondsInYear = 31_536_000;

    /// <summary>The key size of the RSA keys.</summary>
    private const int RsaBits = 2048;

    /// <summary>The number of name shapes: none, short, near a one-byte length, past it, past two bytes.</summary>
    private const int NameShapes = 5;

    /// <summary>The number of serial number shapes: random, high bit, leading zero byte, single byte.</summary>
    private const int SerialShapes = 4;

    /// <summary>The characters in a short name.</summary>
    private const int ShortName = 3;

    /// <summary>The characters in a name close to the one-byte length limit.</summary>
    private const int NearLimitName = 60;

    /// <summary>The RDNs in a name that pushes the name past 127 bytes.</summary>
    private const int LongNameParts = 3;

    /// <summary>The RDNs in a name that pushes the name past 255 bytes.</summary>
    private const int VeryLongNameParts = 6;

    /// <summary>The longest part of a name.</summary>
    private const int PartLength = 60;

    /// <summary>The kinds of key that are signed with.</summary>
    public enum KeyKind
    {
        /// <summary>An ECDSA key on P-256.</summary>
        P256 = 0,

        /// <summary>An ECDSA key on P-384.</summary>
        P384 = 1,

        /// <summary>An ECDSA key on P-521.</summary>
        P521 = 2,

        /// <summary>An RSA key signing with PKCS #1 v1.5.</summary>
        Rsa = 3,

        /// <summary>An RSA key signing with PSS.</summary>
        RsaPss = 4,
    }

    /// <summary>Every container is strict DER, decodes and verifies, whatever the key, digest, format, name and serial number.</summary>
    /// <param name="kind">The key kind.</param>
    /// <param name="digest">The digest.</param>
    /// <param name="format">The format.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(KeyKind.P256, PdfDigestMethod.Sha256, PdfSignatureFormat.Pkcs7Detached)]
    [Arguments(KeyKind.P256, PdfDigestMethod.Sha256, PdfSignatureFormat.CadesDetached)]
    [Arguments(KeyKind.P384, PdfDigestMethod.Sha384, PdfSignatureFormat.Pkcs7Detached)]
    [Arguments(KeyKind.P384, PdfDigestMethod.Sha384, PdfSignatureFormat.CadesDetached)]
    [Arguments(KeyKind.P521, PdfDigestMethod.Sha512, PdfSignatureFormat.Pkcs7Detached)]
    [Arguments(KeyKind.P521, PdfDigestMethod.Sha512, PdfSignatureFormat.CadesDetached)]
    [Arguments(KeyKind.Rsa, PdfDigestMethod.Sha256, PdfSignatureFormat.Pkcs7Detached)]
    [Arguments(KeyKind.Rsa, PdfDigestMethod.Sha384, PdfSignatureFormat.CadesDetached)]
    [Arguments(KeyKind.Rsa, PdfDigestMethod.Sha512, PdfSignatureFormat.Pkcs7Detached)]
    [Arguments(KeyKind.RsaPss, PdfDigestMethod.Sha256, PdfSignatureFormat.CadesDetached)]
    [Arguments(KeyKind.RsaPss, PdfDigestMethod.Sha512, PdfSignatureFormat.Pkcs7Detached)]
    public async Task ContainersAreStrictDerAndVerify(KeyKind kind, PdfDigestMethod digest, PdfSignatureFormat format)
    {
        var iterations = Iterations(kind);
        var failures = new List<string>();
        for (var i = 0; i < iterations; i++)
        {
            var problem = SignAndCheck(kind, digest, format, i);
            if (problem is not null)
            {
                failures.Add($"#{i}: {problem}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Gets the signatures to make for a key kind.</summary>
    /// <param name="kind">The key kind.</param>
    /// <returns>The number.</returns>
    private static int Iterations(KeyKind kind)
    {
        var rsa = kind is KeyKind.Rsa or KeyKind.RsaPss;
        if (int.TryParse(Environment.GetEnvironmentVariable(IterationsVariable), CultureInfo.InvariantCulture, out var configured) && configured > 0)
        {
            return rsa ? Math.Max(1, configured / DefaultIterations * DefaultRsaIterations) : configured;
        }

        return rsa ? DefaultRsaIterations : DefaultIterations;
    }

    /// <summary>Signs once and checks the container.</summary>
    /// <param name="kind">The key kind.</param>
    /// <param name="digest">The digest.</param>
    /// <param name="format">The format.</param>
    /// <param name="iteration">The iteration number, which picks the shape of the name and serial number.</param>
    /// <returns><see langword="null"/> when the container is good, or what is wrong with it.</returns>
    private static string? SignAndCheck(KeyKind kind, PdfDigestMethod digest, PdfSignatureFormat format, int iteration)
    {
        try
        {
            using var certificate = NewCertificate(kind, iteration);
            var options = new PdfSigningOptions { Format = format, Digest = digest, SigningTime = SigningFixtures.Time.AddSeconds(RandomNumberGenerator.GetInt32(SecondsInYear)) };
            var signer = new PdfCertificateSigner(certificate, kind == KeyKind.RsaPss);
            using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
            var signed = PdfDocumentSigning.Sign(document, options, signer);
            var covered = SigningFixtures.SignedBytes(signed.Bytes, out var container);
            return Verify(container, covered);
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException or ArgumentException)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    /// <summary>Checks a container as strict DER and with the framework.</summary>
    /// <param name="container">The container.</param>
    /// <param name="covered">The bytes the signature covers.</param>
    /// <returns><see langword="null"/> when the container is good, or what is wrong with it.</returns>
    private static string? Verify(byte[] container, byte[] covered)
    {
        var strict = StrictDer.Check(container);
        if (strict is not null)
        {
            return $"not strict DER ({strict}): {Convert.ToHexString(container)}";
        }

        var cms = new SignedCms(new(covered), true);
        cms.Decode(container);
        cms.CheckSignature(true);
        return null;
    }

    /// <summary>Makes a self-signed certificate with a fresh key and a name and serial number of a shape that depends on the iteration.</summary>
    /// <param name="kind">The key kind.</param>
    /// <param name="iteration">The iteration number.</param>
    /// <returns>The certificate with its private key.</returns>
    private static X509Certificate2 NewCertificate(KeyKind kind, int iteration)
    {
        var name = new X500DistinguishedName(NameOf(iteration % NameShapes));
        var serial = SerialOf((iteration / NameShapes) % SerialShapes);
        var notBefore = SigningFixtures.Time.AddDays(-1);
        var notAfter = SigningFixtures.Time.AddYears(1);
        if (kind is KeyKind.Rsa or KeyKind.RsaPss)
        {
            using var rsa = RSA.Create(RsaBits);
            var request = new CertificateRequest(name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var unsigned = request.Create(name, X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1), notBefore, notAfter, serial);
            return unsigned.CopyWithPrivateKey(rsa);
        }

        using var ecdsa = ECDsa.Create(CurveOf(kind));
        var curveRequest = new CertificateRequest(name, ecdsa, HashAlgorithmName.SHA512);
        using var curveUnsigned = curveRequest.Create(name, X509SignatureGenerator.CreateForECDsa(ecdsa), notBefore, notAfter, serial);
        return curveUnsigned.CopyWithPrivateKey(ecdsa);
    }

    /// <summary>Gets the curve of an elliptic-curve key kind.</summary>
    /// <param name="kind">The key kind.</param>
    /// <returns>The curve.</returns>
    private static ECCurve CurveOf(KeyKind kind) => kind switch
    {
        KeyKind.P256 => ECCurve.NamedCurves.nistP256,
        KeyKind.P384 => ECCurve.NamedCurves.nistP384,
        _ => ECCurve.NamedCurves.nistP521,
    };

    /// <summary>Makes a distinguished name of a shape: none, short, near 127 bytes, past 127 bytes, past 255 bytes.</summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The name.</returns>
    private static string NameOf(int shape) => shape switch
    {
        0 => "CN=A",
        ShortNameShape => $"CN={new string('b', ShortName)}",
        NearLimitShape => $"CN={new string('c', NearLimitName)}",
        LongNameShape => Parts(LongNameParts),
        _ => Parts(VeryLongNameParts),
    };

    /// <summary>Makes a name of several long organizational unit parts and a common name.</summary>
    /// <param name="count">The number of parts.</param>
    /// <returns>The name.</returns>
    private static string Parts(int count)
    {
        var parts = new List<string>(count + 1);
        for (var i = 0; i < count; i++)
        {
            parts.Add($"OU={new string((char)('d' + i), PartLength)}");
        }

        parts.Add("CN=Long");
        return string.Join(", ", parts);
    }

    /// <summary>Makes a serial number of a shape: random, high bit set, a leading zero byte, or one byte.</summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The big-endian serial number bytes.</returns>
    private static byte[] SerialOf(int shape)
    {
        var length = RandomNumberGenerator.GetInt32(MinSerialBytes, MaxSerialBytes);
        var serial = RandomNumberGenerator.GetBytes(length);
        switch (shape)
        {
            case HighBitSerial:
            {
                serial[0] |= HighBit;
                break;
            }

            case LeadingZeroSerial:
            {
                serial[0] = 0;
                serial[1] |= HighBit;
                break;
            }

            case SingleByteSerial:
            {
                serial = [(byte)((serial[0] & (HighBit - 1)) | 1)];
                break;
            }

            default:
            {
                serial[0] &= HighBit - 1;
                serial[0] |= 1;
                break;
            }
        }

        return serial;
    }
}
