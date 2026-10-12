// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>An annotation was moved or resized.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation index.</param>
/// <param name="Before">The bounds before.</param>
/// <param name="After">The bounds after.</param>
[DebuggerDisplay("AnnotationMoved: page {PageIndex}, annotation {Index}")]
public sealed record AnnotationMoved(int PageIndex, int Index, PageRect Before, PageRect After) : AnnotationChange(PageIndex, Index)
{
    /// <summary>Gets a value indicating whether this came from the arrow keys, so following presses join it as one step.</summary>
    public bool IsNudge { get; init; }

    /// <inheritdoc/>
    public override bool Undo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetBounds(PageIndex, Index, Before);
    }

    /// <inheritdoc/>
    public override bool Redo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetBounds(PageIndex, Index, After);
    }
}
