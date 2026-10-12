// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on XMP packets (ISO 19005-1, 6.7.5 and 6.7.9; ISO 19005-2, 6.6.2.1; ISO 19005-4, 6.7.2.1) and on the
/// PDF/A identification schema of the catalog packet (ISO 19005-1, 6.7.11; ISO 19005-2, 6.6.4; ISO 19005-4, 6.7.3).
/// </summary>
internal static class PdfAXmpPackageRules
{
    /// <summary>The prefix the identification properties must use.</summary>
    private const string IdentificationPrefix = "pdfaid";

    /// <summary>The identification property holding the part.</summary>
    private const string PartProperty = "part";

    /// <summary>The identification property holding the conformance level.</summary>
    private const string ConformanceProperty = "conformance";

    /// <summary>The identification property holding the revision year.</summary>
    private const string RevisionProperty = "rev";

    /// <summary>The revision year PDF/A-4 files give.</summary>
    private const string Revision2020 = "2020";

    /// <summary>The first part of ISO 19005.</summary>
    private const int Part1 = 1;

    /// <summary>The second part of ISO 19005.</summary>
    private const int Part2 = 2;

    /// <summary>The third part of ISO 19005.</summary>
    private const int Part3 = 3;

    /// <summary>The fourth part of ISO 19005.</summary>
    private const int Part4 = 4;

    /// <summary>Gets the rules these checks record.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.XmpEncodingUtf8,
        PdfARule.XmpPacketBytes,
        PdfARule.XmpPacketEncoding,
        PdfARule.XmpSerialization,
        PdfARule.CatalogXmpIdentification,
        PdfARule.IdentificationAmendmentPrefix,
        PdfARule.IdentificationConformanceA,
        PdfARule.IdentificationConformancePdfA1,
        PdfARule.IdentificationConformancePdfA2,
        PdfARule.IdentificationConformanceE,
        PdfARule.IdentificationConformanceF,
        PdfARule.IdentificationConformanceU,
        PdfARule.IdentificationConformanceNone,
        PdfARule.IdentificationConformancePrefix,
        PdfARule.IdentificationCorrigendumPrefix,
        PdfARule.IdentificationPart1,
        PdfARule.IdentificationPart2,
        PdfARule.IdentificationPart3,
        PdfARule.IdentificationPart4,
        PdfARule.IdentificationPartPrefix,
        PdfARule.IdentificationRevision,
        PdfARule.IdentificationRevisionPrefix,
    ];

    /// <summary>Checks a packet: well-formed, UTF-8, and no <c>bytes</c> or <c>encoding</c> in its header.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="package">The packet.</param>
    internal static void Check(PdfAValidationContext context, PdfAXmpPackage package)
    {
        var packet = package.Packet;
        var where = package.Context;
        context.Require(packet.IsWellFormed, PdfARule.XmpSerialization, where);
        context.Require(string.Equals(packet.Encoding, PdfAXmpPacketRepair.Utf8, StringComparison.Ordinal), PdfARule.XmpEncodingUtf8, where);
        context.Require(packet.Header is null || !PdfAXmpPatterns.BytesAttribute().IsMatch(packet.Header), PdfARule.XmpPacketBytes, where);
        context.Require(packet.Header is null || !PdfAXmpPatterns.EncodingAttribute().IsMatch(packet.Header), PdfARule.XmpPacketEncoding, where);
    }

    /// <summary>Checks the catalog packet's PDF/A identification.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="packet">The catalog packet.</param>
    internal static void CheckIdentification(PdfAValidationContext context, PdfAXmpPacket packet)
    {
        const string where = PdfAContexts.CatalogMetadata;
        var identified = packet.HasNamespace(PdfAXmpNames.PdfAId);
        context.Require(identified, PdfARule.CatalogXmpIdentification, where);
        if (!identified)
        {
            return;
        }

        var part = Part(packet);
        context.Require(part == Part1, PdfARule.IdentificationPart1, where);
        context.Require(part == Part2, PdfARule.IdentificationPart2, where);
        context.Require(part == Part3, PdfARule.IdentificationPart3, where);
        context.Require(part == Part4, PdfARule.IdentificationPart4, where);
        CheckConformance(context, Simple(packet, ConformanceProperty));
        context.Require(Simple(packet, RevisionProperty) is Revision2020, PdfARule.IdentificationRevision, where);
        CheckPrefix(context, packet, PartProperty, PdfARule.IdentificationPartPrefix);
        CheckPrefix(context, packet, ConformanceProperty, PdfARule.IdentificationConformancePrefix);
        CheckPrefix(context, packet, "amd", PdfARule.IdentificationAmendmentPrefix);
        CheckPrefix(context, packet, "corr", PdfARule.IdentificationCorrigendumPrefix);
        CheckPrefix(context, packet, RevisionProperty, PdfARule.IdentificationRevisionPrefix);
    }

    /// <summary>Checks <c>pdfaid:conformance</c> against each level's allowed letters.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="conformance">The value, or <see langword="null"/>.</param>
    private static void CheckConformance(PdfAValidationContext context, string? conformance)
    {
        const string where = PdfAContexts.CatalogMetadata;
        context.Require(conformance is "A", PdfARule.IdentificationConformanceA, where);
        context.Require(conformance is "A" or "B", PdfARule.IdentificationConformancePdfA1, where);
        context.Require(conformance is "A" or "B" or "U", PdfARule.IdentificationConformancePdfA2, where);
        context.Require(conformance is "A" or "U", PdfARule.IdentificationConformanceU, where);
        context.Require(conformance is "E", PdfARule.IdentificationConformanceE, where);
        context.Require(conformance is "F", PdfARule.IdentificationConformanceF, where);
        context.Require(conformance is null, PdfARule.IdentificationConformanceNone, where);
    }

    /// <summary>Checks that an identification property, when present, uses the <c>pdfaid</c> prefix.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="packet">The catalog packet.</param>
    /// <param name="name">The property's local name.</param>
    /// <param name="rule">The rule.</param>
    private static void CheckPrefix(PdfAValidationContext context, PdfAXmpPacket packet, string name, PdfARule rule)
    {
        var prefix = packet.Property(PdfAXmpNames.PdfAId, name)?.Prefix;
        context.Require(prefix is null or IdentificationPrefix, rule, PdfAContexts.CatalogMetadata);
    }

    /// <summary>Reads <c>pdfaid:part</c> as an integer, as Java's <c>Integer.parseInt</c> reads it.</summary>
    /// <param name="packet">The catalog packet.</param>
    /// <returns>The part, or <see langword="null"/> when missing, not simple or not an integer.</returns>
    private static int? Part(PdfAXmpPacket packet) =>
        Simple(packet, PartProperty) is { } text && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>Reads a simple identification property.</summary>
    /// <param name="packet">The catalog packet.</param>
    /// <param name="name">The local name.</param>
    /// <returns>The value, or <see langword="null"/> when missing or not simple.</returns>
    private static string? Simple(PdfAXmpPacket packet, string name) => packet.TryGetSimple(PdfAXmpNames.PdfAId, name, out var value) ? value : null;
}
