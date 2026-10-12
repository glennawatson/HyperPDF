// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>
/// One reversible change to an annotation, kept for undo and redo. Annotations keep their index while a document is
/// open (removing only hides them until the file is saved), so a change can name its annotation by page and index.
/// </summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation index.</param>
[DebuggerDisplay("AnnotationChange: page {PageIndex}, annotation {Index}")]
public abstract record AnnotationChange(int PageIndex, int Index)
{
    /// <summary>Reverses the change.</summary>
    /// <param name="editor">The document's editor.</param>
    /// <returns><see langword="true"/> when reversed.</returns>
    public abstract bool Undo(IAnnotationEditor editor);

    /// <summary>Makes the change again.</summary>
    /// <param name="editor">The document's editor.</param>
    /// <returns><see langword="true"/> when made.</returns>
    public abstract bool Redo(IAnnotationEditor editor);

    /// <summary>Appends the page of each annotation the change touches.</summary>
    /// <param name="pages">The set receiving the pages.</param>
    public virtual void AddPages(ISet<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        _ = pages.Add(PageIndex);
    }
}
