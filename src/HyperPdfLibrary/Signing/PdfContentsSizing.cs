// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signing;

/// <summary>Estimates how many bytes a signature value needs, so the room reserved for it fits without wasting space.</summary>
internal static class PdfContentsSizing
{
    /// <summary>The bytes of a CMS container that do not depend on the certificates, signature or timestamp: the wrappers, algorithm identifiers and signed attributes.</summary>
    private const int FixedOverhead = 768;

    /// <summary>The bytes added around each embedded certificate.</summary>
    private const int CertificateOverhead = 8;

    /// <summary>The size assumed for a signature timestamp token when the client does not say: a token with its certificate chain.</summary>
    private const int DefaultTimestampSize = 8192;

    /// <summary>The bytes the unsigned attribute adds around a timestamp token.</summary>
    private const int TimestampOverhead = 32;

    /// <summary>The bytes of an ECDSA signature that are not the two integers.</summary>
    private const int EcdsaOverhead = 9;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The integers in an ECDSA signature: r and s.</summary>
    private const int CoordinatesPerSignature = 2;

    /// <summary>The size assumed for a signature when the key is neither RSA nor ECDSA.</summary>
    private const int UnknownSignatureSize = 512;

    /// <summary>The multiple the estimate is rounded up to.</summary>
    private const int Granularity = 64;

    /// <summary>The hexadecimal characters written for one reserved byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The hexadecimal string delimiters.</summary>
    private const int ContentsDelimiters = 2;

    /// <summary>The bytes a PDF MAC token and its attribute add (ISO/TS 32004:2024 6.5.2).</summary>
    private const int MacAllowance = 256;

    /// <summary>Estimates the size of a signature container.</summary>
    /// <param name="certificates">The DER certificates to embed.</param>
    /// <param name="signer">The signing certificate.</param>
    /// <param name="timestampSize">The size of a timestamp token in bytes, or zero for no timestamp.</param>
    /// <param name="withMac">Whether a PDF MAC token is attached.</param>
    /// <param name="headroom">The extra bytes to add.</param>
    /// <returns>The size in bytes, rounded up.</returns>
    internal static int Estimate(IReadOnlyList<byte[]> certificates, in PdfSignerCertificate signer, int timestampSize, bool withMac, int headroom)
    {
        var size = (long)FixedOverhead + SignatureSize(signer) + Math.Max(0, headroom);
        for (var i = 0; i < certificates.Count; i++)
        {
            size += (long)certificates[i].Length + CertificateOverhead;
        }

        if (timestampSize > 0)
        {
            size += (long)timestampSize + TimestampOverhead;
        }

        if (withMac)
        {
            size += MacAllowance;
        }

        return RoundUp(size);
    }

    /// <summary>Gets the size to reserve for a timestamp token.</summary>
    /// <param name="client">The client, or <see langword="null"/> for no timestamp.</param>
    /// <returns>The size in bytes, or zero for no timestamp.</returns>
    internal static int TimestampSize(IPdfTimestampClient? client)
    {
        if (client is null)
        {
            return 0;
        }

        return client.EstimatedTokenSize > 0 ? client.EstimatedTokenSize : DefaultTimestampSize;
    }

    /// <summary>Rounds a size up to the granularity.</summary>
    /// <param name="size">The size.</param>
    /// <returns>The rounded size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RoundUp(int size) => RoundUp((long)size);

    /// <summary>Checks an accumulated reservation before narrowing or allocating it.</summary>
    /// <param name="size">The requested bytes.</param>
    /// <returns>The aligned, representable reservation.</returns>
    /// <exception cref="PdfException">The reservation cannot fit hexadecimal contents in a PDF byte buffer.</exception>
    internal static int RoundUp(long size)
    {
        // A signature byte needs two hexadecimal digits in the output buffer.
        var maximumContents = (Array.MaxLength - ContentsDelimiters) / DigitsPerByte;
        var maximumAligned = maximumContents / Granularity * Granularity;
        if (size < 0 || size > maximumAligned)
        {
            throw new PdfException(PdfError.Format, "The signature reservation is too large for a PDF byte buffer.");
        }

        return (int)(((size + Granularity - 1) / Granularity) * Granularity);
    }

    /// <summary>Gets the size of the signature value a key makes.</summary>
    /// <param name="signer">The signing certificate.</param>
    /// <returns>The size in bytes.</returns>
    private static int SignatureSize(in PdfSignerCertificate signer) => signer switch
    {
        { KeyKind: PdfSignerKeyKind.Rsa, KeyBits: > 0 } => signer.KeyBits / BitsPerByte,
        { KeyKind: PdfSignerKeyKind.EllipticCurve, KeyBits: > 0 } => (int)((CoordinatesPerSignature * (((long)signer.KeyBits + BitsPerByte - 1) / BitsPerByte)) + EcdsaOverhead),
        _ => PlatformSignatureSize(signer.Der),
    };

    /// <summary>Gets the size of the signature value a key makes by asking the platform, for a key whose size the certificate bytes do not give.</summary>
    /// <param name="certificate">The DER certificate.</param>
    /// <returns>The size in bytes.</returns>
    private static int PlatformSignatureSize(byte[] certificate)
    {
        using var signer = X509CertificateLoader.LoadCertificate(certificate);
        using var rsa = signer.GetRSAPublicKey();
        if (rsa is not null)
        {
            return rsa.KeySize / BitsPerByte;
        }

        using var ecdsa = signer.GetECDsaPublicKey();
        if (ecdsa is null)
        {
            return UnknownSignatureSize;
        }

        var coordinate = (ecdsa.KeySize + BitsPerByte - 1) / BitsPerByte;
        return (CoordinatesPerSignature * coordinate) + EcdsaOverhead;
    }
}
