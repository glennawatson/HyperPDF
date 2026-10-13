// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Adds, changes and removes the protection of a document (ISO 32000-2 7.6): passwords and permissions, or encryption for
/// certificates. The protection covers every string and stream of the file, so each operation writes a whole new copy and
/// the open document never changes; an incremental save (<see cref="PdfDocumentSaving"/>) keeps the encryption a file
/// already has and never adds or changes it.
/// </summary>
/// <remarks>
/// A document that is already protected can only have its protection changed or removed by its owner: the person who opened
/// it as the owner, who gives the owner password, or who is a recipient with the right to change the encryption. A document
/// that already allows everything can be changed by anyone who has opened it. A signed document and a document that claims
/// PDF/A are refused with the reason, and nothing is written.
/// </remarks>
public static class PdfDocumentProtection
{
    /// <summary>Describes how a document is protected and whether the protection can be changed.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfProtectionInfo GetProtection(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfProtectionAuthority.Describe(document);
    }

    /// <summary>Writes a copy of the document with new protection. A document already protected needs to have been opened by its owner.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to encrypt the copy.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfProtectionResult Protect(PdfDocument document, PdfEncryptionOptions options, Stream destination) =>
        Protect(document, options, null, destination);

    /// <summary>Writes a copy of the document with new protection, using an owner password to change protection it already has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to encrypt the copy.</param>
    /// <param name="ownerPassword">The owner password of the document's current protection, or <see langword="null"/>.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="ownerPassword"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    public static PdfProtectionResult Protect(PdfDocument document, PdfEncryptionOptions options, string? ownerPassword, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(destination);
        if (Refuse(document, ownerPassword, true) is { } refusal)
        {
            return refusal;
        }

        PdfEncryptionRules.CheckOptions(options);
        using var copy = PdfDocumentOptimizing.OpenWorkingCopy(document);
        PdfCompactWriter.Save(copy.Objects, PdfCompactOptions.Default with { Encryption = options, OwnerVerified = true }, destination);
        return new(null, NotesFor(options));
    }

    /// <summary>Writes a copy of the document with new protection, with async I/O. A document already protected needs to have been opened by its owner.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to encrypt the copy.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <param name="cancellationToken">Cancels the work; nothing is written then.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfProtectionResult> ProtectAsync(PdfDocument document, PdfEncryptionOptions options, Stream destination, CancellationToken cancellationToken) =>
        ProtectAsync(document, options, null, destination, cancellationToken);

    /// <summary>Writes a copy of the document with new protection, with async I/O, using an owner password to change protection it already has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to encrypt the copy.</param>
    /// <param name="ownerPassword">The owner password of the document's current protection, or <see langword="null"/>.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <param name="cancellationToken">Cancels the work; nothing is written then.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="ownerPassword"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfProtectionResult> ProtectAsync(PdfDocument document, PdfEncryptionOptions options, string? ownerPassword, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refuse(document, ownerPassword, true) is { } refusal)
        {
            return refusal;
        }

        PdfEncryptionRules.CheckOptions(options);
        using var copy = await OpenCopyAsync(document, cancellationToken).ConfigureAwait(false);
        await PdfCompactWriter.SaveAsync(copy.Objects, PdfCompactOptions.Default with { Encryption = options, OwnerVerified = true }, destination, cancellationToken).ConfigureAwait(false);
        return new(null, NotesFor(options));
    }

    /// <summary>Writes a copy of the document with no protection. A document that is protected needs to have been opened by its owner.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfProtectionResult RemoveProtection(PdfDocument document, Stream destination) =>
        RemoveProtection(document, null, destination);

    /// <summary>Writes a copy of the document with no protection, using an owner password.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ownerPassword">The owner password of the document's protection, or <see langword="null"/>.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="ownerPassword"/> is <see langword="null"/>.</exception>
    public static PdfProtectionResult RemoveProtection(PdfDocument document, string? ownerPassword, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        if (Refuse(document, ownerPassword, false) is { } refusal)
        {
            return refusal;
        }

        using var copy = PdfDocumentOptimizing.OpenWorkingCopy(document);
        PdfCompactWriter.Save(copy.Objects, PdfCompactOptions.Default with { RemoveEncryption = true }, destination);
        return new(null, []);
    }

    /// <summary>Writes a copy of the document with no protection, with async I/O. A document that is protected needs to have been opened by its owner.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <param name="cancellationToken">Cancels the work; nothing is written then.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfProtectionResult> RemoveProtectionAsync(PdfDocument document, Stream destination, CancellationToken cancellationToken) =>
        RemoveProtectionAsync(document, null, destination, cancellationToken);

    /// <summary>Writes a copy of the document with no protection, with async I/O, using an owner password.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ownerPassword">The owner password of the document's protection, or <see langword="null"/>.</param>
    /// <param name="destination">The stream that receives the copy; nothing is written when the copy is refused.</param>
    /// <param name="cancellationToken">Cancels the work; nothing is written then.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="ownerPassword"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfProtectionResult> RemoveProtectionAsync(PdfDocument document, string? ownerPassword, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refuse(document, ownerPassword, false) is { } refusal)
        {
            return refusal;
        }

        using var copy = await OpenCopyAsync(document, cancellationToken).ConfigureAwait(false);
        await PdfCompactWriter.SaveAsync(copy.Objects, PdfCompactOptions.Default with { RemoveEncryption = true }, destination, cancellationToken).ConfigureAwait(false);
        return new(null, []);
    }

    /// <summary>Checks the reasons that stop any change of protection before anything is written.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ownerPassword">The owner password, or <see langword="null"/>.</param>
    /// <param name="adding">Whether protection is being added or changed; removing needs the document to be protected.</param>
    /// <returns>The refusal, or <see langword="null"/> when the change may go ahead.</returns>
    private static PdfProtectionResult? Refuse(PdfDocument document, string? ownerPassword, bool adding)
    {
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        if (!adding && !document.IsEncrypted)
        {
            return Refused(PdfProtectionMessages.NotProtected);
        }

        if (SignaturePresence.HasSignature(document.Objects))
        {
            return Refused(PdfProtectionMessages.Signed);
        }

        if (adding && PdfAClaimGuard.IsClaimed(document.Objects))
        {
            return Refused(PdfProtectionMessages.PdfA);
        }

        return PdfProtectionAuthority.IsAuthorised(document, ownerPassword) ? null : Refused(PdfProtectionMessages.NeedsOwner);
    }

    /// <summary>Makes a refusal.</summary>
    /// <param name="reason">The reason in plain words.</param>
    /// <returns>The result.</returns>
    private static PdfProtectionResult Refused(string reason) => new(reason, []);

    /// <summary>Opens a private copy of the document after loading the file with async I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The copy, which carries the unsaved edits and shares the file; the caller disposes it.</returns>
    private static async ValueTask<PdfDocument> OpenCopyAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        var source = document.Objects.Source;
        if (source.NeedsPrefetch)
        {
            await source.PrefetchAsync(0, source.Length, cancellationToken).ConfigureAwait(false);
        }

        return PdfDocumentOptimizing.OpenWorkingCopy(document);
    }

    /// <summary>Lists what a person should know about the file the options make.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The notes in plain words.</returns>
    private static string[] NotesFor(PdfEncryptionOptions options)
    {
        if ((options.Permissions & PdfAccessPermissions.ExtractForAccessibility) != 0 || options.Recipients is not null)
        {
            return [];
        }

        return options.Algorithm == PdfEncryptionAlgorithm.Aes128
            ? ["Programs that follow the older PDF rules will not let screen readers read this file. People who rely on a screen reader will be locked out of it."]
            : ["Screen readers can still read this file. The PDF 2.0 standard no longer lets a file block them, so only the AES-128 format can."];
    }
}
