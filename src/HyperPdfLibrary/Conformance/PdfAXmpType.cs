// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace HyperPdfLibrary.Conformance;

/// <summary>An XMP value type the PDF/A property checks know.</summary>
/// <param name="Kind">How values of the type are checked.</param>
/// <param name="Pattern">The pattern a simple value matches, or <see langword="null"/> for any text.</param>
/// <param name="FieldNamespace">The namespace of a structure's fields.</param>
/// <param name="Fields">The value type of each structure field, by local name.</param>
/// <param name="Home">The type set a structure's field types are looked up in.</param>
internal sealed record PdfAXmpType(PdfAXmpTypeKind Kind, Regex? Pattern, string? FieldNamespace, Dictionary<string, string>? Fields, PdfAXmpTypeSet? Home)
{
    /// <summary>Gets a simple type that takes any text.</summary>
    internal static PdfAXmpType AnyText { get; } = new(PdfAXmpTypeKind.Simple, null, null, null, null);

    /// <summary>Makes a simple type with a pattern.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The type.</returns>
    internal static PdfAXmpType Matching(Regex pattern) => new(PdfAXmpTypeKind.Simple, pattern, null, null, null);
}
