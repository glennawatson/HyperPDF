// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary.Signing;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine;

/// <summary>Lets the library use the app's revocation source.</summary>
[DebuggerDisplay("RevocationSourceAdapter")]
internal sealed class RevocationSourceAdapter : IPdfRevocationSource
{
    /// <summary>The app's revocation source.</summary>
    private readonly ISignatureRevocationSource _source;

    /// <summary>Initializes a new instance of the <see cref="RevocationSourceAdapter"/> class.</summary>
    /// <param name="source">The app's revocation source.</param>
    internal RevocationSourceAdapter(ISignatureRevocationSource source) => _source = source;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<byte[]?> GetOcspResponseAsync(byte[] certificate, byte[] issuer, CancellationToken cancellationToken) =>
        new(_source.GetOcspResponse(certificate, issuer).ToTask(cancellationToken));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<byte[]?> GetCrlAsync(byte[] certificate, CancellationToken cancellationToken) => new(_source.GetCrl(certificate).ToTask(cancellationToken));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<byte[]?> GetIssuerAsync(byte[] certificate, CancellationToken cancellationToken) => new(_source.GetIssuer(certificate).ToTask(cancellationToken));
}
