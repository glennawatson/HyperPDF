// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the PDF/A part a document claims, for writers that must keep the claim valid.</summary>
internal static class PdfAClaimReading
{
    /// <summary>Reads <c>pdfaid:part</c> from the catalog's XMP.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The part, or 0 when the document makes no PDF/A claim.</returns>
    internal static int ReadPart(PdfObjectStore store)
    {
        var packet = store.Catalog.GetStream(KnownName.Metadata).DecodeOrEmpty();
        return packet.Length == 0 ? 0 : XmpParser.Parse(packet).PdfAPart ?? 0;
    }

    /// <summary>Reads the whole claim, part and conformance letter, from the catalog's XMP.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The claim, or <see langword="null"/> when the document makes none.</returns>
    internal static PdfAClaim? ReadClaim(PdfObjectStore store)
    {
        var packet = store.Catalog.GetStream(KnownName.Metadata).DecodeOrEmpty();
        return packet.Length == 0 ? null : PdfDocumentConformance.ReadPdfAClaim(XmpParser.Parse(packet));
    }
}
