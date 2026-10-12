// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The name of an XML element.</summary>
/// <param name="Namespace">The namespace URI, or <see langword="null"/> when it has none.</param>
/// <param name="Prefix">The prefix as written, or <see langword="null"/> when there is none.</param>
/// <param name="LocalName">The local name.</param>
/// <param name="QualifiedName">The name as written.</param>
internal readonly record struct PdfAXmlName(string? Namespace, string? Prefix, string LocalName, string QualifiedName)
{
    /// <summary>Gets the empty name of a node that is not an element.</summary>
    internal static PdfAXmlName None => new(null, null, string.Empty, string.Empty);
}
