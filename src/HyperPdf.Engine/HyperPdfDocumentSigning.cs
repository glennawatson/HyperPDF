// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Engine;

/// <summary>Implements certificate signing over the document's owned state.</summary>
internal static class HyperPdfDocumentSigning
{
    /// <summary>Works out what signing would do. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="request">What the signature records.</param>
    /// <returns>The preview.</returns>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static SigningPreview Preview(HyperPdfDocument self, in SigningRequest request)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);

        // The snapshot carries every unsaved edit, so the preview matches what would be signed.
        using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
        return SigningMapping.ToPreview(PdfDocumentSigning.Preview(snapshot, SigningMapping.ToOptions(request, null)));
    }

    /// <summary>Signs a snapshot of the document and returns the signed file. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="certificate">The signing certificate.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="SigningRefusedException">Something forbids the signature.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    internal static async ValueTask<SigningOutcome> SignAsync(
        HyperPdfDocument self,
        X509Certificate2 certificate,
        SigningRequest request,
        ISignatureTimestamper? timestamper,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var signer = new PdfCertificateSigner(certificate);
            var result = await PdfDocumentSigning.SignAsync(snapshot, SigningMapping.ToOptions(request, timestamper), signer, cancellationToken).ConfigureAwait(false);
            return Outcome(result);
        }
        catch (PdfSigningRefusedException ex)
        {
            throw new SigningRefusedException(ex.Reasons);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Signs a snapshot of the document with an external signature provider. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="provider">Makes the signature value.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="SigningRefusedException">Something forbids the signature.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    internal static async ValueTask<SigningOutcome> SignAsync(
        HyperPdfDocument self,
        ISignatureProvider provider,
        SigningRequest request,
        ISignatureTimestamper? timestamper,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var result = await PdfDocumentSigning.SignAsync(snapshot, SigningMapping.ToOptions(request, timestamper), new ProviderAdapter(provider), cancellationToken).ConfigureAwait(false);
            return Outcome(result);
        }
        catch (PdfSigningRefusedException ex)
        {
            throw new SigningRefusedException(ex.Reasons);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Prepares a signature for a signer that answers later, from a snapshot of the document.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="identity">The signer's certificates and kind of key.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority used when the signature is completed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The pending signature.</returns>
    /// <exception cref="SigningRefusedException">Something forbids the signature.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    internal static async ValueTask<IPendingSignature> PrepareAsync(
        HyperPdfDocument self,
        SignerIdentity identity,
        SigningRequest request,
        ISignatureTimestamper? timestamper,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var pending = await PdfDocumentSigning.PrepareAsync(snapshot, SigningMapping.ToOptions(request, timestamper), SigningMapping.ToIdentity(identity), cancellationToken).ConfigureAwait(false);
            return new EnginePendingSignature(pending);
        }
        catch (PdfSigningRefusedException ex)
        {
            throw new SigningRefusedException(ex.Reasons);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Finishes a pending signature with a signature value.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="ArgumentException">The pending signature was not prepared by the engine.</exception>
    /// <exception cref="InvalidDataException">The signature does not fit or does not check out.</exception>
    internal static async ValueTask<SigningOutcome> CompleteAsync(IPendingSignature pending, ReadOnlyMemory<byte> signature, CancellationToken cancellationToken)
    {
        var owned = Claim(pending);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Outcome(await PdfDocumentSigning.CompleteAsync(owned.Pending, signature, cancellationToken).ConfigureAwait(false));
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Finishes a pending signature with a whole signature container.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <param name="container">The DER-encoded container.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The signed file.</returns>
    /// <exception cref="ArgumentException">The pending signature was not prepared by the engine.</exception>
    /// <exception cref="InvalidDataException">The container does not fit or does not check out.</exception>
    internal static async ValueTask<SigningOutcome> CompleteWithContainerAsync(IPendingSignature pending, ReadOnlyMemory<byte> container, CancellationToken cancellationToken)
    {
        var owned = Claim(pending);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Outcome(await PdfDocumentSigning.CompleteWithContainerAsync(owned.Pending, container, cancellationToken).ConfigureAwait(false));
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Adds long-term validation data for the signatures of a snapshot of the document.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="source">Finds revocation data and missing issuer certificates.</param>
    /// <param name="includeVri">Whether to add a validation-related information entry for each signature.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The updated file.</returns>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    internal static async ValueTask<SigningOutcome> AddValidationDataAsync(HyperPdfDocument self, ISignatureRevocationSource source, bool includeVri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var data = await PdfDocumentSigning.CollectValidationDataAsync(snapshot, new RevocationSourceAdapter(source), cancellationToken).ConfigureAwait(false);
            return Outcome(await PdfDocumentSigning.AddValidationDataAsync(snapshot, data, includeVri, cancellationToken).ConfigureAwait(false));
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Adds a document timestamp to a snapshot of the document. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="timestamper">The timestamp authority.</param>
    /// <param name="digest">The digest of the timestamp request.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The timestamped file.</returns>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    internal static async ValueTask<SigningOutcome> AddDocumentTimestampAsync(HyperPdfDocument self, ISignatureTimestamper timestamper, SigningDigest digest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timestamper);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var options = new PdfSigningOptions { Timestamp = new TimestamperAdapter(timestamper), TimestampDigest = SigningMapping.ToDigest(digest) };
            var result = await PdfDocumentSigning.AddDocumentTimestampAsync(snapshot, options, cancellationToken).ConfigureAwait(false);
            return Outcome(result);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Maps the library's result to the app's.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The file and its notes.</returns>
    private static SigningOutcome Outcome(PdfSigningResult result) => new(result.Bytes, result.Notes);

    /// <summary>Checks that a pending signature is one the engine prepared, and takes its one completion.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <returns>The engine's pending signature.</returns>
    /// <exception cref="ArgumentException">The pending signature was not prepared by the engine.</exception>
    /// <exception cref="SigningRefusedException">The pending signature was already completed, or a completion is running.</exception>
    private static EnginePendingSignature Claim(IPendingSignature pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var owned = pending as EnginePendingSignature ?? throw new ArgumentException("The pending signature was not prepared by this signer.", nameof(pending));
        return owned.TryClaim() ? owned : throw new SigningRefusedException("This signature was already completed or is being completed. Prepare it again to sign again.");
    }
}
