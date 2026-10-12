// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>An annotation's colour changed.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation index.</param>
/// <param name="Before">The colour before, as 0xRRGGBB.</param>
/// <param name="After">The colour after, as 0xRRGGBB.</param>
[DebuggerDisplay("AnnotationRecolored: page {PageIndex}, annotation {Index}")]
public sealed record AnnotationRecolored(int PageIndex, int Index, uint Before, uint After) : AnnotationChange(PageIndex, Index)
{
    /// <inheritdoc/>
    public override bool Undo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetColor(PageIndex, Index, Before);
    }

    /// <inheritdoc/>
    public override bool Redo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.SetColor(PageIndex, Index, After);
    }
}
