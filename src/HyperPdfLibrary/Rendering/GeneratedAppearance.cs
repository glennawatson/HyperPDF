// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>An appearance drawn for an annotation that has none, held in memory and never written to the document.</summary>
/// <param name="Form">The appearance.</param>
/// <param name="Overlay">A second appearance drawn over the first, such as the text of a free text annotation, or null.</param>
/// <param name="Rect">The rectangle the appearance is fitted to, which can differ from /Rect, for example round text markup quadrilaterals.</param>
[DebuggerDisplay("GeneratedAppearance: {Rect}")]
internal readonly record struct GeneratedAppearance(PdfStream Form, PdfStream? Overlay, PdfRectangle Rect);
