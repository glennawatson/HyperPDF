// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// The public part of a signer: the certificates to embed and the kind of signature its key makes. An external signer
/// (a token or a remote service) gives this to the library, which builds the container and asks the signer to sign.
/// </summary>
/// <param name="Certificates">The DER-encoded certificates: the signing certificate first, then the chain that leads to a trusted root.</param>
/// <param name="Algorithm">The kind of signature the key makes.</param>
[DebuggerDisplay("PdfSignerIdentity: {Algorithm} {Certificates.Count} certificates")]
public sealed record PdfSignerIdentity(IReadOnlyList<byte[]> Certificates, PdfSignatureAlgorithm Algorithm)
{
    /// <summary>Gets the identity of a signature provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public static PdfSignerIdentity From(IPdfSignatureProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        // An in-process signer never changes its certificates, so it hands out the one identity it made.
        return provider is PdfCertificateSigner signer ? signer.Identity : new(provider.Certificates, provider.Algorithm);
    }
}
