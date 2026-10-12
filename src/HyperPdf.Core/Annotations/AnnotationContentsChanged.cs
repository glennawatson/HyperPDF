// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>An annotation's note text changed.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation index.</param>
/// <param name="Before">The text before.</param>
/// <param name="After">The text after.</param>
[DebuggerDisplay("AnnotationContentsChanged: page {PageIndex}, annotation {Index}")]
public sealed record AnnotationContentsChanged(int PageIndex, int Index, string Before, string After) : AnnotationChange(PageIndex, Index)
{
    /// <inheritdoc/>
    public override bool Undo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetContents(PageIndex, Index, Before);
    }

    /// <inheritdoc/>
    public override bool Redo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetContents(PageIndex, Index, After);
    }
}
