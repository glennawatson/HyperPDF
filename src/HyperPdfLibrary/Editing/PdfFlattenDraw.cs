// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>One annotation to draw into a page's content, with how it is marked.</summary>
/// <param name="Form">The appearance and its matrix.</param>
/// <param name="Overlay">A second appearance drawn over the first, such as the text of a free text annotation, or null.</param>
/// <param name="Layer">The annotation's optional content (<c>/OC</c>) as stored, or null for none.</param>
/// <param name="Marking">How the drawing is marked in a tagged document.</param>
/// <param name="Blend">The blend mode the annotation is painted with (its <c>/BM</c>), or none for Normal.</param>
[DebuggerDisplay("PdfFlattenDraw: {Form} {Marking}")]
internal readonly record struct PdfFlattenDraw(PdfFlattenForm Form, PdfFlattenForm? Overlay, PdfValue Layer, PdfFlattenMarking Marking, PdfName Blend);
