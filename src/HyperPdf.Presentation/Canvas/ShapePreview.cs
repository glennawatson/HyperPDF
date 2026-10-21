// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Presentation.Canvas;

/// <summary>The preview state of a shape being dragged on a page.</summary>
/// <param name="Kind">The shape kind.</param>
/// <param name="Page">The page index.</param>
/// <param name="Start">Where the drag started in page coordinates.</param>
/// <param name="End">Where the pointer is now in page coordinates.</param>
[DebuggerDisplay("ShapePreview: {Kind} on page {Page}")]
public readonly record struct ShapePreview(AnnotationKind Kind, int Page, PagePoint Start, PagePoint End);
