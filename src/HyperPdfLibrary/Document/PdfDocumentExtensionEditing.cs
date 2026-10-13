// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Declares developer extensions in the catalog's <c>/Extensions</c> dictionary (ISO 32000-2 §7.12). Existing declarations
/// are kept and merged, never duplicated.
/// </summary>
public static class PdfDocumentExtensionEditing
{
    /// <summary>The undo label of a declaration.</summary>
    private const string DeclareLabel = "Declare developer extension";

    /// <summary>The prefix of ISO's own extensions, which ISO/TS 32003 requires to be an array.</summary>
    private const string IsoPrefix = "ISO_";

    /// <summary>Gets the highest extension level the catalog declares for a prefix.</summary>
    /// <param name="document">The document.</param>
    /// <param name="prefix">The developer prefix, for example ADBE.</param>
    /// <returns>The level, or zero when the prefix is not declared.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int GetExtensionLevel(PdfDocument document, string prefix)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(prefix);
        var level = 0;
        foreach (var extension in PdfDocumentExtensionDeclarations.GetDeveloperExtensions(document))
        {
            if (string.Equals(extension.Prefix, prefix, StringComparison.Ordinal))
            {
                level = Math.Max(level, extension.ExtensionLevel);
            }
        }

        return level;
    }

    /// <summary>
    /// Declares a developer extension in a transaction (the open one, or its own), raising the document's version to the
    /// extension's base version when it is older (the extension builds on that version). A declaration with the same prefix
    /// and level is replaced; one with a different level is kept beside it when the catalog already uses an array for the
    /// prefix, when the document is PDF 2.0 or newer, or for the ISO_ prefix, as ISO 32000-2 allows an array of dictionaries
    /// there. For an older document the catalog keeps one dictionary per prefix, holding the highest level.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="extension">The extension. Its base version must be like "1.7".</param>
    /// <returns><see langword="true"/> when the catalog changed; <see langword="false"/> when it already declared the extension.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The prefix is empty or has characters a name cannot hold, the base version is not a version, or the level is negative.</exception>
    public static bool Declare(PdfDocument document, PdfDeveloperExtension extension)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(extension);
        if (!PdfVersion.TryParse(extension.BaseVersion, out var baseVersion))
        {
            throw new ArgumentException("The base version is not a version such as \"1.7\".", nameof(extension));
        }

        if (extension.Prefix.Length == 0 || extension.ExtensionLevel < 0 || !IsNameText(extension.Prefix))
        {
            throw new ArgumentException("The prefix is empty or not a name, or the level is negative.", nameof(extension));
        }

        var catalog = DeveloperExtensionMerge.Merge(document.Objects, extension, baseVersion, IsArrayForm(document, extension, baseVersion));
        if (catalog is null)
        {
            return false;
        }

        PdfDocumentEditing.RunEdit(
            document,
            DeclareLabel,
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            new DeclareState(document, catalog),
            static (transaction, state) => PdfPageTreeWriter.ReplaceCatalog(transaction, state.Document.Objects, state.Catalog));
        return true;
    }

    /// <summary>Decides whether the prefix is written as an array of dictionaries.</summary>
    /// <param name="document">The document.</param>
    /// <param name="extension">The extension.</param>
    /// <param name="baseVersion">The extension's base version.</param>
    /// <returns><see langword="true"/> when an array is used.</returns>
    private static bool IsArrayForm(PdfDocument document, PdfDeveloperExtension extension, PdfVersion baseVersion)
    {
        var store = document.Objects;
        var existing = store.Catalog.GetDictionary(KnownName.Extensions)?.Get(store.Names.Intern(extension.Prefix)) ?? default;
        var version = PdfVersion.Max(PdfDocumentVersioning.GetVersion(document), baseVersion);
        return existing.AsArray() is not null || string.Equals(extension.Prefix, IsoPrefix, StringComparison.Ordinal) || version >= PdfVersion.Pdf20;
    }

    /// <summary>Determines whether text can be written as a name: printable ASCII with no delimiter or white space.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private static bool IsNameText(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return Encoding.ASCII.GetByteCount(text) == text.Length;
    }

    /// <summary>The arguments of a declaration.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Catalog">The catalog to write.</param>
    private readonly record struct DeclareState(PdfDocument Document, PdfDictionary Catalog);
}
