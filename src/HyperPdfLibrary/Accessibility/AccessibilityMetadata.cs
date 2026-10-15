// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// What the accessibility report reads from the document's XMP packet. Parsing the packet is the costly part, so the facts
/// are kept with the document until an edit.
/// </summary>
/// <param name="HasMetadata">Whether the catalog has a readable XMP packet.</param>
/// <param name="HasTitle">Whether the packet has a title.</param>
/// <param name="Part">The claimed PDF/UA part (<c>pdfuaid:part</c>), or <see langword="null"/>.</param>
/// <param name="Revision">The claimed <c>pdfuaid:rev</c>, or <see langword="null"/> when missing or empty.</param>
[DebuggerDisplay("AccessibilityMetadata: part {Part}, revision {Revision}")]
internal sealed record AccessibilityMetadata(bool HasMetadata, bool HasTitle, int? Part, string? Revision)
{
    /// <summary>Gets the facts for a document, reading the packet the first time after each edit.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The facts.</returns>
    internal static AccessibilityMetadata Get(PdfDocument document)
    {
        var revision = PdfDocumentCaches.Revision(document);
        return Volatile.Read(ref document.State.AccessibilityMetadata)
            ?? PdfDocumentCaches.PublishFirst(document, ref document.State.AccessibilityMetadata, Read(document), revision);
    }

    /// <summary>Reads the packet.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The facts.</returns>
    private static AccessibilityMetadata Read(PdfDocument document)
    {
        if (PdfDocumentMetadata.GetXmp(document) is not { } xmp)
        {
            return new(false, false, null, null);
        }

        var revision = xmp.GetValue(XmpMetadata.PdfUaIdNamespace, "rev") is { Length: > 0 } text ? text : null;
        return new(true, !string.IsNullOrWhiteSpace(xmp.Title), xmp.PdfUaPart, revision);
    }
}
