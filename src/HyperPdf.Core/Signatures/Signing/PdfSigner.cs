// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// Signs an open document through its <see cref="IDocumentSigner"/> feature, which the document engine provides. This class
/// only finds the feature; the signature is made by the engine.
/// </summary>
public static class PdfSigner
{
    /// <summary>Gets a value indicating whether the document can be signed.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when the document engine provides signing.</returns>
    public static bool CanSign(IDocument? document) => DocumentFeature<IDocumentSigner>.Get(document) is not null;

    /// <summary>Works out what signing would do without changing the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="request">What the signature records.</param>
    /// <returns>The preview, or <see langword="null"/> when the document cannot be signed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SigningPreview? Preview(IDocument? document, in SigningRequest request) => DocumentFeature<IDocumentSigner>.Get(document)?.Preview(request);

    /// <summary>Signs a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/> for none.</param>
    /// <returns>
    /// A cold signal giving the signed file once; unsubscribing stops signing. It fails with <see cref="NotSupportedException"/> when the
    /// document cannot be signed and with <see cref="SigningRefusedException"/> when a lock, a certification, a seed value or the certificate forbids it.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<SigningOutcome> Sign(
        IDocument? document,
        X509Certificate2 certificate,
        SigningRequest request,
        ISignatureTimestamper? timestamper) =>
        Signal.Defer(() => (DocumentFeature<IDocumentSigner>.Get(document) ?? throw new NotSupportedException("This document cannot be signed."))
            .Sign(certificate, request, timestamper));

    /// <summary>Adds a document timestamp to a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="timestamper">The timestamp authority.</param>
    /// <returns>A cold signal giving the timestamped file once; it fails with <see cref="NotSupportedException"/> when the document cannot be signed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<SigningOutcome> AddDocumentTimestamp(IDocument? document, ISignatureTimestamper timestamper) =>
        Signal.Defer(() => (DocumentFeature<IDocumentSigner>.Get(document) ?? throw new NotSupportedException("This document cannot be signed."))
            .AddDocumentTimestamp(timestamper, SigningDigest.Sha256));
}
