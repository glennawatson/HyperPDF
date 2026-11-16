// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>Provides IDocumentSigner through the owning document.</summary>
internal sealed class HyperPdfSignerService : IDocumentSigner
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfSignerService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfSignerService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SigningPreview Preview(in SigningRequest request) => HyperPdfDocumentSigning.Preview(_owner, request);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<SigningOutcome> Sign(X509Certificate2 certificate, SigningRequest request, ISignatureTimestamper? timestamper) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.SignAsync(_owner, certificate, request, timestamper, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<SigningOutcome> Sign(ISignatureProvider provider, SigningRequest request, ISignatureTimestamper? timestamper) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.SignAsync(_owner, provider, request, timestamper, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<IPendingSignature> Prepare(SignerIdentity identity, SigningRequest request, ISignatureTimestamper? timestamper) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.PrepareAsync(_owner, identity, request, timestamper, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<SigningOutcome> Complete(IPendingSignature pending, ReadOnlyMemory<byte> signature) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.CompleteAsync(pending, signature, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<SigningOutcome> CompleteWithContainer(IPendingSignature pending, ReadOnlyMemory<byte> container) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.CompleteWithContainerAsync(pending, container, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<SigningOutcome> AddValidationData(ISignatureRevocationSource source, bool includeVri) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.AddValidationDataAsync(_owner, source, includeVri, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<SigningOutcome> AddDocumentTimestamp(ISignatureTimestamper timestamper, SigningDigest digest) =>
        Signal.FromAsync(token => HyperPdfDocumentSigning.AddDocumentTimestampAsync(_owner, timestamper, digest, token).AsTask());
}
