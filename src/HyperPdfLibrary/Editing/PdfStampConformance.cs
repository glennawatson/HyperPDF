// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Decides whether a stamp (an overlay or a watermark) can be added without breaking a conformance claim the document
/// makes, and how the stamp is marked in a tagged document. A stamp that would break the claim is refused with an
/// explanation, never saved under a false claim.
/// </summary>
internal static class PdfStampConformance
{
    /// <summary>The first part of ISO 19005, which allows neither optional content nor transparency.</summary>
    private const int PdfAPartOne = 1;

    /// <summary>Gets the part of ISO 19005 a document claims in its XMP metadata.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The part, or 0 when the document makes no PDF/A claim.</returns>
    internal static int GetPdfAPart(PdfDocument document) => PdfDocumentMetadata.GetXmp(document)?.PdfAPart ?? 0;

    /// <summary>Checks that a stamp may use an optional content group.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/A-1, which forbids optional content (ISO 19005-1 6.1.13).</exception>
    internal static void RequireLayersAllowed(PdfDocument document)
    {
        if (GetPdfAPart(document) == PdfAPartOne)
        {
            throw new InvalidOperationException("The document claims PDF/A-1, which does not allow optional content, so the stamp cannot be put in a layer.");
        }
    }

    /// <summary>Checks that a stamp may be partly transparent.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/A-1, which forbids transparency (ISO 19005-1 6.4).</exception>
    internal static void RequireTransparencyAllowed(PdfDocument document)
    {
        if (GetPdfAPart(document) == PdfAPartOne)
        {
            throw new InvalidOperationException("The document claims PDF/A-1, which does not allow transparency, so the stamp must be opaque.");
        }
    }

    /// <summary>Checks that a stamp may use a font that is not embedded.</summary>
    /// <param name="document">The document.</param>
    /// <exception cref="InvalidOperationException">The document claims PDF/A, which needs every font embedded.</exception>
    internal static void RequireUnembeddedFontAllowed(PdfDocument document)
    {
        if (GetPdfAPart(document) > 0)
        {
            throw new InvalidOperationException("The document claims PDF/A, which needs every font embedded. Use a font that is already embedded in the document.");
        }
    }

    /// <summary>Gets how a stamp is marked: as an artifact when the document is tagged, so it stays out of the structure.</summary>
    /// <param name="document">The document.</param>
    /// <param name="watermark">Whether the stamp is a watermark rather than an overlay.</param>
    /// <returns>The marking.</returns>
    internal static PdfStampMarking GetMarking(PdfDocument document, bool watermark)
    {
        if (!document.Objects.Catalog.ContainsKey(KnownName.StructTreeRoot))
        {
            return PdfStampMarking.None;
        }

        return watermark ? PdfStampMarking.Watermark : PdfStampMarking.Artifact;
    }
}
