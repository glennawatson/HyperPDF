// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.TestAssets;

/// <summary>
/// A timestamp authority for tests: it issues RFC 3161 tokens (a CMS-signed TSTInfo over a SHA-256 hash) with its own
/// self-signed certificate, at a time the test chooses, without any network.
/// </summary>
public sealed class TestTimestampAuthority : ISignatureTimestamper, IDisposable
{
    /// <summary>The authority's name.</summary>
    public static readonly string Name = "HyperPDF Test Timestamps";

    /// <summary>The RSA key size.</summary>
    private const int KeyBits = 2048;

    /// <summary>The content type of a timestamp token's content (id-ct-TSTInfo).</summary>
    private const string TstInfoOid = "1.2.840.113549.1.9.16.1.4";

    /// <summary>The signed attribute naming the authority's certificate (id-aa-signingCertificateV2).</summary>
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";

    /// <summary>The timestamping extended key usage.</summary>
    private const string TimeStampingOid = "1.3.6.1.5.5.7.3.8";

    /// <summary>The SHA-256 algorithm.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The SHA-384 algorithm.</summary>
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";

    /// <summary>The SHA-512 algorithm.</summary>
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";

    /// <summary>The SHA3-384 algorithm.</summary>
    private const string Sha3x384Oid = "2.16.840.1.101.3.4.2.9";

    /// <summary>The SHA3-512 algorithm.</summary>
    private const string Sha3x512Oid = "2.16.840.1.101.3.4.2.10";

    /// <summary>The SHA3-256 algorithm.</summary>
    private const string Sha3x256Oid = "2.16.840.1.101.3.4.2.8";

    /// <summary>The id-shake256 algorithm.</summary>
    private const string Shake256Oid = "2.16.840.1.101.3.4.2.12";

    /// <summary>The SHAKE256 output length PDF uses.</summary>
    private const int Shake256Length = 64;

    /// <summary>A policy identifier for the tokens.</summary>
    private const string PolicyOid = "1.3.6.1.4.1.99999.1";

    /// <summary>The certificate's validity before and after now, in years.</summary>
    private const int ValidYears = 5;

    /// <summary>The time the tokens state.</summary>
    private readonly DateTimeOffset _time;

    /// <summary>The next serial number.</summary>
    private long _serial;

    /// <summary>Initializes a new instance of the <see cref="TestTimestampAuthority"/> class.</summary>
    /// <param name="time">The time the tokens state.</param>
    public TestTimestampAuthority(DateTimeOffset time)
    {
        _time = time;
        using var key = RSA.Create(KeyBits);
        var request = new CertificateRequest($"CN={Name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new(TimeStampingOid)], true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        Certificate = request.CreateSelfSigned(time.AddYears(-ValidYears), time.AddYears(ValidYears));
    }

    /// <summary>Gets the authority's certificate, to be trusted by the test.</summary>
    public X509Certificate2 Certificate { get; }

    /// <summary>Gets the number of tokens issued, so tests can tell a timestamp was asked for.</summary>
    public int Issued { get; private set; }

    /// <summary>
    /// Gets the message imprint's digest OID: SHA-256 (the default), SHA3-256 (2.16.840.1.101.3.4.2.8) or SHAKE256
    /// (2.16.840.1.101.3.4.2.12, 64 bytes).
    /// </summary>
    public string ImprintOid { get; init; } = Sha256Oid;

    /// <inheritdoc/>
    public int EstimatedTokenSize => 0;

    /// <summary>Gets a token over some data, hashed with <see cref="ImprintOid"/>.</summary>
    /// <param name="data">The data to stamp.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The DER token.</returns>
    public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
        Stamp(Imprint(data.Span), ImprintOid, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, SigningDigest digest, CancellationToken cancellationToken) =>
        Stamp(hash.ToArray(), DigestOid(digest), cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => Certificate.Dispose();

    /// <summary>Gets the object identifier of a digest.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The identifier.</returns>
    private static string DigestOid(SigningDigest digest) => digest switch
    {
        SigningDigest.Sha3_256 => Sha3x256Oid,
        SigningDigest.Shake256 => Shake256Oid,
        SigningDigest.Sha384 => Sha384Oid,
        SigningDigest.Sha512 => Sha512Oid,
        SigningDigest.Sha3_384 => Sha3x384Oid,
        SigningDigest.Sha3_512 => Sha3x512Oid,
        _ => Sha256Oid,
    };

    /// <summary>Signs a token over a hash.</summary>
    /// <param name="hash">The hash to stamp.</param>
    /// <param name="imprintOid">The digest that made the hash.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The DER token.</returns>
    private ValueTask<byte[]> Stamp(byte[] hash, string imprintOid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = new ContentInfo(new(TstInfoOid), TstInfo(hash, imprintOid));
        var cms = new SignedCms(content, false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, Certificate) { DigestAlgorithm = new(Sha256Oid), IncludeOption = X509IncludeOption.EndCertOnly };
        _ = signer.SignedAttributes.Add(new AsnEncodedData(SigningCertificateV2Oid, SigningCertificateV2()));
        cms.ComputeSignature(signer, true);
        Issued++;
        return ValueTask.FromResult(cms.Encode());
    }

    /// <summary>Encodes the TSTInfo: version, policy, message imprint, serial number and time.</summary>
    /// <param name="hash">The hash being stamped.</param>
    /// <param name="imprintOid">The digest that made the hash.</param>
    /// <returns>The DER bytes.</returns>
    private byte[] TstInfo(byte[] hash, string imprintOid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteObjectIdentifier(PolicyOid);
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(imprintOid);
                }

                writer.WriteOctetString(hash);
            }

            writer.WriteInteger(new BigInteger(Interlocked.Increment(ref _serial)));
            writer.WriteGeneralizedTime(_time, true);
        }

        return writer.Encode();
    }

    /// <summary>Hashes the stamped data with the imprint's digest.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="NotSupportedException">The imprint OID is not one the authority can compute.</exception>
    private byte[] Imprint(ReadOnlySpan<byte> data) => ImprintOid switch
    {
        Sha256Oid => SHA256.HashData(data),
        Sha3x256Oid => SHA3_256.HashData(data),
        Shake256Oid => Shake256.HashData(data, Shake256Length),
        _ => throw new NotSupportedException($"The test authority cannot hash with {ImprintOid}."),
    };

    /// <summary>Encodes SigningCertificateV2 with one ESSCertIDv2 holding the certificate's SHA-256 hash.</summary>
    /// <returns>The DER bytes.</returns>
    private byte[] SigningCertificateV2()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                using (writer.PushSequence())
                {
                    writer.WriteOctetString(SHA256.HashData(Certificate.RawData));
                }
            }
        }

        return writer.Encode();
    }
}
