// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>A signature field found in the document's interactive form.</summary>
/// <param name="Id">The field's object id; invalid when the field is a direct object.</param>
/// <param name="Dictionary">The field's dictionary.</param>
/// <param name="Name">The fully qualified field name.</param>
/// <param name="WidgetId">The object id of the field's widget (the field itself when it is merged with its widget); invalid when it is a direct object.</param>
/// <param name="Widget">The widget annotation's dictionary, or <see langword="null"/> when the field has none.</param>
[DebuggerDisplay("PdfSigningField: {Name}")]
internal sealed record PdfSigningField(PdfObjectId Id, PdfDictionary Dictionary, string Name, PdfObjectId WidgetId, PdfDictionary? Widget)
{
    /// <summary>Gets a value indicating whether the field already holds a signature.</summary>
    internal bool IsSigned => Dictionary.ContainsKey(KnownName.V) && !Dictionary.Get(KnownName.V).IsNull;
}
