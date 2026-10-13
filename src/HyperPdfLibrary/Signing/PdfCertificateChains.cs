// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Gathers the certificates that lead from a certificate to its root, to embed in a signature. Whether the chain is trusted is
/// the verifier's question, so nothing is looked up online.
/// </summary>
internal static class PdfCertificateChains
{
    /// <summary>Builds the chain of a certificate from the system's stores and some extra certificates.</summary>
    /// <param name="leaf">The certificate.</param>
    /// <param name="extras">Certificates to use as well as the system's stores.</param>
    /// <returns>The chain's certificates, the leaf first. The chain may stop short of a root when an issuer cannot be found.</returns>
    internal static List<X509Certificate2> Build(X509Certificate2 leaf, X509Certificate2Collection extras)
    {
        if (!CmsChainSupport.CanBuild(leaf))
        {
            // This platform's chain engine cannot build a chain for the certificate; the path is the certificate alone.
            return [X509CertificateLoader.LoadCertificate(leaf.RawData)];
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
        chain.ChainPolicy.ExtraStore.AddRange(extras);

        // The chain's status says whether it is trusted; the elements are the path either way.
        _ = chain.Build(leaf);
        var elements = new List<X509Certificate2>(chain.ChainElements.Count);
        foreach (var element in chain.ChainElements)
        {
            elements.Add(X509CertificateLoader.LoadCertificate(element.Certificate.RawData));
        }

        return elements;
    }

    /// <summary>Determines whether a certificate is its own issuer.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSelfSigned(X509Certificate2 certificate) => certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);
}
