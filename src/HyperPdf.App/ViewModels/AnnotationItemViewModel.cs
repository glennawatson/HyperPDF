// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using HyperPdf.Core.Annotations;

namespace HyperPdf.App.ViewModels;

/// <summary>An annotation listed in the sidebar.</summary>
/// <param name="Annotation">The annotation.</param>
/// <param name="PageLabel">The page label shown, for example "iv" or "12".</param>
/// <param name="Replies">The replies to it and its review status changes, oldest first.</param>
[DebuggerDisplay("AnnotationItemViewModel: {Summary}")]
public sealed record AnnotationItemViewModel(PageAnnotation Annotation, string PageLabel, IReadOnlyList<AnnotationReply> Replies)
{
    /// <summary>Gets the kind in words, so colour is never the only cue.</summary>
    public string KindName => AnnotationNames.Get(Annotation.Kind);

    /// <summary>Gets the line shown under the kind: the note, or the page.</summary>
    public string Summary => Annotation.Contents.Length > 0 ? Annotation.Contents : string.Create(CultureInfo.CurrentCulture, $"Page {PageLabel}");

    /// <summary>Gets the review status: the latest one recorded, or none.</summary>
    public ReviewState Status
    {
        get
        {
            for (var i = Replies.Count - 1; i >= 0; i--)
            {
                if (Replies[i].State != ReviewState.None)
                {
                    return Replies[i].State;
                }
            }

            return ReviewState.None;
        }
    }

    /// <summary>Gets the replies as lines of "author: text", without bare status changes.</summary>
    public string RepliesText
    {
        get
        {
            var text = new StringBuilder();
            foreach (var reply in Replies)
            {
                AppendReply(text, reply);
            }

            return text.ToString();
        }
    }

    /// <summary>Gets the line above the summary: kind, page and any review status.</summary>
    public string Heading => Status == ReviewState.None
        ? string.Create(CultureInfo.CurrentCulture, $"{KindName} · {PageCaption}")
        : string.Create(CultureInfo.CurrentCulture, $"{KindName} · {PageCaption} · {Status}");

    /// <summary>Gets the page caption.</summary>
    public string PageCaption => string.Create(CultureInfo.CurrentCulture, $"Page {PageLabel}");

    /// <summary>Gets the colour in words, so the sidebar's colour swatch is never the only cue.</summary>
    public string ColorName => AnnotationNames.GetColor(Annotation.Color);

    /// <summary>Gets who wrote the annotation, or "Unknown author" when the file does not say.</summary>
    public string AuthorName => Annotation.Author.Length > 0 ? Annotation.Author : "Unknown author";

    /// <summary>Gets the line naming the author and, when known, when it was last changed.</summary>
    public string Details => Annotation.Modified is { } modified
        ? string.Create(CultureInfo.CurrentCulture, $"{AuthorName} · {modified.ToLocalTime():g}")
        : AuthorName;

    /// <summary>Gets the words the list's text filter looks through: the kind, note, replies and author.</summary>
    public string SearchText => field ??= string.Create(CultureInfo.CurrentCulture, $"{KindName}\n{Annotation.Contents}\n{RepliesText}\n{AuthorName}");

    /// <summary>Gets the item as a screen reader says it: kind, page, status, colour, note and author.</summary>
    public string SpokenText => Annotation.Contents.Length > 0
        ? string.Create(CultureInfo.CurrentCulture, $"{Heading}, {ColorName}: {Annotation.Contents}. {Details}")
        : string.Create(CultureInfo.CurrentCulture, $"{Heading}, {ColorName}. {Details}");

    /// <summary>Determines whether another item has the same annotation, page label and replies; the cached filter text is not compared.</summary>
    /// <param name="other">The other item.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    public bool Equals(AnnotationItemViewModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other)
            || (Annotation.Equals(other.Annotation)
                && string.Equals(PageLabel, other.PageLabel, StringComparison.Ordinal)
                && ReferenceEquals(Replies, other.Replies));
    }

    /// <summary>Gets a hash code from the annotation, page label and replies.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(Annotation, PageLabel, Replies);

    /// <summary>Appends one reply as "author: text"; bare status changes are left out.</summary>
    /// <param name="text">The text so far.</param>
    /// <param name="reply">The reply.</param>
    private static void AppendReply(StringBuilder text, AnnotationReply reply)
    {
        if (reply.Contents.Length == 0)
        {
            return;
        }

        _ = text.Append(text.Length > 0 ? "\n" : string.Empty).Append(reply.Author.Length > 0 ? reply.Author : "Reply").Append(": ").Append(reply.Contents);
    }
}
