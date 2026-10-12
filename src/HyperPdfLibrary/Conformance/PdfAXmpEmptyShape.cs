// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What the attributes of an empty RDF property element make it.</summary>
/// <param name="Value">The attribute holding the value, <c>rdf:value</c> or <c>rdf:resource</c>, or <see langword="null"/>.</param>
/// <param name="HasValue">Whether the value comes from <c>rdf:value</c>.</param>
/// <param name="HasProperties">Whether other attributes make the element a structure.</param>
internal sealed record PdfAXmpEmptyShape(PdfAXmlAttribute? Value, bool HasValue, bool HasProperties);
