// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>
/// The undo and redo history of one open document's annotation changes. A new change clears what could be redone. The
/// history belongs to one editor: it is cleared when the document is closed or reloaded, and kept when it is saved,
/// because saving leaves removed annotations in memory (hidden) so their indexes stay the same.
/// </summary>
[DebuggerDisplay("AnnotationHistory: {_undo.Count} to undo, {_redo.Count} to redo")]
public sealed class AnnotationHistory
{
    /// <summary>The most changes kept; the oldest are forgotten first.</summary>
    private const int MaxChanges = 200;

    /// <summary>The changes that can be undone, oldest first.</summary>
    private readonly List<AnnotationChange> _undo = [];

    /// <summary>The changes that can be redone, most recently undone last.</summary>
    private readonly List<AnnotationChange> _redo = [];

    /// <summary>Gets the most changes kept; the oldest are forgotten first.</summary>
    public static int Capacity => MaxChanges;

    /// <summary>Gets a value indicating whether there is a change to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets a value indicating whether there is a change to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Gets the number of changes that can be undone.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Gets the number of changes that can be redone.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>Records a change that was just made, and forgets what could be redone.</summary>
    /// <param name="change">The change.</param>
    public void Record(AnnotationChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _redo.Clear();
        if (_undo.Count == MaxChanges)
        {
            _undo.RemoveAt(0);
        }

        _undo.Add(change);
    }

    /// <summary>
    /// Records a move. When <paramref name="merge"/> is set and the last change moved the same annotation, as repeated
    /// arrow key presses do, the two become one change, so a single undo puts the annotation back where it started.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="before">The bounds before.</param>
    /// <param name="after">The bounds after.</param>
    /// <param name="merge">Whether to merge with a move of the same annotation just before.</param>
    public void RecordMove(int pageIndex, int index, PageRect before, PageRect after, bool merge)
    {
        // Only nudges join together: a nudge after a drag or a resize is its own step, so Undo reverses them one at a time.
        if (merge && _redo.Count == 0 && _undo.Count > 0 && _undo[^1] is AnnotationMoved { IsNudge: true } last && last.PageIndex == pageIndex && last.Index == index)
        {
            _undo[^1] = last with { After = after };
            return;
        }

        Record(new AnnotationMoved(pageIndex, index, before, after) { IsNudge = merge });
    }

    /// <summary>Undoes the latest change.</summary>
    /// <param name="editor">The document's editor.</param>
    /// <returns>The change undone, or <see langword="null"/> when there was none or it could not be undone.</returns>
    public AnnotationChange? Undo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (_undo.Count == 0)
        {
            return null;
        }

        var change = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        if (!change.Undo(editor))
        {
            return null;
        }

        _redo.Add(change);
        return change;
    }

    /// <summary>Makes the latest undone change again.</summary>
    /// <param name="editor">The document's editor.</param>
    /// <returns>The change redone, or <see langword="null"/> when there was none or it could not be made.</returns>
    public AnnotationChange? Redo(IAnnotationEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (_redo.Count == 0)
        {
            return null;
        }

        var change = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        if (!change.Redo(editor))
        {
            return null;
        }

        _undo.Add(change);
        return change;
    }

    /// <summary>Forgets every change, for example when the document is reloaded.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
