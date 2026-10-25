// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.ViewModels.Signing;

/// <summary>Reads the public certificates of an external signer from DER and PEM files and puts them in chain order.</summary>
internal static class SignerCertificateFiles
{
    /// <summary>Gets the start of a PEM block, which tells a text certificate file from a binary one.</summary>
    private static ReadOnlySpan<byte> PemMarker => "-----BEGIN"u8;

    /// <summary>Reads the certificates of the files and finds the signing certificate.</summary>
    /// <param name="paths">The certificate files; each may hold one certificate or several.</param>
    /// <returns>The signer: the signing certificate first, then the certificates that lead to a root.</returns>
    /// <exception cref="CryptographicException">A file holds no certificate, or does not hold a certificate at all.</exception>
    /// <exception cref="IOException">A file could not be read.</exception>
    internal static LoadedSigner Load(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var found = new List<X509Certificate2>();
        try
        {
            foreach (var path in paths)
            {
                found.AddRange(ReadFile(path));
            }

            if (found.Count == 0)
            {
                throw new CryptographicException(UiText.Get("The file holds no certificate."));
            }

            var ordered = Order(found);
            var subject = ordered[0].GetNameInfo(X509NameType.SimpleName, false);
            var certificates = new byte[ordered.Count][];
            for (var i = 0; i < certificates.Length; i++)
            {
                certificates[i] = ordered[i].RawData;
            }

            return new(new(certificates, KindOf(ordered[0])), subject);
        }
        finally
        {
            foreach (var certificate in found)
            {
                certificate.Dispose();
            }
        }
    }

    /// <summary>Gets the kind of signature a certificate's public key makes.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <returns>ECDSA for an elliptic curve key, otherwise RSA with PKCS #1 padding.</returns>
    internal static SigningKeyKind KindOf(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using var ec = certificate.GetECDsaPublicKey();
        return ec is null ? SigningKeyKind.RsaPkcs1 : SigningKeyKind.Ecdsa;
    }

    /// <summary>Reads the certificates in one file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The certificates, which the caller disposes.</returns>
    private static X509Certificate2Collection ReadFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var collection = new X509Certificate2Collection();
        if (bytes.AsSpan().TrimStart((byte)' ').StartsWith(PemMarker))
        {
            collection.ImportFromPem(System.Text.Encoding.UTF8.GetString(bytes));
            return collection;
        }

        _ = collection.Add(X509CertificateLoader.LoadCertificate(bytes));
        return collection;
    }

    /// <summary>Puts the signing certificate first, followed by each certificate that issued the one before it.</summary>
    /// <param name="certificates">The certificates in file order.</param>
    /// <returns>The certificates in chain order; any that do not belong to the chain come last.</returns>
    private static List<X509Certificate2> Order(List<X509Certificate2> certificates)
    {
        var leaf = FindLeaf(certificates);
        var ordered = new List<X509Certificate2> { leaf };
        var current = leaf;
        while (FindIssuer(certificates, ordered, current) is { } issuer)
        {
            ordered.Add(issuer);
            current = issuer;
        }

        foreach (var certificate in certificates)
        {
            if (!ordered.Contains(certificate))
            {
                ordered.Add(certificate);
            }
        }

        return ordered;
    }

    /// <summary>Finds the certificate that did not issue any other: the signer's own.</summary>
    /// <param name="certificates">The certificates.</param>
    /// <returns>The signing certificate; the first one when they all issued each other.</returns>
    private static X509Certificate2 FindLeaf(List<X509Certificate2> certificates)
    {
        foreach (var candidate in certificates)
        {
            if (!IssuedAnother(certificates, candidate))
            {
                return candidate;
            }
        }

        return certificates[0];
    }

    /// <summary>Determines whether a certificate issued any other certificate of the list.</summary>
    /// <param name="certificates">The certificates.</param>
    /// <param name="candidate">The possible issuer.</param>
    /// <returns><see langword="true"/> when another certificate names it as its issuer.</returns>
    private static bool IssuedAnother(List<X509Certificate2> certificates, X509Certificate2 candidate)
    {
        foreach (var other in certificates)
        {
            if (!ReferenceEquals(other, candidate) && string.Equals(other.Issuer, candidate.Subject, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the certificate that issued another, which is not in the chain yet.</summary>
    /// <param name="certificates">The certificates.</param>
    /// <param name="chain">The chain so far.</param>
    /// <param name="current">The last certificate of the chain.</param>
    /// <returns>The issuer, or <see langword="null"/> when the chain ends.</returns>
    private static X509Certificate2? FindIssuer(List<X509Certificate2> certificates, List<X509Certificate2> chain, X509Certificate2 current)
    {
        foreach (var candidate in certificates)
        {
            if (!chain.Contains(candidate) && string.Equals(candidate.Subject, current.Issuer, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
