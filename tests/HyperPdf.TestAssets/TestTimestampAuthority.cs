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

    /// <inheritdoc/>
    public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = new ContentInfo(new(TstInfoOid), TstInfo(SHA256.HashData(data.Span)));
        var cms = new SignedCms(content, false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, Certificate) { DigestAlgorithm = new(Sha256Oid), IncludeOption = X509IncludeOption.EndCertOnly };
        _ = signer.SignedAttributes.Add(new AsnEncodedData(SigningCertificateV2Oid, SigningCertificateV2()));
        cms.ComputeSignature(signer, true);
        Issued++;
        return ValueTask.FromResult(cms.Encode());
    }

    /// <inheritdoc/>
    public void Dispose() => Certificate.Dispose();

    /// <summary>Encodes the TSTInfo: version, policy, message imprint, serial number and time.</summary>
    /// <param name="hash">The SHA-256 hash being stamped.</param>
    /// <returns>The DER bytes.</returns>
    private byte[] TstInfo(byte[] hash)
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
                    writer.WriteObjectIdentifier(Sha256Oid);
                }

                writer.WriteOctetString(hash);
            }

            writer.WriteInteger(new BigInteger(Interlocked.Increment(ref _serial)));
            writer.WriteGeneralizedTime(_time, true);
        }

        return writer.Encode();
    }

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
