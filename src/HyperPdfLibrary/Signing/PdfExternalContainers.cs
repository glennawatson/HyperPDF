// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace HyperPdfLibrary.Signing;

/// <summary>Works on signature containers that a signing service built.</summary>
internal static class PdfExternalContainers
{
    /// <summary>The signatureTimeStampToken unsigned attribute.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>Reads the signature value of a container's first signer.</summary>
    /// <param name="container">The DER container.</param>
    /// <returns>The signature value.</returns>
    /// <exception cref="PdfException">The bytes are not a signature container with a signer.</exception>
    internal static byte[] SignatureValue(byte[] container)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(container);
            return cms.SignerInfos.Count == 0 ? throw Damaged(null) : cms.SignerInfos[0].GetSignature();
        }
        catch (CryptographicException exception)
        {
            throw Damaged(exception);
        }
    }

    /// <summary>Adds a signature timestamp to a container's first signer as an unsigned attribute.</summary>
    /// <param name="container">The DER container.</param>
    /// <param name="token">The DER timestamp token.</param>
    /// <returns>The container with the token.</returns>
    /// <exception cref="PdfException">The bytes are not a signature container with a signer.</exception>
    internal static byte[] AddTimestamp(byte[] container, byte[] token)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(container);
            cms.SignerInfos[0].AddUnsignedAttribute(new(TimestampTokenOid, token));
            return cms.Encode();
        }
        catch (CryptographicException exception)
        {
            throw Damaged(exception);
        }
    }

    /// <summary>Makes the exception for a container that cannot be read.</summary>
    /// <param name="inner">The cause, or <see langword="null"/>.</param>
    /// <returns>The exception.</returns>
    private static PdfException Damaged(Exception? inner) =>
        inner is null
            ? new(PdfError.Format, "The signature container has no signer.")
            : new("The signature container could not be read.", inner);
}
