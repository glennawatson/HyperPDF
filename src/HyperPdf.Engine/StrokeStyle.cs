// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>How strokes are written: the annotation subtype, the kind (which picks the subject and how points join), the colour and the line width.</summary>
/// <param name="Subtype">The subtype.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
/// <param name="Width">The line width in points.</param>
[DebuggerDisplay("StrokeStyle: {Kind} {Width}pt")]
internal readonly record struct StrokeStyle(KnownName Subtype, AnnotationKind Kind, uint Color, float Width);
