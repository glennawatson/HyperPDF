// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;

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
    public ValueTask<SigningOutcome> SignAsync(X509Certificate2 certificate, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.SignAsync(_owner, certificate, request, timestamper, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<SigningOutcome> SignAsync(ISignatureProvider provider, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.SignAsync(_owner, provider, request, timestamper, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IPendingSignature> PrepareAsync(SignerIdentity identity, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.PrepareAsync(_owner, identity, request, timestamper, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<SigningOutcome> CompleteAsync(IPendingSignature pending, ReadOnlyMemory<byte> signature, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.CompleteAsync(pending, signature, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<SigningOutcome> CompleteWithContainerAsync(IPendingSignature pending, ReadOnlyMemory<byte> container, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.CompleteWithContainerAsync(pending, container, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<SigningOutcome> AddValidationDataAsync(ISignatureRevocationSource source, bool includeVri, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.AddValidationDataAsync(_owner, source, includeVri, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<SigningOutcome> AddDocumentTimestampAsync(ISignatureTimestamper timestamper, SigningDigest digest, CancellationToken cancellationToken) =>
        HyperPdfDocumentSigning.AddDocumentTimestampAsync(_owner, timestamper, digest, cancellationToken);
}
