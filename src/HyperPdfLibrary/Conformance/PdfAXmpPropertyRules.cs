// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on XMP properties (ISO 19005-1, 6.7.9; ISO 19005-2, 6.6.2.3.1): each property is predefined or defined by
/// an extension schema, and its value matches the defined type.
/// </summary>
internal static class PdfAXmpPropertyRules
{
    /// <summary>The first part of ISO 19005.</summary>
    private const int Part1 = 1;

    /// <summary>Gets the rules these checks record.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.XmpPropertyDefinedPdfA1,
        PdfARule.XmpPropertyDefined,
        PdfARule.XmpPropertyValueType,
    ];

    /// <summary>Checks every top-level property of a packet.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="package">The packet.</param>
    internal static void Check(PdfAValidationContext context, PdfAXmpPackage package)
    {
        var pdfA1 = context.Part == Part1;
        var predefined = PdfAXmpSchemaTable.Predefined(pdfA1);
        var definedRule = pdfA1 ? PdfARule.XmpPropertyDefinedPdfA1 : PdfARule.XmpPropertyDefined;
        foreach (var property in package.Packet.Properties)
        {
            var key = property.Namespace ?? string.Empty;
            var own = package.Definitions.GetValueOrDefault(key);
            var main = pdfA1 ? null : package.MainDefinitions.GetValueOrDefault(key);
            var defined = predefined.Defines(property) || Defines(own, property) || Defines(main, property);
            var matches = Matches(property, own, main, predefined);
            if (defined && matches)
            {
                continue;
            }

            var where = Describe(package.Context, property);
            context.Require(defined, definedRule, where);
            context.Require(matches, PdfARule.XmpPropertyValueType, where);
        }
    }

    /// <summary>Determines whether a property's value matches the type of the first definition that holds it.</summary>
    /// <param name="property">The property.</param>
    /// <param name="own">The packet's own extension schema for the namespace.</param>
    /// <param name="main">The catalog packet's extension schema for the namespace.</param>
    /// <param name="predefined">The predefined schemas.</param>
    /// <returns><see langword="true"/> when a definition holds it and the value matches; a property nothing defines does not match.</returns>
    private static bool Matches(PdfAXmpNode property, PdfAXmpSchemaDefinition? own, PdfAXmpSchemaDefinition? main, PdfAXmpSchemaDefinition predefined) =>
        FirstDefining(property, own, main, predefined)?.Matches(property) ?? false;

    /// <summary>Determines whether an optional definition holds a property.</summary>
    /// <param name="definition">The definition, or <see langword="null"/>.</param>
    /// <param name="property">The property.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Defines(PdfAXmpSchemaDefinition? definition, PdfAXmpNode property) => definition is not null && definition.Defines(property);

    /// <summary>Gets the first definition, in the order veraPDF looks, that holds a property.</summary>
    /// <param name="property">The property.</param>
    /// <param name="own">The packet's own extension schema for the namespace.</param>
    /// <param name="main">The catalog packet's extension schema for the namespace.</param>
    /// <param name="predefined">The predefined schemas.</param>
    /// <returns>The definition, or <see langword="null"/> when none holds the property.</returns>
    private static PdfAXmpSchemaDefinition? FirstDefining(PdfAXmpNode property, PdfAXmpSchemaDefinition? own, PdfAXmpSchemaDefinition? main, PdfAXmpSchemaDefinition predefined)
    {
        if (Defines(own, property))
        {
            return own;
        }

        if (Defines(main, property))
        {
            return main;
        }

        return predefined.Defines(property) ? predefined : null;
    }

    /// <summary>Names a property for the report.</summary>
    /// <param name="packet">Where the packet was found.</param>
    /// <param name="property">The property.</param>
    /// <returns>For example "catalog metadata, dc:title".</returns>
    private static string Describe(string packet, PdfAXmpNode property) =>
        property.Prefix is null ? $"{packet}, {property.Name}" : $"{packet}, {property.Prefix}:{property.Name}";
}
