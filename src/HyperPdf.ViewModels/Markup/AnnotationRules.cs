// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Rules about how annotations may be edited and drawn, which every render head follows alike.</summary>
public static class AnnotationRules
{
    /// <summary>Determines whether an annotation shows resize handles: movable kinds other than notes, which keep their icon size.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it can be resized.</returns>
    public static bool CanResize(PageAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.IsMovable && annotation.Kind != AnnotationKind.Note;
    }

    /// <summary>Determines whether a kind is drawn as lines only, so the page shows through it.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for drawings, lines and unfilled shapes.</returns>
    public static bool IsOutline(AnnotationKind kind) =>
        kind is AnnotationKind.Ink or AnnotationKind.Rectangle or AnnotationKind.Ellipse or AnnotationKind.Arrow or AnnotationKind.Line
            or AnnotationKind.Polygon or AnnotationKind.Cloud or AnnotationKind.PolyLine;
}
