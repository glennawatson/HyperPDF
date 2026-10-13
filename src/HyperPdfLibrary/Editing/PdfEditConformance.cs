// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Decides whether a document-level edit (an attachment, a layer, a script) can be made without breaking the PDF/A claim
/// the document makes. An edit that would break the claim is refused with an explanation, never saved under a false claim.
/// </summary>
internal static class PdfEditConformance
{
    /// <summary>The conformance letter of PDF/A-4 that allows embedded files.</summary>
    private const string FileLevel = "F";

    /// <summary>Checks that a file may be embedded, or attached with an annotation.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The claimed PDF/A level does not allow the file (ISO 19005-1 6.1.7, 19005-2 6.8, 19005-4 6.8).</exception>
    internal static void RequireEmbeddedFilesAllowed(PdfDocument document)
    {
        if (GetClaim(document) is not { } claim)
        {
            return;
        }

        var allowed = claim.Part == PdfAClaim.Part3 || (claim.Part == PdfAClaim.Part4 && string.Equals(claim.Conformance, FileLevel, StringComparison.Ordinal));
        if (!allowed)
        {
            throw new InvalidOperationException(
                $"The document claims PDF/A-{claim.Label}, which does not allow you to attach files that are not themselves PDF/A. "
                + "Only PDF/A-3 and PDF/A-4F documents can hold any file. The file was not attached.");
        }
    }

    /// <summary>Determines whether the claim (PDF/A-3 or PDF/A-4) needs an attached file's MIME type and relationship set.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when the file must be listed as an associated file.</returns>
    internal static bool NeedsAssociatedFile(PdfDocument document) => GetClaim(document) is { } claim && claim.Part is PdfAClaim.Part3 or PdfAClaim.Part4;

    /// <summary>Checks that an annotation may be added without a place in the structure tree.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/UA, which needs every annotation tagged (ISO 14289-1 7.18.1).</exception>
    internal static void RequireUntaggedAnnotationsAllowed(PdfDocument document)
    {
        if (PdfDocumentMetadata.GetXmp(document)?.PdfUaPart is { } part)
        {
            throw new InvalidOperationException(
                $"The document claims PDF/UA-{part}, which needs every annotation to be part of the document's structure. The annotation was not added, as it would break that claim.");
        }
    }

    /// <summary>Checks that the window title may show the file name instead of the document title.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/UA, which needs the title shown (ISO 14289-1 7.1).</exception>
    internal static void RequireDocumentTitleShown(PdfDocument document)
    {
        if (PdfDocumentMetadata.GetXmp(document)?.PdfUaPart is { } part)
        {
            throw new InvalidOperationException(
                $"The document claims PDF/UA-{part}, which needs the window to show the document title. The setting was not changed, as it would break that claim.");
        }
    }

    /// <summary>Checks that optional content may be added.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/A-1, which forbids optional content (ISO 19005-1 6.1.13).</exception>
    internal static void RequireLayersAllowed(PdfDocument document)
    {
        if (GetClaim(document) is { Part: PdfAClaim.Part1 } claim)
        {
            throw new InvalidOperationException($"The document claims PDF/A-{claim.Label}, which does not allow layers. The layer was not added.");
        }
    }

    /// <summary>Checks that scripts and document-level actions may be added.</summary>
    /// <param name="document">The document.</param>
    /// <param name="what">What was asked for, in plain words.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/A, which forbids JavaScript and the catalog's additional actions.</exception>
    internal static void RequireScriptsAllowed(PdfDocument document, string what)
    {
        if (GetClaim(document) is { } claim)
        {
            throw new InvalidOperationException($"The document claims PDF/A-{claim.Label}, which does not allow {what}. It was not added.");
        }
    }

    /// <summary>Determines whether the document claims a PDF/A level.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool ClaimsPdfA(PdfDocument document) => GetClaim(document) is not null;

    /// <summary>Checks that a portfolio (a collection of attached files) may be made.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The claimed level does not allow attached files.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireCollectionAllowed(PdfDocument document) => RequireEmbeddedFilesAllowed(document);

    /// <summary>Reads the PDF/A claim of the document, if it makes one the standards define.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The claim, or <see langword="null"/>.</returns>
    private static PdfAClaim? GetClaim(PdfDocument document)
    {
        var claim = PdfDocumentConformance.ReadPdfAClaim(PdfDocumentMetadata.GetXmp(document));
        return claim is { Part: > 0 } ? claim : null;
    }
}
