// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A metadata packet being checked, with the extension schema definitions its properties may use.</summary>
/// <param name="Packet">The packet.</param>
/// <param name="Context">Where the packet was found, for the report.</param>
/// <param name="IsMain">Whether the packet is the catalog's.</param>
/// <param name="MainDefinitions">The definitions of the catalog packet's extension schemas; empty for the catalog packet itself.</param>
/// <param name="Definitions">The definitions of this packet's own extension schemas, read for the part checked.</param>
internal sealed record PdfAXmpPackage(
    PdfAXmpPacket Packet,
    string Context,
    bool IsMain,
    Dictionary<string, PdfAXmpSchemaDefinition> MainDefinitions,
    Dictionary<string, PdfAXmpSchemaDefinition> Definitions);
