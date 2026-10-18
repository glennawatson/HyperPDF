// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Document;

/// <summary>Validates document signatures.</summary>
public static class PdfDocumentSignatureValidation
{
    /// <summary>Gets current and historical signatures with the permissions and locks recorded when they were signed.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The signature details, including signed fields hidden by later catalog or field updates.</returns>
    public static IReadOnlyList<PdfSignatureDetails> GetSignatureDetails(PdfDocument document)
    {
        var revision = PdfDocumentCaches.Revision(document);
        return Volatile.Read(ref document.State.SignatureDetails) is { } cached
            ? cached
            : PdfDocumentCaches.Publish(document, ref document.State.SignatureDetails, PdfSignatureHistory.Read(document), revision);
    }

    /// <summary>Gets the document security store (/DSS) and its /VRI entries.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The store, or <see cref="PdfSecurityStore.Empty"/>.</returns>
    public static PdfSecurityStore GetSecurityStore(PdfDocument document)
    {
        var revision = PdfDocumentCaches.Revision(document);
        return Volatile.Read(ref document.State.SecurityStore) is { } cached
            ? cached
            : PdfDocumentCaches.Publish(document, ref document.State.SecurityStore, PdfSecurityStoreReader.Read(document.Catalog, document.Objects.Names), revision);
    }

    /// <summary>Gets the file's revisions: the original save and each incremental update, oldest first.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The revisions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfRevision> GetRevisions(PdfDocument document) => PdfDocumentSignatureValidation.GetRevisionArray(document);

    /// <summary>Gets the usage rights of the document's UR3 signature (/Perms /UR3).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The rights, or <see langword="null"/> when there is no UR3 signature.</returns>
    public static PdfUsageRights? GetUsageRights(PdfDocument document)
    {
        var signature = document.Catalog.GetDictionary(KnownName.Perms)?.GetDictionary(document.Objects.Names.Intern("UR3"u8));
        return PdfSignatureDictionaries.ReadReferences(signature?.GetArray(KnownName.Reference), document.Objects.Names).UsageRights;
    }

    /// <summary>Checks every signature without the network, trusting the system's roots.</summary>
    /// <param name="document">The document.</param>
    /// <returns>One report per entry of <see cref="GetSignatureDetails"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfSignatureValidationReport> ValidateSignatures(PdfDocument document) =>
        PdfDocumentSignatureValidation.ValidateSignatures(
        document,
        PdfSignatureValidationOptions.Default);

    /// <summary>Checks every signature: byte ranges, changes after signing and whether MDP allows them, digests, signatures, timestamps and chains.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to check.</param>
    /// <returns>One report per entry of <see cref="GetSignatureDetails"/>.</returns>
    public static IReadOnlyList<PdfSignatureValidationReport> ValidateSignatures(PdfDocument document, PdfSignatureValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return PdfSignatureValidator.Validate(document, options);
    }

    /// <summary>Gets the revisions, reading them on first use.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The revisions.</returns>
    internal static PdfRevision[] GetRevisionArray(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.Revisions) is { } cached)
        {
            return cached;
        }

        var revisions = StoreRevisions.ReadRevisions(document.Objects);
        Volatile.Write(ref document.State.Revisions, revisions);
        return revisions;
    }
}
