// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Self-signed recipient certificates made on the spot, with a private key for opening a file and a public-only copy for encrypting one.</summary>
internal static class RecipientCertificates
{
    /// <summary>The size of the RSA keys.</summary>
    private const int RsaBits = 2048;

    /// <summary>The days a certificate is valid, either side of now.</summary>
    private const int ValidDays = 30;

    /// <summary>Makes an RSA recipient that allows key encipherment.</summary>
    /// <returns>The certificate with its private key.</returns>
    internal static X509Certificate2 Rsa() => Rsa(X509KeyUsageFlags.KeyEncipherment);

    /// <summary>Makes an RSA certificate with a key usage.</summary>
    /// <param name="usage">The key usage extension's flags.</param>
    /// <returns>The certificate with its private key.</returns>
    internal static X509Certificate2 Rsa(X509KeyUsageFlags usage)
    {
        using var key = RSA.Create(RsaBits);
        var request = new CertificateRequest("CN=RSA Recipient", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, true));
        return Sign(request);
    }

    /// <summary>Makes an elliptic curve recipient on a named curve that allows key agreement.</summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The certificate with its private key as an ECDH key.</returns>
    internal static X509Certificate2 EllipticCurve(ECCurve curve)
    {
        using var signing = ECDsa.Create(curve);
        var request = new CertificateRequest("CN=EC Recipient", signing, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.DigitalSignature, true));
        using var signed = Sign(request);
        using var publicOnly = PublicOnly(signed);

        // Every platform can use an ECDH key for key agreement; a key made for signing is not always usable for it.
        using var agreement = ECDiffieHellman.Create();
        agreement.ImportParameters(signing.ExportParameters(true));
        return publicOnly.CopyWithPrivateKey(agreement);
    }

    /// <summary>Makes the copy of a certificate that has no private key, as a person who encrypts for someone else has it.</summary>
    /// <param name="certificate">The certificate with its key.</param>
    /// <returns>The public-only certificate.</returns>
    internal static X509Certificate2 PublicOnly(X509Certificate2 certificate) => X509CertificateLoader.LoadCertificate(certificate.RawData);

    /// <summary>Signs a request with its own key, valid around now.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The certificate.</returns>
    private static X509Certificate2 Sign(CertificateRequest request)
    {
        var now = TimeProvider.System.GetUtcNow();
        return request.CreateSelfSigned(now.AddDays(-ValidDays), now.AddDays(ValidDays));
    }
}
