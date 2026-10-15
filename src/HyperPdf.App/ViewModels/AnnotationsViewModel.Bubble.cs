// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// On-page comment bubble state: opens beside a sticky note or commented annotation when clicked on the page so the
/// reader can read, edit, reply to or delete the comment without leaving the page.
/// </summary>
public sealed partial class AnnotationsViewModel
{
    /// <summary>The side of a new sticky note's icon in points.</summary>
    private const float NoteIconSide = 20;

    /// <summary>The side of a comment indicator badge in points.</summary>
    private const float CommentBadgeSide = 14;

    /// <summary>How far a comment indicator badge overlaps the top-right corner of its annotation, in points.</summary>
    private const float CommentBadgeOverlap = 6;

    /// <summary>Gets the comment bubble currently open on the page, or <see langword="null"/> when none is open.</summary>
    [Reactive(
        nameof(HasBubble),
        nameof(IsExistingBubble),
        nameof(CanReplyInBubble),
        nameof(ShowBubbleReplies))]
    public partial AnnotationBubbleSession? ActiveBubble { get; private set; }

    /// <summary>Gets a value indicating whether a comment bubble is open on the page.</summary>
    public bool HasBubble => ActiveBubble is not null;

    /// <summary>Gets a value indicating whether the open bubble belongs to an existing annotation.</summary>
    public bool IsExistingBubble => ActiveBubble is { IsExisting: true };

    /// <summary>Gets a value indicating whether the Reply button is shown in the open bubble.</summary>
    public bool CanReplyInBubble => ActiveBubble is { IsExisting: true } && !IsReplyingInBubble;

    /// <summary>Gets a value indicating whether existing replies are shown in the open bubble.</summary>
    public bool ShowBubbleReplies => ActiveBubble is { HasReplies: true };

    /// <summary>Gets or sets the note text being edited in the open bubble.</summary>
    [Reactive]
    public partial string BubbleNoteText { get; set; } = string.Empty;

    /// <summary>Gets or sets the reply text being typed in the open bubble.</summary>
    [Reactive]
    public partial string BubbleReplyText { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the reply box is open inside the bubble.</summary>
    [Reactive(nameof(CanReplyInBubble))]
    public partial bool IsReplyingInBubble { get; set; }

    /// <summary>Determines whether an annotation can open an on-page comment bubble.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it can hold a note and replies.</returns>
    public static bool CanShowBubble(PageAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.Kind is not (AnnotationKind.TextBox or AnnotationKind.Signature or AnnotationKind.Redaction);
    }

    /// <summary>Determines whether a non-note annotation has a comment that should show a speech-bubble badge on the page.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when a badge should be drawn at its corner.</returns>
    public static bool HasCommentBadge(PageAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.Contents.Length > 0
            && annotation.Kind is not (AnnotationKind.Note or AnnotationKind.TextBox or AnnotationKind.Callout or AnnotationKind.Stamp or AnnotationKind.Signature or AnnotationKind.Redaction);
    }

    /// <summary>Gets the page-space bounds of the speech-bubble badge drawn at the top-right corner of a commented annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The badge rectangle in page space.</returns>
    public static PageRect CommentBadgeBounds(PageAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var bounds = annotation.Bounds;
        return new(bounds.Right - CommentBadgeOverlap, Math.Max(0, bounds.Top - CommentBadgeOverlap), CommentBadgeSide, CommentBadgeSide);
    }

    /// <summary>Appends the annotations on a page for drawing comment badges.</summary>
    /// <param name="page">The zero-based page index.</param>
    /// <param name="output">Receives the annotations on the page.</param>
    public void GetPageAnnotations(int page, List<PageAnnotation> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        if (Editor is { } editor && (uint)page < (uint)_owner.PageCount)
        {
            editor.GetAnnotations(page, output);
        }
    }

    /// <summary>Finds a commented annotation whose speech-bubble badge is under a point, topmost first.</summary>
    /// <param name="page">The zero-based page index.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The annotation whose badge was hit, or <see langword="null"/>.</returns>
    public PageAnnotation? HitTestCommentBadge(int page, PagePoint point)
    {
        if (Editor is not { } editor)
        {
            return null;
        }

        _scratch.Clear();
        editor.GetAnnotations(page, _scratch);
        for (var i = _scratch.Count - 1; i >= 0; i--)
        {
            var candidate = _scratch[i];
            if (!HasCommentBadge(candidate))
            {
                continue;
            }

            var badge = CommentBadgeBounds(candidate);
            var grown = PageRect.FromEdges(badge.Left - HitTolerance, badge.Top - HitTolerance, badge.Right + HitTolerance, badge.Bottom + HitTolerance);
            if (grown.Contains(point))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Opens the on-page comment bubble for an existing annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when the bubble opened.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OpenBubble(PageAnnotation? annotation) => OpenBubble(annotation, false);

    /// <summary>Opens the on-page comment bubble for an existing annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="startReply">Whether to open the reply box straight away.</param>
    /// <returns><see langword="true"/> when the bubble opened.</returns>
    public bool OpenBubble(PageAnnotation? annotation, bool startReply)
    {
        if (annotation is null || !CanShowBubble(annotation) || Editor is not { } editor)
        {
            return false;
        }

        _ = CommitText();
        Selected = annotation;
        var item = CreateItem(editor, annotation, _owner.GetPageDisplay(annotation.PageIndex));
        var heading = string.Create(CultureInfo.CurrentCulture, $"{item.Heading} · {item.AuthorName}");
        BubbleNoteText = annotation.Contents;
        BubbleReplyText = string.Empty;
        IsReplyingInBubble = startReply;
        ActiveBubble = new(annotation.PageIndex, annotation.Bounds, annotation.Color, heading, item.RepliesText, annotation, null);
        return true;
    }

    /// <summary>Opens the on-page comment bubble to place a new sticky note at a point on a page.</summary>
    /// <param name="page">The zero-based page index.</param>
    /// <param name="location">Where the note icon goes, in page space.</param>
    /// <returns><see langword="true"/> when the bubble opened.</returns>
    public bool BeginNoteBubble(int page, PagePoint location)
    {
        if (!CanAnnotate || (uint)page >= (uint)_owner.PageCount)
        {
            return false;
        }

        _ = CommitText();
        var bounds = new PageRect(location.X, location.Y, NoteIconSide, NoteIconSide);
        var heading = string.Create(CultureInfo.CurrentCulture, $"New Note · Page {_owner.GetPageDisplay(page)}");
        BubbleNoteText = string.Empty;
        BubbleReplyText = string.Empty;
        IsReplyingInBubble = false;
        ActiveBubble = new(page, bounds, Color, heading, string.Empty, null, location);
        return true;
    }

    /// <summary>Closes the open comment bubble without saving unkept edits.</summary>
    [ReactiveCommand]
    public void CloseBubble()
    {
        if (ActiveBubble is null)
        {
            return;
        }

        ActiveBubble = null;
        IsReplyingInBubble = false;
        BubbleNoteText = string.Empty;
        BubbleReplyText = string.Empty;
    }

    /// <summary>Saves the note text (and any typed reply) in the open bubble.</summary>
    /// <returns><see langword="true"/> when saved.</returns>
    [ReactiveCommand]
    public bool SaveBubble()
    {
        if (ActiveBubble is not { } bubble)
        {
            return false;
        }

        if (IsReplyingInBubble && !string.IsNullOrWhiteSpace(BubbleReplyText))
        {
            return SendBubbleReply();
        }

        if (bubble.NewNoteLocation is { } location)
        {
            return SaveNewNoteBubble(bubble.PageIndex, location);
        }

        if (bubble.Annotation is { } annotation)
        {
            SaveExistingBubbleNote(annotation);
            CloseBubble();
            return true;
        }

        return false;
    }

    /// <summary>Shows the reply box inside the open bubble.</summary>
    [ReactiveCommand]
    public void StartBubbleReply()
    {
        if (ActiveBubble is { IsExisting: true })
        {
            IsReplyingInBubble = true;
        }
    }

    /// <summary>Adds the typed reply to the open bubble's annotation and refreshes the bubble.</summary>
    /// <returns><see langword="true"/> when the reply was added.</returns>
    [ReactiveCommand]
    public bool SendBubbleReply()
    {
        if (ActiveBubble is not { Annotation: { } annotation } || string.IsNullOrWhiteSpace(BubbleReplyText) || EditorForChange() is not { } editor)
        {
            return false;
        }

        SaveExistingBubbleNote(annotation);
        var replyText = BubbleReplyText.Trim();
        if (!Added(annotation.PageIndex, editor.AddReply(annotation.PageIndex, annotation.Index, replyText, ReviewState.None)))
        {
            return false;
        }

        if (Find(annotation.PageIndex, annotation.Index) is { } updated)
        {
            _ = OpenBubble(updated, false);
        }
        else
        {
            CloseBubble();
        }

        return true;
    }

    /// <summary>Deletes the annotation shown in the open bubble and closes the bubble.</summary>
    [ReactiveCommand]
    public void DeleteBubbleAnnotation()
    {
        if (ActiveBubble is not { Annotation: { } annotation })
        {
            return;
        }

        CloseBubble();
        Delete(annotation);
    }

    /// <summary>Refreshes the open bubble when its annotation changed on the page.</summary>
    /// <param name="annotation">The annotation before the change.</param>
    private void RefreshBubble(PageAnnotation annotation)
    {
        if (ActiveBubble is not { Annotation: { } current } || current.PageIndex != annotation.PageIndex || current.Index != annotation.Index)
        {
            return;
        }

        if (Find(annotation.PageIndex, annotation.Index) is { } updated && Editor is { } editor)
        {
            var item = CreateItem(editor, updated, _owner.GetPageDisplay(updated.PageIndex));
            var heading = string.Create(CultureInfo.CurrentCulture, $"{item.Heading} · {item.AuthorName}");
            ActiveBubble = new(updated.PageIndex, updated.Bounds, updated.Color, heading, item.RepliesText, updated, null);
        }
        else
        {
            CloseBubble();
        }
    }

    /// <summary>Places a new sticky note from the open bubble and closes it.</summary>
    /// <param name="page">The page index.</param>
    /// <param name="location">The note location.</param>
    /// <returns><see langword="true"/> when handled.</returns>
    private bool SaveNewNoteBubble(int page, PagePoint location)
    {
        var text = BubbleNoteText.Trim();
        CloseBubble();
        if (text.Length == 0 || EditorForChange() is not { } editor)
        {
            return false;
        }

        var index = editor.AddNote(page, location, text, Color);
        if (!Added(page, index))
        {
            return false;
        }

        Selected = Find(page, index);
        return true;
    }

    /// <summary>Saves changed note text onto an existing annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    private void SaveExistingBubbleNote(PageAnnotation annotation)
    {
        if (!string.Equals(BubbleNoteText, annotation.Contents, StringComparison.Ordinal)
            && EditorForChange()?.SetContents(annotation.PageIndex, annotation.Index, BubbleNoteText) == true)
        {
            Record(new AnnotationContentsChanged(annotation.PageIndex, annotation.Index, annotation.Contents, BubbleNoteText));
        }
    }

    /// <summary>Opens the on-page bubble for an annotation; null opens for the selected one.</summary>
    /// <param name="item">The sidebar item, or <see langword="null"/>.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OpenBubbleFor(AnnotationItemViewModel? item) => _ = OpenBubble(item?.Annotation ?? Selected, false);

    /// <summary>Opens the on-page bubble with its reply box ready; null opens for the selected annotation.</summary>
    /// <param name="item">The sidebar item, or <see langword="null"/>.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReplyInBubbleFor(AnnotationItemViewModel? item) => _ = OpenBubble(item?.Annotation ?? Selected, true);

    /// <summary>Opens the on-page bubble to add a note where the page was clicked.</summary>
    /// <param name="location">The page and point.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginNoteBubbleHere(PageLocation location) => _ = BeginNoteBubble(location.Page, location.Point);
}
