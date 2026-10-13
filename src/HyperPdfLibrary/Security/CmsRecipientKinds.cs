// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>Works out how a certificate can receive an encrypted PDF.</summary>
internal static class CmsRecipientKinds
{
    /// <summary>The key algorithm OID of RSA.</summary>
    private const string RsaKey = "1.2.840.113549.1.1.1";

    /// <summary>The key algorithm OID of elliptic curve keys.</summary>
    private const string EllipticCurveKey = "1.2.840.10045.2.1";

    /// <summary>
    /// Classifies a certificate. A certificate with a key usage extension shall allow the use (RFC 5280 4.2.1.3): key or data
    /// encipherment for RSA, key agreement for elliptic curve keys.
    /// </summary>
    /// <param name="certificate">The recipient's certificate.</param>
    /// <returns>The kind of recipient it can be.</returns>
    internal static CmsRecipientKind Classify(X509Certificate2 certificate)
    {
        var usage = KeyUsageOf(certificate);
        return certificate.PublicKey.Oid.Value switch
        {
            RsaKey when Allows(usage, X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment) => CmsRecipientKind.Rsa,
            EllipticCurveKey when Allows(usage, X509KeyUsageFlags.KeyAgreement) => CmsRecipientKind.EllipticCurve,
            _ => CmsRecipientKind.Unsupported,
        };
    }

    /// <summary>Finds a certificate's key usage extension.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The extension, or <see langword="null"/> when the certificate has none.</returns>
    private static X509KeyUsageExtension? KeyUsageOf(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509KeyUsageExtension usage)
            {
                return usage;
            }
        }

        return null;
    }

    /// <summary>Determines whether a key usage extension, if there is one, allows any of the wanted uses.</summary>
    /// <param name="usage">The extension, or <see langword="null"/> when the certificate has none.</param>
    /// <param name="wanted">The uses, any of which is enough.</param>
    /// <returns><see langword="true"/> when there is no extension or it has one of the uses.</returns>
    private static bool Allows(X509KeyUsageExtension? usage, X509KeyUsageFlags wanted) => usage is null || (usage.KeyUsages & wanted) != 0;
}
