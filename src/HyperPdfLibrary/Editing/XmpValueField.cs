// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>One field of a structured XMP value type, as a PDF/A extension schema describes it (ISO 19005-2, 6.6.2.3.3).</summary>
/// <param name="Name">The field's local name.</param>
/// <param name="ValueType">The field's value type, such as Text, URI or Bag Claim.</param>
/// <param name="Description">What the field holds.</param>
[DebuggerDisplay("XmpValueField: {Name}")]
internal sealed record XmpValueField(string Name, string ValueType, string Description);
