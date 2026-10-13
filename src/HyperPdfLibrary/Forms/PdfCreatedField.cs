// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A field the library created.</summary>
/// <param name="Name">The field's fully qualified name.</param>
/// <param name="FieldId">The object that holds the field (the merged widget when it has one widget).</param>
/// <param name="Widgets">The field's widgets, in the order they were given.</param>
[DebuggerDisplay("PdfCreatedField: {Name}")]
public sealed record PdfCreatedField(string Name, PdfObjectId FieldId, PdfWidgetLocation[] Widgets);
