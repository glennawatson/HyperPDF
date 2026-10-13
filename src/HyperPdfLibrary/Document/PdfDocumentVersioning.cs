// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads and raises the PDF version a document declares (ISO 32000-2 §7.5.2 for the header, §7.2.2 for the catalog's
/// <c>/Version</c>). The version is never lowered. A whole-file save writes the newest of the header, the catalog entry and
/// <see cref="PdfCompactOptions.MinimumVersion"/> in the header; an incremental save cannot change the header, so the
/// catalog entry carries a raised version.
/// </summary>
public static class PdfDocumentVersioning
{
    /// <summary>The undo label of a version change.</summary>
    private const string RaiseLabel = "Raise PDF version";

    /// <summary>Gets the version in the file header.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The version, or the unset version when the header is missing or is not a version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfVersion GetHeaderVersion(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfVersionRules.Header(document.Objects);
    }

    /// <summary>Gets the version of the document: the newer of the header and the catalog's <c>/Version</c> (ISO 32000-2 §7.2.2).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The version, or the unset version when the document names none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfVersion GetVersion(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfVersion.Max(PdfVersionRules.Header(document.Objects), PdfVersionRules.Catalog(document.Objects));
    }

    /// <summary>
    /// Raises the document's version to at least a version by setting the catalog's <c>/Version</c>, in a transaction (the
    /// open one, or its own). A document already at that version or newer is left as it is.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="version">The oldest version the document may declare.</param>
    /// <returns><see langword="true"/> when the version was raised; <see langword="false"/> when the document was already there.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="version"/> is the unset version.</exception>
    public static bool RaiseVersion(PdfDocument document, PdfVersion version)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (version.IsUnset)
        {
            throw new ArgumentException("The version is not set.", nameof(version));
        }

        if (version <= GetVersion(document))
        {
            return false;
        }

        PdfDocumentEditing.RunEdit(
            document,
            RaiseLabel,
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            new RaiseState(document, version),
            static (transaction, state) =>
            {
                var store = state.Document.Objects;
                var catalog = store.Catalog.Clone();
                catalog.Set(KnownName.Version, PdfValue.FromName(store.Names.Intern(state.Version.ToString())));
                PdfPageTreeWriter.ReplaceCatalog(transaction, store, catalog);
            });
        return true;
    }

    /// <summary>The arguments of a version change.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Version">The version to raise to.</param>
    private readonly record struct RaiseState(PdfDocument Document, PdfVersion Version);
}
