// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// Signs the open document with a certificate. The signature is appended as an incremental update, so the bytes already in
/// the file are untouched and earlier signatures stay valid. Unsaved edits are part of what is signed.
/// </summary>
public interface IDocumentSigner : IDocumentFeature
{
    /// <summary>Works out what signing would do, and every reason it would be refused, without changing the document.</summary>
    /// <param name="request">What the signature records.</param>
    /// <returns>The preview.</returns>
    SigningPreview Preview(in SigningRequest request);

    /// <summary>Signs the document and returns the signed file.</summary>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority that stamps the signature, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops signing, including waiting for the timestamp authority.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="SigningRefusedException">A lock, a certification, a seed value or the certificate forbids the signature.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<SigningOutcome> SignAsync(X509Certificate2 certificate, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken);

    /// <summary>Signs the document with a signer whose key is outside the app, such as a hardware token or a remote signing service.</summary>
    /// <param name="provider">Makes the signature value.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority that stamps the signature, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops signing, including waiting for the signer and the timestamp authority.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="SigningRefusedException">A lock, a certification, a seed value or the certificate forbids the signature.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<SigningOutcome> SignAsync(ISignatureProvider provider, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken);

    /// <summary>
    /// Prepares a signature for a signer that answers later: the file is built with room reserved for the signature, and the
    /// bytes to sign are returned. External signing finishes with <see cref="CompleteAsync"/> or <see cref="CompleteWithContainerAsync"/>.
    /// </summary>
    /// <param name="identity">The signer's certificates and kind of key.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority that stamps the signature when it is completed, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The pending signature.</returns>
    /// <exception cref="SigningRefusedException">A lock, a certification, a seed value or the certificate forbids the signature.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<IPendingSignature> PrepareAsync(SignerIdentity identity, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken);

    /// <summary>Finishes a pending signature with the signature value a signer made over its signed attributes.</summary>
    /// <param name="pending">The pending signature this signer prepared.</param>
    /// <param name="signature">The signature value: PKCS #1 or PSS bytes for RSA, and the ASN.1 DER <c>Ecdsa-Sig-Value</c> for ECDSA.</param>
    /// <param name="cancellationToken">Stops waiting for the timestamp authority.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="ArgumentException"><paramref name="pending"/> was not prepared by this signer.</exception>
    /// <exception cref="SigningRefusedException"><paramref name="pending"/> was already completed, or is being completed; a pending signature completes once.</exception>
    /// <exception cref="InvalidDataException">The signature does not fit the room reserved, or does not check out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<SigningOutcome> CompleteAsync(IPendingSignature pending, ReadOnlyMemory<byte> signature, CancellationToken cancellationToken);

    /// <summary>Finishes a pending signature with a whole signature container that a signing service built over its document digest.</summary>
    /// <param name="pending">The pending signature this signer prepared.</param>
    /// <param name="container">The DER-encoded CMS ContentInfo.</param>
    /// <param name="cancellationToken">Stops waiting for the timestamp authority.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="ArgumentException"><paramref name="pending"/> was not prepared by this signer.</exception>
    /// <exception cref="SigningRefusedException"><paramref name="pending"/> was already completed, or is being completed; a pending signature completes once.</exception>
    /// <exception cref="InvalidDataException">The container is not a signature container, does not fit the room reserved, or does not check out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<SigningOutcome> CompleteWithContainerAsync(IPendingSignature pending, ReadOnlyMemory<byte> container, CancellationToken cancellationToken);

    /// <summary>
    /// Adds long-term validation data (PAdES LTV): the certificates, OCSP responses and CRLs each signature needs, in the
    /// document security store (ISO 32000-2, 12.8.4.3) with an entry for each signature.
    /// </summary>
    /// <param name="source">Finds revocation data and missing issuer certificates.</param>
    /// <param name="includeVri">Whether to add a validation-related information entry for each signature.</param>
    /// <param name="cancellationToken">Stops the lookups.</param>
    /// <returns>The updated file.</returns>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<SigningOutcome> AddValidationDataAsync(ISignatureRevocationSource source, bool includeVri, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a document timestamp (PAdES-LTA): a timestamp token over the whole file as it stands, so the document and its
    /// signatures can be shown to have existed then.
    /// </summary>
    /// <param name="timestamper">The timestamp authority.</param>
    /// <param name="digest">The digest of the timestamp request.</param>
    /// <param name="cancellationToken">Stops waiting for the authority.</param>
    /// <returns>The timestamped file.</returns>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<SigningOutcome> AddDocumentTimestampAsync(ISignatureTimestamper timestamper, SigningDigest digest, CancellationToken cancellationToken);
}
