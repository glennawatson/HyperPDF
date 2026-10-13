// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Signs documents (ISO 32000-2, 12.8): adds a signature field or signs an empty one, certifies, timestamps and writes long-term
/// validation data. Every operation appends an incremental update to the document's original bytes, so earlier signatures stay
/// valid, and returns the new file; the <see cref="PdfDocument"/> is left as it was.
/// </summary>
public static class PdfDocumentSigning
{
    /// <summary>Works out what signing would do, and every reason it would be refused, without changing the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <returns>The preview. Without a signer, the certificate-dependent checks (digest for the curve, certificate constraints) are left out.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfSigningPreview Preview(PdfDocument document, PdfSigningOptions options) => Preview(document, options, null);

    /// <summary>Works out what signing with a signer would do, and every reason it would be refused, without changing the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="identity">The signer, or <see langword="null"/> to leave the certificate-dependent checks out.</param>
    /// <returns>The preview.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static PdfSigningPreview Preview(PdfDocument document, PdfSigningOptions options, PdfSignerIdentity? identity)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        var planned = PdfSigningPlanner.Plan(document, options, identity);
        var plan = planned.Plan;
        return new(
            plan.FieldName,
            plan.Field is null,
            plan.Appearance.PageIndex + 1,
            plan.Appearance.IsVisible,
            plan.Format,
            plan.Digest,
            plan.Reason ?? string.Empty,
            plan.Location ?? string.Empty,
            plan.Certification,
            plan.FieldLock is { Action: PdfFieldLockAction.All },
            planned.TimestampUrl,
            planned.TimestampRequired,
            planned.Problems,
            planned.Notes);
    }

    /// <summary>Signs a document with an in-process certificate and returns the signed file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options. A timestamp client is not allowed here; use <see cref="SignAsync"/>.</param>
    /// <param name="signer">The signer.</param>
    /// <returns>The signed file and what was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The options ask for a timestamp, which needs a network request.</exception>
    /// <exception cref="PdfSigningRefusedException">A lock, a certification, a seed value or the certificate forbids the signature.</exception>
    /// <exception cref="PdfException">The file cannot be written or the signature does not check out.</exception>
    public static PdfSigningResult Sign(PdfDocument document, PdfSigningOptions options, PdfCertificateSigner signer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(signer);
        if (options.Timestamp is not null)
        {
            throw new InvalidOperationException("A signature timestamp needs a network request. Use SignAsync.");
        }

        var identity = PdfSignerIdentity.From(signer);
        var planned = PdfSigningEngine.PlanOrThrow(document, options, identity);
        var capacity = PdfSigningEngine.Capacity(identity, options, PdfSigningMac.IsNeeded(document.Objects));
        var needed = 0;
        using var workspace = PdfSigningWorkspace.Open(document);
        for (var attempt = 1; attempt <= PdfSigningEngine.MaxAttempts; attempt++)
        {
            var prepared = BuildPrepared(workspace, planned, identity, options, capacity, CancellationToken.None);
            var attributes = PdfSigningEngine.Attributes(prepared, identity);
            var signature = signer.Sign(attributes.SignedAttributes, planned.Plan.Digest);
            var container = Assemble(prepared, attributes, signature, []);
            if (PdfSigningEngine.TryFinish(prepared, container, attempt, out needed) is { } result)
            {
                return result;
            }

            prepared.Mac?.Wipe();
            capacity = PdfSigningEngine.Grow(needed, options.Headroom);
        }

        throw PdfSigningEngine.NoRoom(needed, capacity);
    }

    /// <summary>Signs a document and returns the signed file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="provider">Makes the signature value: an in-process certificate, a token or a remote signing service.</param>
    /// <param name="cancellationToken">Cancels the signing, including the wait for the signer and the timestamp authority.</param>
    /// <returns>The signed file and what was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfSigningRefusedException">A lock, a certification, a seed value or the certificate forbids the signature.</exception>
    /// <exception cref="PdfException">The file cannot be written or the signature does not check out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfSigningResult> SignAsync(PdfDocument document, PdfSigningOptions options, IPdfSignatureProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);
        var identity = PdfSignerIdentity.From(provider);
        var planned = PdfSigningEngine.PlanOrThrow(document, options, identity);
        await PdfSigningEngine.PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        var capacity = PdfSigningEngine.Capacity(identity, options, PdfSigningMac.IsNeeded(document.Objects));
        var needed = 0;
        using var workspace = PdfSigningWorkspace.Open(document);
        for (var attempt = 1; attempt <= PdfSigningEngine.MaxAttempts; attempt++)
        {
            var prepared = BuildPrepared(workspace, planned, identity, options, capacity, cancellationToken);
            var attributes = PdfSigningEngine.Attributes(prepared, identity);
            var signature = await provider.SignAsync(attributes.SignedAttributes, planned.Plan.Digest, cancellationToken).ConfigureAwait(false);
            var token = await PdfSigningEngine.StampAsync(options.Timestamp, options.TimestampDigest, signature, cancellationToken).ConfigureAwait(false);
            var container = Assemble(prepared, attributes, signature, token);
            if (PdfSigningEngine.TryFinish(prepared, container, attempt, out needed) is { } result)
            {
                return result;
            }

            prepared.Mac?.Wipe();
            capacity = PdfSigningEngine.Grow(needed, options.Headroom);
        }

        throw PdfSigningEngine.NoRoom(needed, capacity);
    }

    /// <summary>
    /// Prepares a signature for a signer that works apart from the library, such as a hardware token or a remote signing service.
    /// The returned object holds the finished file with room reserved for the signature, and says what to sign: the
    /// <see cref="PdfPendingSignature.SignedAttributes"/>, hashed with <see cref="PdfPendingSignature.Digest"/>.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options. Raise <see cref="PdfSigningOptions.Headroom"/> when the signer adds more than the certificates.</param>
    /// <param name="identity">The signer's certificates and kind of key.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The pending signature.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfSigningRefusedException">A lock, a certification, a seed value or the certificate forbids the signature.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfPendingSignature> PrepareAsync(PdfDocument document, PdfSigningOptions options, PdfSignerIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(identity);
        var planned = PdfSigningEngine.PlanOrThrow(document, options, identity);
        await PdfSigningEngine.PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        var capacity = PdfSigningEngine.Capacity(identity, options, PdfSigningMac.IsNeeded(document.Objects));
        using var workspace = PdfSigningWorkspace.Open(document);
        var prepared = BuildPrepared(workspace, planned, identity, options, capacity, cancellationToken);
        var attributes = PdfSigningEngine.Attributes(prepared, identity);
        return new(prepared, attributes.SignedAttributes, attributes.DocumentDigest, attributes.Spec, 1);
    }

    /// <summary>Finishes a pending signature with the signature value a signer made over its signed attributes.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <param name="signature">The signature value: PKCS #1 or PSS bytes for RSA, and the ASN.1 DER <c>Ecdsa-Sig-Value</c> for ECDSA.</param>
    /// <param name="cancellationToken">Cancels the wait for the timestamp authority.</param>
    /// <returns>The signed file and what was done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pending"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The pending signature was already completed, or another completion is writing it.</exception>
    /// <exception cref="PdfException">The signature does not fit the room reserved, or does not check out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfSigningResult> CompleteAsync(PdfPendingSignature pending, ReadOnlyMemory<byte> signature, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var work = pending.Work;
        var value = signature.ToArray();
        var token = await PdfSigningEngine.StampAsync(work.Options.Timestamp, work.Options.TimestampDigest, value, cancellationToken).ConfigureAwait(false);
        var attributes = new PdfSignedAttributes(pending.Spec, pending.DocumentDigest, pending.SignedAttributes);
        return FinishPending(pending, Assemble(work, attributes, value, token));
    }

    /// <summary>
    /// Finishes a pending signature with a whole signature container that a signing service built, for example a CAdES
    /// container from a remote signing API. The container must sign the file's signed bytes (the <see cref="PdfPendingSignature.DocumentDigest"/>).
    /// </summary>
    /// <param name="pending">The pending signature.</param>
    /// <param name="container">The DER-encoded CMS ContentInfo.</param>
    /// <param name="cancellationToken">Cancels the wait for the timestamp authority.</param>
    /// <returns>The signed file and what was done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pending"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The pending signature was already completed, or another completion is writing it.</exception>
    /// <exception cref="PdfException">The container is not a signature container, does not fit the room reserved, or does not check out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfSigningResult> CompleteWithContainerAsync(PdfPendingSignature pending, ReadOnlyMemory<byte> container, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var work = pending.Work;
        var value = container.ToArray();
        if (work.Options.Timestamp is not null)
        {
            var stamped = PdfExternalContainers.SignatureValue(value);
            var token = await PdfSigningEngine.StampAsync(work.Options.Timestamp, work.Options.TimestampDigest, stamped, cancellationToken).ConfigureAwait(false);
            value = PdfExternalContainers.AddTimestamp(value, token);
        }

        if (work.Mac is { } mac)
        {
            value = PdfSigningMac.Attach(mac, work.File, value);
        }

        return FinishPending(pending, value);
    }

    /// <summary>
    /// Adds a document timestamp (PAdES-LTA): a timestamp token over the whole file as it stands, so the document, its
    /// signatures and any validation data can be shown to have existed then.
    /// </summary>
    /// <param name="document">The document, usually already signed.</param>
    /// <param name="options">
    /// The options; <see cref="PdfSigningOptions.Timestamp"/> is required, and <see cref="PdfSigningOptions.TimestampDigest"/> and
    /// <see cref="PdfSigningOptions.Clock"/> are used.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait for the timestamp authority.</param>
    /// <returns>The timestamped file.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> has no timestamp client.</exception>
    /// <exception cref="PdfException">The file cannot be written or the timestamp does not check out.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfSigningResult> AddDocumentTimestampAsync(PdfDocument document, PdfSigningOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        return PdfDocumentTimestamps.AddAsync(document, options, cancellationToken);
    }

    /// <summary>
    /// Adds long-term validation data (PAdES LTV): certificates, revocation responses and CRLs in the document security store,
    /// and an entry for each signature that uses them. Data already in the store stays; the new data is added to it.
    /// </summary>
    /// <param name="document">The signed document.</param>
    /// <param name="data">The validation data to add, with the signature each part belongs to.</param>
    /// <param name="includeVri">Whether to add a validation-related information entry for each signature.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The updated file.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfSigningResult> AddValidationDataAsync(PdfDocument document, IReadOnlyList<PdfSignatureValidationData> data, bool includeVri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        await PdfSigningEngine.PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return PdfSecurityStoreWriter.Add(document, data, includeVri, cancellationToken);
    }

    /// <summary>
    /// Collects the validation data each signature in the document needs: the certificates of its chain (and of its timestamp), and
    /// the revocation responses or CRLs a source can find for them.
    /// </summary>
    /// <param name="document">The signed document.</param>
    /// <param name="source">Finds revocation data and missing issuer certificates.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The data for each signed signature.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<IReadOnlyList<PdfSignatureValidationData>> CollectValidationDataAsync(PdfDocument document, IPdfRevocationSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        await PdfSigningEngine.PrefetchAsync(document, cancellationToken).ConfigureAwait(false);
        return await PdfValidationDataCollector.CollectAsync(document, source, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the file for a signature.</summary>
    /// <param name="workspace">The private copy of the document; the attempts of one signature share it.</param>
    /// <param name="planned">The plan.</param>
    /// <param name="identity">The signer.</param>
    /// <param name="options">The options.</param>
    /// <param name="capacity">The room to reserve.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The prepared file.</returns>
    private static PdfPreparedSignature BuildPrepared(
        PdfSigningWorkspace workspace,
        PdfSigningPlanResult planned,
        PdfSignerIdentity identity,
        PdfSigningOptions options,
        int capacity,
        CancellationToken cancellationToken)
    {
        var signer = PdfSignerCertificate.Read(identity.Certificates[0]);
        return PdfSigningEngine.Build(workspace, planned.Plan, signer.IsEllipticCurve, options, planned.Notes, capacity, cancellationToken);
    }

    /// <summary>Assembles the container, attaching the PDF MAC when the document needs one.</summary>
    /// <param name="prepared">The prepared file.</param>
    /// <param name="attributes">The signed attributes.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="token">The timestamp token, or empty.</param>
    /// <returns>The container.</returns>
    private static byte[] Assemble(PdfPreparedSignature prepared, in PdfSignedAttributes attributes, byte[] signature, byte[] token)
    {
        var container = PdfCmsBuilder.Assemble(attributes.Spec, attributes.SignedAttributes, signature, token);
        return prepared.Mac is { } mac ? PdfSigningMac.Attach(mac, prepared.File, container) : container;
    }

    /// <summary>Writes a container into a pending signature's file.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <param name="container">The container.</param>
    /// <returns>The result.</returns>
    /// <exception cref="PdfException">The container does not fit the room reserved.</exception>
    private static PdfSigningResult FinishPending(PdfPendingSignature pending, byte[] container)
    {
        // Only one completion writes into the file, and a result is handed out once, because it is the file's own array.
        pending.Claim();
        var result = PdfSigningEngine.TryFinish(pending.Work, container, pending.Attempts, out var needed);
        if (result is null)
        {
            // Nothing was written, so a smaller value can still finish the signature.
            pending.Release();
        }

        var more = needed - pending.ContentsCapacity + PdfSigningOptions.DefaultHeadroom;
        return result ?? throw new PdfException(
            PdfError.Format,
            $"The signature needs {needed} bytes but {pending.ContentsCapacity} were reserved for it. Prepare the signature again with a Headroom at least {more} bytes larger.");
    }
}
