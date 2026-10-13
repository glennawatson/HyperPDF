// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Cms;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Signs with an in-process certificate that has a private key, such as one loaded from a PFX file.</summary>
[DebuggerDisplay("PdfCertificateSigner: {_certificate.Subject}")]
public sealed class PdfCertificateSigner : IPdfSignatureProvider
{
    /// <summary>The signing certificate.</summary>
    private readonly X509Certificate2 _certificate;

    /// <summary>The certificates to embed, signing certificate first.</summary>
    private readonly byte[][] _chain;

    /// <summary>Initializes a new instance of the <see cref="PdfCertificateSigner"/> class that signs RSA keys with PKCS #1 v1.5 padding.</summary>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="certificate"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The certificate has no private key.</exception>
    /// <exception cref="NotSupportedException">The key is neither an RSA nor an ECDSA key.</exception>
    public PdfCertificateSigner(X509Certificate2 certificate)
        : this(certificate, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfCertificateSigner"/> class.</summary>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="usePss">Whether an RSA key signs with PSS padding instead of PKCS #1 v1.5; ignored for an ECDSA key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="certificate"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The certificate has no private key.</exception>
    /// <exception cref="NotSupportedException">The key is neither an RSA nor an ECDSA key.</exception>
    public PdfCertificateSigner(X509Certificate2 certificate, bool usePss)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!certificate.HasPrivateKey)
        {
            throw new ArgumentException("The certificate has no private key, so it cannot sign.", nameof(certificate));
        }

        _certificate = certificate;
        Algorithm = AlgorithmOf(certificate, usePss);
        _chain = BuildChain(certificate);
        Identity = new(_chain, Algorithm);
    }

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> Certificates => _chain;

    /// <inheritdoc/>
    public PdfSignatureAlgorithm Algorithm { get; }

    /// <summary>Gets the public part of this signer: its certificates and the kind of signature it makes.</summary>
    internal PdfSignerIdentity Identity { get; }

    /// <inheritdoc/>
    public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, PdfDigestMethod digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Sign(data.Span, digest));
    }

    /// <summary>Signs some bytes without waiting, as the key is in this process.</summary>
    /// <param name="data">The bytes to sign.</param>
    /// <param name="digest">The digest to hash with.</param>
    /// <returns>The signature value.</returns>
    /// <exception cref="NotSupportedException">This computer cannot compute the digest, or the key cannot sign with it.</exception>
    public byte[] Sign(ReadOnlySpan<byte> data, PdfDigestMethod digest)
    {
        var cms = PdfSigningDigests.ToCms(digest);
        if (CmsDigests.Status(cms) != CmsDigestStatus.Available)
        {
            throw new NotSupportedException($"This computer cannot compute the {PdfSigningDigests.DisplayName(digest)} digest. Choose SHA-256 instead.");
        }

        return Algorithm == PdfSignatureAlgorithm.Ecdsa ? SignEcdsa(data, cms) : SignRsa(data, cms);
    }

    /// <summary>Gets the algorithm for a certificate's key.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="usePss">Whether an RSA key uses PSS padding.</param>
    /// <returns>The algorithm.</returns>
    /// <exception cref="NotSupportedException">The key is neither an RSA nor an ECDSA key.</exception>
    private static PdfSignatureAlgorithm AlgorithmOf(X509Certificate2 certificate, bool usePss)
    {
        if (CmsEcRules.IsEllipticCurveKey(certificate))
        {
            return PdfSignatureAlgorithm.Ecdsa;
        }

        using var rsa = certificate.GetRSAPublicKey() ?? throw new NotSupportedException("PDF signing here needs an RSA or an elliptic-curve (ECDSA) key; this certificate's key is neither.");
        return usePss ? PdfSignatureAlgorithm.RsaPss : PdfSignatureAlgorithm.RsaPkcs1;
    }

    /// <summary>Gets the certificates to embed: the signing certificate, then the chain a verifier builds from it.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <returns>The DER certificates.</returns>
    private static byte[][] BuildChain(X509Certificate2 certificate)
    {
        var certificates = new List<byte[]> { certificate.RawData };
        foreach (var element in PdfCertificateChains.Build(certificate, []))
        {
            if (!element.RawDataMemory.Span.SequenceEqual(certificate.RawDataMemory.Span))
            {
                certificates.Add(element.RawData);
            }
        }

        return [.. certificates];
    }

    /// <summary>Signs with an RSA key.</summary>
    /// <param name="data">The bytes to sign.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The signature value.</returns>
    /// <exception cref="NotSupportedException">The private key is not available to sign with.</exception>
    private byte[] SignRsa(ReadOnlySpan<byte> data, CmsDigestAlgorithm digest)
    {
        using var rsa = _certificate.GetRSAPrivateKey() ?? throw new NotSupportedException("The certificate's RSA private key is not available to sign with.");
        var padding = Algorithm == PdfSignatureAlgorithm.RsaPss ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1;
        return rsa.SignData(data, CmsDigests.HashName(digest), padding);
    }

    /// <summary>Signs with an ECDSA key.</summary>
    /// <param name="data">The bytes to sign.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The signature value in the ASN.1 DER form.</returns>
    /// <exception cref="NotSupportedException">The private key is not available to sign with.</exception>
    private byte[] SignEcdsa(ReadOnlySpan<byte> data, CmsDigestAlgorithm digest)
    {
        using var ecdsa = _certificate.GetECDsaPrivateKey() ?? throw new NotSupportedException("The certificate's elliptic-curve private key is not available to sign with.");
        if (digest != CmsDigestAlgorithm.Shake256)
        {
            return ecdsa.SignData(data, CmsDigests.HashName(digest), DSASignatureFormat.Rfc3279DerSequence);
        }

        // .NET has no hash name for SHAKE256, so the hash is made here and signed as it is (id-ecdsa-with-shake256, RFC 8702).
        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        _ = CmsDigests.Hash(digest, data, hash);
        return ecdsa.SignHash(hash, DSASignatureFormat.Rfc3279DerSequence);
    }
}
