// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>Several changes made by one action, such as marking text over several pages, undone and redone together.</summary>
/// <param name="Changes">The changes in the order they were made.</param>
[DebuggerDisplay("AnnotationChangeGroup: {Changes.Count} changes")]
public sealed record AnnotationChangeGroup(IReadOnlyList<AnnotationChange> Changes) : AnnotationChange(Changes[0].PageIndex, Changes[0].Index)
{
    /// <inheritdoc/>
    public override bool Undo(IAnnotationEditor editor)
    {
        var any = false;
        for (var i = Changes.Count - 1; i >= 0; i--)
        {
            any |= Changes[i].Undo(editor);
        }

        return any;
    }

    /// <inheritdoc/>
    public override bool Redo(IAnnotationEditor editor)
    {
        var any = false;
        foreach (var change in Changes)
        {
            any |= change.Redo(editor);
        }

        return any;
    }

    /// <inheritdoc/>
    public override void AddPages(ISet<int> pages)
    {
        foreach (var change in Changes)
        {
            change.AddPages(pages);
        }
    }
}
