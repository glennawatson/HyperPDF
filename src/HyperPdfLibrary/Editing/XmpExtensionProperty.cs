// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>A custom XMP property and the schema it belongs to, as a PDF/A extension schema describes them.</summary>
/// <param name="SchemaName">The schema's description (<c>pdfaSchema:schema</c>).</param>
/// <param name="Namespace">The schema's namespace URI.</param>
/// <param name="Prefix">The schema's preferred prefix.</param>
/// <param name="Name">The property's local name.</param>
/// <param name="ValueType">The property's XMP value type, such as Boolean or Text.</param>
/// <param name="Description">What the property records.</param>
[DebuggerDisplay("XmpExtensionProperty: {Prefix}:{Name}")]
internal sealed record XmpExtensionProperty(string SchemaName, string Namespace, string Prefix, string Name, string ValueType, string Description);
