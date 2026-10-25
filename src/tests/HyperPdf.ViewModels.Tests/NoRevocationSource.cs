// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A revocation source that finds nothing, so validation data is built without the network.</summary>
internal sealed class NoRevocationSource : ISignatureRevocationSource
{
    /// <inheritdoc/>
    public ValueTask<byte[]?> GetOcspResponseAsync(byte[] certificate, byte[] issuer, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);

    /// <inheritdoc/>
    public ValueTask<byte[]?> GetCrlAsync(byte[] certificate, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);

    /// <inheritdoc/>
    public ValueTask<byte[]?> GetIssuerAsync(byte[] certificate, CancellationToken cancellationToken) => GetCrlAsync(certificate, cancellationToken);
}
