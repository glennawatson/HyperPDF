// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>A text box's or callout's text size changed.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation index.</param>
/// <param name="Before">The size before, in points.</param>
/// <param name="After">The size after, in points.</param>
[DebuggerDisplay("AnnotationFontSizeChanged: page {PageIndex}, annotation {Index}")]
public sealed record AnnotationFontSizeChanged(int PageIndex, int Index, float Before, float After) : AnnotationChange(PageIndex, Index)
{
    /// <inheritdoc/>
    public override bool Undo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetFontSize(PageIndex, Index, Before);
    }

    /// <inheritdoc/>
    public override bool Redo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetFontSize(PageIndex, Index, After);
    }
}
