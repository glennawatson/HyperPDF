// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Finds the revocation data and missing issuer certificates that long-term validation data needs.</summary>
public interface ISignatureRevocationSource
{
    /// <summary>Gets an OCSP response (RFC 6960) saying whether a certificate was revoked.</summary>
    /// <param name="certificate">The DER certificate.</param>
    /// <param name="issuer">The DER certificate of its issuer.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The DER response, or <see langword="null"/> when none can be had.</returns>
    ValueTask<byte[]?> GetOcspResponseAsync(byte[] certificate, byte[] issuer, CancellationToken cancellationToken);

    /// <summary>Gets a certificate revocation list (RFC 5280) that covers a certificate.</summary>
    /// <param name="certificate">The DER certificate.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The DER list, or <see langword="null"/> when none can be had.</returns>
    ValueTask<byte[]?> GetCrlAsync(byte[] certificate, CancellationToken cancellationToken);

    /// <summary>Gets the issuer of a certificate when the chain is missing it.</summary>
    /// <param name="certificate">The DER certificate.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The DER issuer certificate, or <see langword="null"/> when none can be had.</returns>
    ValueTask<byte[]?> GetIssuerAsync(byte[] certificate, CancellationToken cancellationToken);
}
