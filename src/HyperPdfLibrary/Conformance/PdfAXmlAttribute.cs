// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>An attribute of an XML element in an XMP packet.</summary>
/// <param name="Namespace">The namespace URI, or <see langword="null"/> when the attribute has none.</param>
/// <param name="Prefix">The prefix as written, or <see langword="null"/> when there is none.</param>
/// <param name="LocalName">The local name.</param>
/// <param name="QualifiedName">The name as written, with its prefix.</param>
/// <param name="Value">The value.</param>
internal sealed record PdfAXmlAttribute(string? Namespace, string? Prefix, string LocalName, string QualifiedName, string Value);
