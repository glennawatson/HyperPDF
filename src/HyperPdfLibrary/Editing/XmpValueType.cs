// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>A structured XMP value type that a PDF/A extension schema defines for its properties (ISO 19005-2, 6.6.2.3.3).</summary>
/// <param name="Name">The type's name, which a property or field names as its value type.</param>
/// <param name="Description">What the type holds.</param>
/// <param name="Fields">The type's fields.</param>
[DebuggerDisplay("XmpValueType: {Name}")]
internal sealed record XmpValueType(string Name, string Description, ImmutableArray<XmpValueField> Fields);
