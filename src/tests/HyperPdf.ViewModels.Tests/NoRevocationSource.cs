// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures.Signing;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A revocation source that finds nothing, so validation data is built without the network.</summary>
internal sealed class NoRevocationSource : ISignatureRevocationSource
{
    /// <inheritdoc/>
    public IObservable<byte[]?> GetOcspResponse(byte[] certificate, byte[] issuer) => Signal.Return<byte[]?>(null);

    /// <inheritdoc/>
    public IObservable<byte[]?> GetCrl(byte[] certificate) => Signal.Return<byte[]?>(null);

    /// <inheritdoc/>
    public IObservable<byte[]?> GetIssuer(byte[] certificate) => GetCrl(certificate);
}
