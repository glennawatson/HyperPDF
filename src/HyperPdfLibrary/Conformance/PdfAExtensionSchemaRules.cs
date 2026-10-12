// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on extension schema descriptions (ISO 19005-1, 6.7.8; ISO 19005-2, 6.6.2.3.2 and 6.6.2.3.3): the
/// container is a bag, and each schema, property, value type and field description has the fields the PDF/A extension
/// schema description schemas define, of the right form and with the right prefix.
/// </summary>
internal static class PdfAExtensionSchemaRules
{
    /// <summary>The first part of ISO 19005.</summary>
    private const int Part1 = 1;

    /// <summary>The prefix of the extension schema container.</summary>
    private const string ContainerPrefix = "pdfaExtension";

    /// <summary>The fields of a schema description.</summary>
    private static readonly PdfAExtensionField[] SchemaFields =
    [
        new("schema", PdfARule.ExtensionSchemaName, PdfAExtensionFieldForm.RequiredText),
        new(PdfAXmpExtensionDefinitions.NamespaceUriField, PdfARule.ExtensionSchemaNamespaceUri, PdfAExtensionFieldForm.RequiredText),
        new("prefix", PdfARule.ExtensionSchemaPrefix, PdfAExtensionFieldForm.RequiredText),
        new(PdfAXmpExtensionDefinitions.PropertyField, PdfARule.ExtensionSchemaProperties, PdfAExtensionFieldForm.OptionalSequence),
        new(PdfAXmpExtensionDefinitions.ValueTypeField, PdfARule.ExtensionSchemaValueTypes, PdfAExtensionFieldForm.OptionalSequence),
    ];

    /// <summary>The fields of a property description.</summary>
    private static readonly PdfAExtensionField[] PropertyFields =
    [
        new(PdfAXmpExtensionDefinitions.NameField, PdfARule.ExtensionPropertyName, PdfAExtensionFieldForm.RequiredText),
        new(PdfAXmpExtensionDefinitions.ValueTypeField, PdfARule.ExtensionPropertyValueType, PdfAExtensionFieldForm.RequiredValueType),
        new("category", PdfARule.ExtensionPropertyCategory, PdfAExtensionFieldForm.RequiredCategory),
        new("description", PdfARule.ExtensionPropertyDescription, PdfAExtensionFieldForm.RequiredText),
    ];

    /// <summary>The fields of a value type description.</summary>
    private static readonly PdfAExtensionField[] ValueTypeFields =
    [
        new(PdfAXmpExtensionDefinitions.TypeField, PdfARule.ExtensionValueTypeName, PdfAExtensionFieldForm.RequiredText),
        new(PdfAXmpExtensionDefinitions.NamespaceUriField, PdfARule.ExtensionValueTypeNamespaceUri, PdfAExtensionFieldForm.RequiredText),
        new("prefix", PdfARule.ExtensionValueTypePrefix, PdfAExtensionFieldForm.RequiredText),
        new("description", PdfARule.ExtensionValueTypeDescription, PdfAExtensionFieldForm.RequiredText),
        new(PdfAXmpExtensionDefinitions.FieldsField, PdfARule.ExtensionValueTypeFields, PdfAExtensionFieldForm.OptionalSequence),
    ];

    /// <summary>The fields of a field description.</summary>
    private static readonly PdfAExtensionField[] FieldFields =
    [
        new(PdfAXmpExtensionDefinitions.NameField, PdfARule.ExtensionFieldName, PdfAExtensionFieldForm.RequiredText),
        new(PdfAXmpExtensionDefinitions.ValueTypeField, PdfARule.ExtensionFieldValueType, PdfAExtensionFieldForm.RequiredValueType),
        new("description", PdfARule.ExtensionFieldDescription, PdfAExtensionFieldForm.RequiredText),
    ];

    /// <summary>Gets the rules these checks record.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.ExtensionContainer,
        PdfARule.ExtensionUndefinedFields,
        .. Array.ConvertAll(SchemaFields, static entry => entry.Rule),
        .. Array.ConvertAll(PropertyFields, static entry => entry.Rule),
        .. Array.ConvertAll(ValueTypeFields, static entry => entry.Rule),
        .. Array.ConvertAll(FieldFields, static entry => entry.Rule),
    ];

    /// <summary>Checks a packet's extension schema container and the descriptions in it.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="package">The packet.</param>
    internal static void Check(PdfAValidationContext context, PdfAXmpPackage package)
    {
        if (package.Packet.ExtensionSchemas is not { } container)
        {
            return;
        }

        var isBag = (container.Form & PdfAXmpForm.Array) != 0 && (container.Form & (PdfAXmpForm.Ordered | PdfAXmpForm.Alternate)) == 0;
        context.Require(isBag && string.Equals(container.Prefix, ContainerPrefix, StringComparison.Ordinal), PdfARule.ExtensionContainer, package.Context);
        if ((container.Form & PdfAXmpForm.Array) == 0)
        {
            return;
        }

        foreach (var schema in container.Children)
        {
            CheckSchema(context, package, schema);
        }
    }

    /// <summary>Checks a schema description and the property and value type descriptions in it.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="package">The packet.</param>
    /// <param name="schema">The schema description.</param>
    private static void CheckSchema(PdfAValidationContext context, PdfAXmpPackage package, PdfAXmpNode schema)
    {
        var where = package.Context;
        CheckDescription(context, schema, PdfAXmpNames.PdfASchema, SchemaFields, PdfAXmpSchemaTable.BaseTypes, where);
        var namespaceUri = schema.Child(PdfAXmpNames.PdfASchema, PdfAXmpExtensionDefinitions.NamespaceUriField)?.Value;
        var types = namespaceUri is not null && package.Definitions.TryGetValue(namespaceUri, out var definition) ? definition.Types : PdfAXmpSchemaTable.BaseTypes;
        if (FirstArray(schema, PdfAXmpExtensionDefinitions.PropertyField) is { } properties)
        {
            foreach (var property in properties.Children)
            {
                CheckDescription(context, property, PdfAXmpNames.PdfAProperty, PropertyFields, types, where);
            }
        }

        if (FirstArray(schema, PdfAXmpExtensionDefinitions.ValueTypeField) is not { } valueTypes)
        {
            return;
        }

        foreach (var valueType in valueTypes.Children)
        {
            CheckDescription(context, valueType, PdfAXmpNames.PdfAType, ValueTypeFields, types, where);
            if (PdfAXmpExtensionDefinitions.ArrayChild(valueType, PdfAXmpNames.PdfAType, PdfAXmpExtensionDefinitions.FieldsField) is not { } fields)
            {
                continue;
            }

            foreach (var field in fields.Children)
            {
                CheckDescription(context, field, PdfAXmpNames.PdfAField, FieldFields, types, where);
            }
        }
    }

    /// <summary>Checks one description: its known fields, and that it has no others.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="description">The description.</param>
    /// <param name="namespaceUri">The namespace its fields must be in.</param>
    /// <param name="fields">The fields it may have.</param>
    /// <param name="types">The value types known where it is.</param>
    /// <param name="where">Where the packet was found.</param>
    private static void CheckDescription(PdfAValidationContext context, PdfAXmpNode description, string namespaceUri, PdfAExtensionField[] fields, PdfAXmpTypeSet types, string where)
    {
        var prefix = Prefix(namespaceUri);
        foreach (var field in fields)
        {
            var node = description.Child(namespaceUri, field.Name);
            context.Require(Holds(node, field.Form, prefix, types), field.Rule, where);
        }

        foreach (var child in description.Children)
        {
            var known = string.Equals(child.Namespace, namespaceUri, StringComparison.Ordinal) && Array.Exists(fields, field => string.Equals(field.Name, child.Name, StringComparison.Ordinal));
            context.Require(known, PdfARule.ExtensionUndefinedFields, where);
        }
    }

    /// <summary>Determines whether a field holds its rule.</summary>
    /// <param name="node">The field, or <see langword="null"/> when absent.</param>
    /// <param name="form">What the field must be.</param>
    /// <param name="prefix">The prefix it must use.</param>
    /// <param name="types">The value types known where it is.</param>
    /// <returns><see langword="true"/> when it holds.</returns>
    private static bool Holds(PdfAXmpNode? node, PdfAExtensionFieldForm form, string prefix, PdfAXmpTypeSet types)
    {
        if (form == PdfAExtensionFieldForm.OptionalSequence)
        {
            return node is null || ((node.Form & PdfAXmpForm.Ordered) != 0 && (node.Prefix is null || string.Equals(node.Prefix, prefix, StringComparison.Ordinal)));
        }

        if (node is null || !node.IsSimple || !string.Equals(node.Prefix, prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return form switch
        {
            PdfAExtensionFieldForm.RequiredValueType => PdfAXmpTypeChecks.IsKnown(node.Value, types),
            PdfAExtensionFieldForm.RequiredCategory => node.Value is "external" or "internal",
            _ => true,
        };
    }

    /// <summary>Gets the first schema field with a name, when it is an array.</summary>
    /// <param name="schema">The schema description.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The array, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfAXmpNode? FirstArray(PdfAXmpNode schema, string name) =>
        PdfAXmpExtensionDefinitions.ArrayChild(schema, PdfAXmpNames.PdfASchema, name);

    /// <summary>Gets the prefix the fields of a description schema must use.</summary>
    /// <param name="namespaceUri">The description schema's namespace.</param>
    /// <returns>The prefix.</returns>
    private static string Prefix(string namespaceUri) => namespaceUri switch
    {
        PdfAXmpNames.PdfASchema => "pdfaSchema",
        PdfAXmpNames.PdfAProperty => "pdfaProperty",
        PdfAXmpNames.PdfAType => "pdfaType",
        _ => "pdfaField",
    };
}
