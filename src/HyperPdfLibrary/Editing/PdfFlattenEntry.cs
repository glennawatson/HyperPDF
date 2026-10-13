// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>One entry of a page's /Annots with what flattening does with it.</summary>
/// <param name="Index">The entry's index in the array.</param>
/// <param name="Raw">The entry as stored: a reference, or the annotation itself.</param>
/// <param name="Annotation">The annotation, or <see langword="null"/> when the entry does not resolve to a dictionary.</param>
/// <param name="Decision">What happens to it.</param>
/// <param name="Appearance">The appearance to draw, when the decision is to draw it.</param>
[DebuggerDisplay("PdfFlattenEntry: {Index} {Decision}")]
internal readonly record struct PdfFlattenEntry(int Index, PdfValue Raw, PdfDictionary? Annotation, PdfFlattenDecision Decision, PdfFlattenAppearance? Appearance)
{
    /// <summary>Gets a value indicating whether the annotation is a form field widget.</summary>
    internal bool IsWidget => Annotation?.IsName(KnownName.Subtype, KnownName.Widget) == true;

    /// <summary>Gets the annotation's subtype as text, for reports.</summary>
    /// <param name="names">The document's names.</param>
    /// <returns>The subtype.</returns>
    internal string GetSubtype(PdfNameTable names) => Annotation is { } annotation ? names.GetString(annotation.GetName(KnownName.Subtype)) : string.Empty;
}
