// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>
/// Changing annotations after they are placed, and undoing and redoing every change: adding, deleting, moving,
/// resizing, recolouring, restyling, note text, replies and review status. The history belongs to the open document;
/// it is kept when the document is saved and forgotten when the document is closed or reloaded.
/// </summary>
public sealed partial class AnnotationsViewModel
{
    /// <summary>The smallest side, in points, a resized annotation keeps.</summary>
    private const float MinSide = 4;

    /// <summary>The changes that can be undone and redone.</summary>
    private readonly AnnotationHistory _history = new();

    /// <summary>The editor the history belongs to.</summary>
    private IAnnotationEditor? _historyEditor;

    /// <summary>The changes of the action being made, recorded together, or <see langword="null"/>.</summary>
    private List<AnnotationChange>? _group;

    /// <summary>Gets a value indicating whether there is a change to undo.</summary>
    [Reactive]
    public partial bool CanUndo { get; private set; }

    /// <summary>Gets a value indicating whether there is a change to redo.</summary>
    [Reactive]
    public partial bool CanRedo { get; private set; }

    /// <summary>Determines whether a kind is drawn in the deeper tone of the chosen colour.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="false"/> for highlights, notes and other light marks.</returns>
    public static bool UsesDeepTone(AnnotationKind kind) =>
        kind is not (AnnotationKind.Highlight or AnnotationKind.Underline or AnnotationKind.StrikeOut or AnnotationKind.Squiggly or AnnotationKind.Note or AnnotationKind.Other);

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    public void Recolor(PageAnnotation annotation, uint color)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation.Color != color && EditorForChange()?.SetColor(annotation.PageIndex, annotation.Index, color) == true)
        {
            Record(new AnnotationRecolored(annotation.PageIndex, annotation.Index, annotation.Color, color));
        }
    }

    /// <summary>Changes the line width of a drawing, line or shape.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="width">The width in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    public bool Restyle(PageAnnotation annotation, float width)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation.LineWidth <= 0 || Math.Abs(annotation.LineWidth - width) < float.Epsilon || EditorForChange()?.SetLineWidth(annotation.PageIndex, annotation.Index, width) != true)
        {
            return false;
        }

        Record(new AnnotationLineWidthChanged(annotation.PageIndex, annotation.Index, annotation.LineWidth, width));
        return true;
    }

    /// <summary>Changes the text size of a text box or callout written here.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="fontSize">The size in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    public bool Resize(PageAnnotation annotation, float fontSize)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation.FontSize <= 0 || Math.Abs(annotation.FontSize - fontSize) < float.Epsilon || EditorForChange()?.SetFontSize(annotation.PageIndex, annotation.Index, fontSize) != true)
        {
            return false;
        }

        Record(new AnnotationFontSizeChanged(annotation.PageIndex, annotation.Index, annotation.FontSize, fontSize));
        return true;
    }

    /// <summary>
    /// Moves or resizes an annotation. Repeated small moves of the same annotation, such as arrow key presses, can be
    /// merged so one undo puts it back where it started.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="bounds">The new bounds in page space.</param>
    /// <param name="merge">Whether to merge with a move of the same annotation just before.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    public bool Move(PageAnnotation annotation, PageRect bounds, bool merge)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (!annotation.IsMovable || bounds.Width < MinSide || bounds.Height < MinSide || bounds == annotation.Bounds)
        {
            return false;
        }

        // A text box made wider or narrower wraps its text to the new width instead of stretching it.
        if (RewrapTextBox(annotation, bounds))
        {
            return true;
        }

        if (EditorForChange() is not { } editor || !editor.SetBounds(annotation.PageIndex, annotation.Index, bounds))
        {
            return false;
        }

        _history.RecordMove(annotation.PageIndex, annotation.Index, annotation.Bounds, bounds, merge);
        UpdateHistoryState();
        Edited(annotation.PageIndex);
        RefreshSelected(annotation);
        return true;
    }

    /// <summary>Deletes an annotation; Undo puts it back.</summary>
    /// <param name="annotation">The annotation.</param>
    public void Delete(PageAnnotation? annotation)
    {
        if (annotation is null || EditorForChange()?.SetRemoved(annotation.PageIndex, annotation.Index, true) != true)
        {
            return;
        }

        if (Selected is { } selected && selected.PageIndex == annotation.PageIndex && selected.Index == annotation.Index)
        {
            Selected = null;
        }

        if (ActiveBubble is { Annotation: { } bubble } && bubble.PageIndex == annotation.PageIndex && bubble.Index == annotation.Index)
        {
            CloseBubble();
        }

        Record(new AnnotationDeleted(annotation.PageIndex, annotation.Index));
    }

    /// <summary>Asks for new note text for an annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>A task.</returns>
    public async Task EditNoteAsync(PageAnnotation? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        var text = await PromptInteraction.Handle(new(
            LocalizedMessage.Get(UiText, "Edit Note"),
            LocalizedMessage.Get(UiText, "Note"),
            annotation.Contents,
            LocalizedMessage.Get(UiText, "Save Note"),
            true)).ToTask().ConfigureAwait(true);
        if (text is not null && !string.Equals(text, annotation.Contents, StringComparison.Ordinal) && EditorForChange()?.SetContents(annotation.PageIndex, annotation.Index, text) == true)
        {
            Record(new AnnotationContentsChanged(annotation.PageIndex, annotation.Index, annotation.Contents, text));
        }
    }

    /// <summary>Asks for a reply to a comment and adds it.</summary>
    /// <param name="annotation">The comment.</param>
    /// <returns>A task.</returns>
    public async Task ReplyAsync(PageAnnotation? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        var text = await PromptInteraction.Handle(new(
            LocalizedMessage.Get(UiText, ReplyKey),
            LocalizedMessage.Get(UiText, ReplyKey),
            string.Empty,
            LocalizedMessage.Get(UiText, ReplyKey),
            true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && EditorForChange() is { } editor)
        {
            _ = Added(annotation.PageIndex, editor.AddReply(annotation.PageIndex, annotation.Index, text, ReviewState.None));
        }
    }

    /// <summary>Records a review status for a comment.</summary>
    /// <param name="annotation">The comment.</param>
    /// <param name="state">The status.</param>
    /// <returns><see langword="true"/> when recorded.</returns>
    public bool SetStatus(PageAnnotation annotation, ReviewState state)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return EditorForChange() is { } editor && Added(annotation.PageIndex, editor.AddReply(annotation.PageIndex, annotation.Index, string.Empty, state));
    }

    /// <summary>
    /// Gets the editor for a change, recording the comment author on it. A different editor than last time means the
    /// document was reopened, so the old history no longer applies and is forgotten.
    /// </summary>
    /// <returns>The editor, or <see langword="null"/> when the document cannot be edited.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IAnnotationEditor? EditorForChange() => DocumentFeature<IAnnotationEditor>.Get(DocumentForChange());

    /// <summary>Gets and prepares the document used for annotation changes.</summary>
    /// <returns>The prepared document, or <see langword="null"/> when it cannot be edited.</returns>
    private IDocument? DocumentForChange()
    {
        if (_owner.TryGetDocument() is not { } document
            || DocumentFeature<IAnnotationEditor>.Get(document) is not { } editor)
        {
            return null;
        }

        _ = PrepareEditorForChange(editor);
        return document;
    }

    /// <summary>Prepares an editor and resets history when its document changes.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The prepared editor.</returns>
    private IAnnotationEditor PrepareEditorForChange(IAnnotationEditor editor)
    {
        if (!ReferenceEquals(editor, _historyEditor))
        {
            _history.Clear();
            _historyEditor = editor;
            UpdateHistoryState();
        }

        editor.Author = _owner.CommentAuthor;
        return editor;
    }

    /// <summary>Records an added annotation for undo and redraws its page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The new index, or -1.</param>
    /// <returns><see langword="true"/> when one was added.</returns>
    private bool Added(int page, int index)
    {
        if (index < 0)
        {
            return false;
        }

        Record(new AnnotationAdded(page, index));
        return true;
    }

    /// <summary>Records a change for undo, then redraws its page and refreshes the pick.</summary>
    /// <param name="change">The change.</param>
    private void Record(AnnotationChange change)
    {
        if (_group is { } group)
        {
            group.Add(change);
        }
        else
        {
            _history.Record(change);
            UpdateHistoryState();
        }

        Edited(change.PageIndex);
        if (Selected is { } selected)
        {
            RefreshSelected(selected);
        }
    }

    /// <summary>Starts recording the changes of one action together.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginGroup() => _group = [];

    /// <summary>Records the changes of the action as one step, so a single undo reverses all of them.</summary>
    private void EndGroup()
    {
        var group = _group;
        _group = null;
        if (group is not { Count: > 0 })
        {
            return;
        }

        _history.Record(group.Count == 1 ? group[0] : new AnnotationChangeGroup(group));
        UpdateHistoryState();
    }

    /// <summary>Reads the picked annotation again after a change, or drops the pick when it was removed.</summary>
    /// <param name="annotation">The annotation that was picked.</param>
    private void RefreshSelected(PageAnnotation annotation)
    {
        if (Selected is { } selected && selected.PageIndex == annotation.PageIndex && selected.Index == annotation.Index)
        {
            Selected = Find(annotation.PageIndex, annotation.Index);
        }

        RefreshBubble(annotation);
    }

    /// <summary>Shows whether there is anything to undo or redo.</summary>
    private void UpdateHistoryState()
    {
        CanUndo = _history.CanUndo;
        CanRedo = _history.CanRedo;
    }

    /// <summary>Redraws each page a change touched, and reads the pick again.</summary>
    /// <param name="change">The change undone or redone.</param>
    private void Replayed(AnnotationChange change)
    {
        var pages = new SortedSet<int>();
        change.AddPages(pages);
        foreach (var page in pages)
        {
            Edited(page);
        }

        if (Selected is { } selected)
        {
            RefreshSelected(selected);
        }
    }

    /// <summary>Undoes the latest annotation change.</summary>
    [ReactiveCommand]
    private void Undo()
    {
        if (EditorForChange() is { } editor && _history.Undo(editor) is { } change)
        {
            Replayed(change);
        }

        UpdateHistoryState();
    }

    /// <summary>Makes the latest undone annotation change again.</summary>
    [ReactiveCommand]
    private void Redo()
    {
        if (EditorForChange() is { } editor && _history.Redo(editor) is { } change)
        {
            Replayed(change);
        }

        UpdateHistoryState();
    }

    /// <summary>Deletes an annotation; null deletes the selected one.</summary>
    /// <param name="item">The sidebar item.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Delete(AnnotationItemViewModel? item) => Delete(item?.Annotation ?? Selected);

    /// <summary>Edits an annotation's note; null edits the selected one.</summary>
    /// <param name="item">The sidebar item.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task EditNote(AnnotationItemViewModel? item) => EditNoteAsync(item?.Annotation ?? Selected);

    /// <summary>Asks for a reply to an annotation; null replies to the selected one.</summary>
    /// <param name="item">The sidebar item.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task Reply(AnnotationItemViewModel? item) => ReplyAsync(item?.Annotation ?? Selected);

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="choice">The annotation and its new colour.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecolorAnnotation(AnnotationColorChoice choice) => Recolor(choice.Annotation, choice.Color);

    /// <summary>Records a review status for a comment.</summary>
    /// <param name="choice">The comment and its status.</param>
    /// <returns><see langword="true"/> when recorded.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SetAnnotationStatus(AnnotationStatusChoice choice) => SetStatus(choice.Annotation, choice.State);

    /// <summary>Chooses a colour by its name, such as "Yellow" or "Dark blue", and gives it to the picked annotation.</summary>
    /// <param name="name">The name.</param>
    [ReactiveCommand]
    private void SetColor(string name)
    {
        foreach (var (colorName, color) in AnnotationColors.All)
        {
            if (!string.Equals(UiText.Get(colorName), name, StringComparison.Ordinal))
            {
                continue;
            }

            Color = color;
            if (Selected is { } selected)
            {
                // Lines, shapes, drawings, text and stamps use the deeper tone, as they do when made.
                Recolor(selected, UsesDeepTone(selected.Kind) ? AnnotationColors.Deep(color) : color);
            }

            return;
        }
    }

    /// <summary>Chooses a line width by its name, such as "Thick", and gives it to the picked annotation.</summary>
    /// <param name="name">The name.</param>
    [ReactiveCommand]
    private void SetLineWidth(string name)
    {
        foreach (var (widthName, width) in LineWidths)
        {
            if (!string.Equals(widthName, name, StringComparison.Ordinal))
            {
                continue;
            }

            LineWidth = width;
            _ = Selected is { } selected && Restyle(selected, width);
            return;
        }
    }

    /// <summary>Chooses a text size by its name, such as "Large", and gives it to the picked text box or callout.</summary>
    /// <param name="name">The name.</param>
    [ReactiveCommand]
    private void SetFontSize(string name)
    {
        foreach (var (sizeName, size) in FontSizes)
        {
            if (!string.Equals(sizeName, name, StringComparison.Ordinal))
            {
                continue;
            }

            FontSize = size;
            _ = Selected is { } selected && Resize(selected, size);
            return;
        }
    }
}
