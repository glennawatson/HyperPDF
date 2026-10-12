// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Builds the properties and value types a packet's PDF/A extension schemas define (ISO 19005-1, 6.7.8; ISO 19005-2,
/// 6.6.2.3). A schema counts when it has a namespace URI and a property array; its value types extend the part's
/// predefined types, or the types the main packet's schema for the same namespace already defines.
/// </summary>
internal static class PdfAXmpExtensionDefinitions
{
    /// <summary>The name of the schema field holding the namespace URI.</summary>
    internal const string NamespaceUriField = "namespaceURI";

    /// <summary>The name of the schema field holding the properties.</summary>
    internal const string PropertyField = "property";

    /// <summary>The name of the schema and property field holding value types.</summary>
    internal const string ValueTypeField = "valueType";

    /// <summary>The name of the entry holding a property's or a structure field's name.</summary>
    internal const string NameField = "name";

    /// <summary>The name of the value type field holding the type name.</summary>
    internal const string TypeField = "type";

    /// <summary>The name of the value type field holding the fields.</summary>
    internal const string FieldsField = "field";

    /// <summary>Builds the definitions in a packet's extension schemas.</summary>
    /// <param name="schemas">The <c>pdfaExtension:schemas</c> property, or <see langword="null"/>.</param>
    /// <param name="pdfA1">Whether PDF/A-1, rather than PDF/A-2 or PDF/A-3, is checked.</param>
    /// <param name="inherited">The main packet's definitions, whose value types a schema for the same namespace extends.</param>
    /// <returns>The definitions by namespace URI.</returns>
    internal static Dictionary<string, PdfAXmpSchemaDefinition> Build(PdfAXmpNode? schemas, bool pdfA1, Dictionary<string, PdfAXmpSchemaDefinition> inherited)
    {
        var definitions = new Dictionary<string, PdfAXmpSchemaDefinition>(StringComparer.Ordinal);
        if (schemas is null || (schemas.Form & PdfAXmpForm.Array) == 0)
        {
            return definitions;
        }

        foreach (var schema in schemas.Children)
        {
            var namespaceUri = schema.Child(PdfAXmpNames.PdfASchema, NamespaceUriField)?.Value;
            if (namespaceUri is not null && ArrayChild(schema, PdfAXmpNames.PdfASchema, PropertyField) is { } properties)
            {
                definitions[namespaceUri] = BuildSchema(schema, namespaceUri, properties, pdfA1, inherited);
            }
        }

        return definitions;
    }

    /// <summary>Gets a field of a structure when it is an array.</summary>
    /// <param name="node">The structure.</param>
    /// <param name="namespaceUri">The field's namespace.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The field, or <see langword="null"/> when it is missing or not an array.</returns>
    internal static PdfAXmpNode? ArrayChild(PdfAXmpNode node, string namespaceUri, string name) =>
        node.Child(namespaceUri, name) is { } child && (child.Form & PdfAXmpForm.Array) != 0 ? child : null;

    /// <summary>Builds one schema's definition: its value types, then the properties whose types are known.</summary>
    /// <param name="schema">The schema description.</param>
    /// <param name="namespaceUri">The schema's namespace URI.</param>
    /// <param name="properties">The schema's property array.</param>
    /// <param name="pdfA1">Whether PDF/A-1, rather than PDF/A-2 or PDF/A-3, is checked.</param>
    /// <param name="inherited">The main packet's definitions.</param>
    /// <returns>The definition.</returns>
    private static PdfAXmpSchemaDefinition BuildSchema(PdfAXmpNode schema, string namespaceUri, PdfAXmpNode properties, bool pdfA1, Dictionary<string, PdfAXmpSchemaDefinition> inherited)
    {
        var types = inherited.TryGetValue(namespaceUri, out var main) ? main.Types.Copy() : PdfAXmpSchemaTable.CreatePredefinedTypes(pdfA1);
        AddValueTypes(types, ArrayChild(schema, PdfAXmpNames.PdfASchema, ValueTypeField));
        var definition = new PdfAXmpSchemaDefinition(types);
        foreach (var property in properties.Children)
        {
            var name = property.Child(PdfAXmpNames.PdfAProperty, NameField)?.Value;
            var valueType = property.Child(PdfAXmpNames.PdfAProperty, ValueTypeField)?.Value;
            if (name is not null && valueType is not null)
            {
                definition.Register(new(namespaceUri, name), valueType);
            }
        }

        return definition;
    }

    /// <summary>Adds an extension schema's value types: a type without fields is text, one with fields and a namespace a structure.</summary>
    /// <param name="types">The types to add to.</param>
    /// <param name="valueTypes">The schema's <c>pdfaSchema:valueType</c> array, or <see langword="null"/>.</param>
    private static void AddValueTypes(PdfAXmpTypeSet types, PdfAXmpNode? valueTypes)
    {
        if (valueTypes is null)
        {
            return;
        }

        foreach (var valueType in valueTypes.Children)
        {
            if (valueType.Child(PdfAXmpNames.PdfAType, TypeField)?.Value is not { } name)
            {
                continue;
            }

            var fields = FieldTypes(ArrayChild(valueType, PdfAXmpNames.PdfAType, FieldsField));
            var namespaceUri = valueType.Child(PdfAXmpNames.PdfAType, NamespaceUriField)?.Value;
            if (fields.Count == 0)
            {
                types.Types[PdfAXmpTypeChecks.Simplify(name)] = PdfAXmpType.AnyText;
            }
            else if (namespaceUri is not null)
            {
                types.Types[PdfAXmpTypeChecks.Simplify(name)] = new(PdfAXmpTypeKind.Structure, null, namespaceUri, fields, types);
            }
        }
    }

    /// <summary>Reads the field types of a value type.</summary>
    /// <param name="fields">The <c>pdfaType:field</c> array, or <see langword="null"/>.</param>
    /// <returns>Each field's value type name, by field name.</returns>
    private static Dictionary<string, string> FieldTypes(PdfAXmpNode? fields)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fields is null)
        {
            return types;
        }

        foreach (var field in fields.Children)
        {
            var name = field.Child(PdfAXmpNames.PdfAField, NameField)?.Value;
            var valueType = field.Child(PdfAXmpNames.PdfAField, ValueTypeField)?.Value;
            if (name is not null && valueType is not null)
            {
                types[name] = valueType;
            }
        }

        return types;
    }
}
