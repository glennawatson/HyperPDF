// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>The state of an active annotation move or resize drag on the canvas.</summary>
/// <param name="Annotation">The annotation being moved or resized.</param>
/// <param name="Anchor">Where the drag started in page space, or the opposite corner when resizing.</param>
/// <param name="PressPosition">Where the drag started in canvas coordinates.</param>
/// <param name="Bounds">The preview bounds in page space.</param>
/// <param name="Resizing">Whether a corner resize handle is being dragged.</param>
[DebuggerDisplay("AnnotationEditSession: resizing={Resizing}")]
public readonly record struct AnnotationEditSession(PageAnnotation Annotation, PagePoint Anchor, CanvasPoint PressPosition, PageRect Bounds, bool Resizing);
