// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>A set of XMP properties with their value types: the predefined schemas, or one extension schema.</summary>
[DebuggerDisplay("PdfAXmpSchemaDefinition: {Properties.Count} properties")]
internal sealed class PdfAXmpSchemaDefinition
{
    /// <summary>Initializes a new instance of the <see cref="PdfAXmpSchemaDefinition"/> class.</summary>
    /// <param name="types">The value types the properties use.</param>
    internal PdfAXmpSchemaDefinition(PdfAXmpTypeSet types) => Types = types;

    /// <summary>Gets the value types the properties use.</summary>
    internal PdfAXmpTypeSet Types { get; }

    /// <summary>Gets each property's value type name.</summary>
    internal Dictionary<PdfAXmpPropertyKey, string> Properties { get; } = [];

    /// <summary>Adds a property when its type is known and it is not defined yet.</summary>
    /// <param name="key">The property.</param>
    /// <param name="typeName">The value type name.</param>
    internal void Register(PdfAXmpPropertyKey key, string typeName)
    {
        if (PdfAXmpTypeChecks.IsKnown(typeName, Types))
        {
            _ = Properties.TryAdd(key, typeName);
        }
    }

    /// <summary>Determines whether the definition holds a property.</summary>
    /// <param name="node">The property.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Defines(PdfAXmpNode node) => Properties.ContainsKey(new(node.Namespace, node.Name));

    /// <summary>Checks a property's value against its defined type.</summary>
    /// <param name="node">The property, which the definition holds.</param>
    /// <returns><see langword="true"/> when the value matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Matches(PdfAXmpNode node) => PdfAXmpTypeChecks.Matches(node, Properties[new(node.Namespace, node.Name)], Types);
}
