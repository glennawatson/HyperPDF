// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on XMP metadata: the packets themselves, the PDF/A identification schema, the properties and their
/// value types, the extension schemas that define other properties, and, for PDF/A-1, the document information
/// crosswalk. PDF/A-1 checks the catalog packet only; later parts check every metadata stream.
/// </summary>
internal static class PdfAMetadataRules
{
    /// <summary>The first part of ISO 19005.</summary>
    private const int Part1 = 1;

    /// <summary>The fourth part of ISO 19005, which has no property or extension schema rules.</summary>
    private const int Part4 = 4;

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        .. PdfAInfoCrosswalkRules.Rules,
        .. PdfAExtensionSchemaRules.Rules,
        .. PdfAXmpPackageRules.Rules,
        .. PdfAXmpPropertyRules.Rules,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var document = context.Document;
        var main = PdfAMetadataStreams.Main(document);
        var mainPacket = main is null ? null : PdfAXmpPacketReader.Read(main.DecodeOrEmpty());
        if (context.Part == Part1)
        {
            PdfAInfoCrosswalkRules.Check(context, CatalogPacket(document, main, mainPacket));
        }

        if (mainPacket is not null)
        {
            var definitions = PdfAXmpExtensionDefinitions.Build(mainPacket.ExtensionSchemas, context.Part == Part1, []);
            Check(context, new(mainPacket, PdfAContexts.CatalogMetadata, true, [], definitions));
            PdfAXmpPackageRules.CheckIdentification(context, mainPacket);
        }

        if (context.Part != Part1)
        {
            CheckOthers(context, main, mainPacket);
        }
    }

    /// <summary>Checks the metadata streams of dictionaries other than the catalog.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="main">The catalog's metadata stream, or <see langword="null"/>.</param>
    /// <param name="mainPacket">The catalog's packet, or <see langword="null"/>.</param>
    private static void CheckOthers(PdfAValidationContext context, PdfStream? main, PdfAXmpPacket? mainPacket)
    {
        var catalogPacket = CatalogPacket(context.Document, main, mainPacket);
        var mainDefinitions = PdfAXmpExtensionDefinitions.Build(catalogPacket?.ExtensionSchemas, false, []);
        foreach (var other in PdfAMetadataStreams.Others(context.Document, main, context.CancellationToken))
        {
            // veraPDF reads the catalog packet again for each other packet and gives up on the other packet when that fails.
            var packet = catalogPacket is { IsWellFormed: false } ? PdfAXmpPacket.Malformed : PdfAXmpPacketReader.Read(other.Stream.DecodeOrEmpty());
            var definitions = packet.ExtensionSchemas is null ? [] : PdfAXmpExtensionDefinitions.Build(packet.ExtensionSchemas, false, mainDefinitions);
            Check(context, new(packet, other.Context, false, mainDefinitions, definitions));
        }
    }

    /// <summary>Checks one packet's own rules: the packet, its properties and its extension schemas.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="package">The packet.</param>
    private static void Check(PdfAValidationContext context, PdfAXmpPackage package)
    {
        PdfAXmpPackageRules.Check(context, package);
        if (context.Part == Part4)
        {
            return;
        }

        PdfAXmpPropertyRules.Check(context, package);
        PdfAExtensionSchemaRules.Check(context, package);
    }

    /// <summary>
    /// Gets the packet of the catalog's <c>/Metadata</c> stream of any type, which the document information crosswalk
    /// compares with and the other packets' extension schemas build on.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="main">The catalog's proper metadata stream, or <see langword="null"/>.</param>
    /// <param name="mainPacket">Its packet, or <see langword="null"/>.</param>
    /// <returns>The packet, or <see langword="null"/> when the catalog has no metadata stream.</returns>
    private static PdfAXmpPacket? CatalogPacket(PdfDocument document, PdfStream? main, PdfAXmpPacket? mainPacket)
    {
        var stream = PdfAMetadataStreams.Catalog(document);
        if (ReferenceEquals(stream, main))
        {
            return mainPacket;
        }

        return stream is null ? null : PdfAXmpPacketReader.Read(stream.DecodeOrEmpty());
    }
}
