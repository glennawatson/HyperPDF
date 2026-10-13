// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Builds certificate chains with <see cref="X509Chain"/>, using the signature's and the security store's certificates as
/// extra candidates. With the default options it never touches the network: downloads and online revocation are off,
/// and revocation comes from the store's saved CRLs and OCSP responses.
/// </summary>
internal static class PdfChainBuilder
{
    /// <summary>Builds the chain for a certificate.</summary>
    /// <param name="certificate">The leaf certificate.</param>
    /// <param name="included">Certificates the signature or token carries.</param>
    /// <param name="context">The store and options.</param>
    /// <param name="at">The time to check at.</param>
    /// <returns>The chain result.</returns>
    internal static PdfChainResult Build(X509Certificate2 certificate, X509Certificate2Collection included, CmsContext context, DateTimeOffset at)
    {
        if (!CmsChainSupport.CanBuild(certificate))
        {
            // This platform's chain engine cannot build a chain for the certificate, so it cannot be trusted here.
            return new(false, X509ChainStatusFlags.PartialChain, [X509CertificateLoader.LoadCertificate(certificate.RawData)], PdfRevocationStatus.Unknown, at);
        }

        using var chain = new X509Chain();
        Configure(chain.ChainPolicy, included, context, at);
        var trusted = chain.Build(certificate);
        var status = X509ChainStatusFlags.NoError;
        foreach (var element in chain.ChainStatus)
        {
            status |= element.Status;
        }

        // The chain owns its elements' certificates; the result keeps copies that outlive it.
        var elements = chain.ChainElements;
        var certificates = new X509Certificate2[elements.Count];
        for (var i = 0; i < certificates.Length; i++)
        {
            certificates[i] = X509CertificateLoader.LoadCertificate(elements[i].Certificate.RawData);
        }

        return new(trusted, status, certificates, PdfRevocationChecker.Check(certificate, Issuer(certificate, certificates), context.Store), at);
    }

    /// <summary>Gets a certificate's issuer from its chain.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="chain">The chain, leaf first.</param>
    /// <returns>The issuer, the certificate itself when self-issued, or <see langword="null"/>.</returns>
    private static X509Certificate2? Issuer(X509Certificate2 certificate, X509Certificate2[] chain)
    {
        if (chain.Length > 1)
        {
            return chain[1];
        }

        return IsSelfIssued(certificate) ? certificate : null;
    }

    /// <summary>Sets the chain policy.</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="included">Certificates the signature or token carries.</param>
    /// <param name="context">The store and options.</param>
    /// <param name="at">The time to check at.</param>
    private static void Configure(X509ChainPolicy policy, X509Certificate2Collection included, CmsContext context, DateTimeOffset at)
    {
        var online = context.Options.AllowNetwork;
        policy.RevocationMode = online ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
        policy.DisableCertificateDownloads = !online;
        policy.VerificationTime = at.UtcDateTime;
        policy.VerificationTimeIgnored = false;
        policy.ExtraStore.AddRange(included);
        policy.ExtraStore.AddRange(context.StoreCertificates);
        if (context.Options.TrustedRoots.Count <= 0)
        {
            return;
        }

        policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        policy.CustomTrustStore.AddRange(context.Options.TrustedRoots);
    }

    /// <summary>Determines whether a certificate names itself as issuer.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> when subject and issuer match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSelfIssued(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);
}
