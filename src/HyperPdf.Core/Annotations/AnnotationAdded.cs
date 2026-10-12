// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>An annotation, reply or review status was added; undoing takes it off the page again.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation index.</param>
[DebuggerDisplay("AnnotationAdded: page {PageIndex}, annotation {Index}")]
public sealed record AnnotationAdded(int PageIndex, int Index) : AnnotationChange(PageIndex, Index)
{
    /// <inheritdoc/>
    public override bool Undo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetRemoved(PageIndex, Index, true);
    }

    /// <inheritdoc/>
    public override bool Redo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetRemoved(PageIndex, Index, false);
    }
}
