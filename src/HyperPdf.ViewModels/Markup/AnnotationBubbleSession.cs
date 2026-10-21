// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>
/// An open comment bubble on the page: either an existing annotation's note and replies, or a new sticky note being placed.
/// </summary>
/// <param name="PageIndex">The zero-based page index.</param>
/// <param name="Bounds">The annotation or note bounds in page space.</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
/// <param name="Heading">The heading naming the kind, page and author.</param>
/// <param name="RepliesText">The existing replies formatted as lines of "author: text", or empty.</param>
/// <param name="Annotation">The existing annotation being viewed or edited, or <see langword="null"/> when placing a new note.</param>
/// <param name="NewNoteLocation">The top-left corner for a new note, or <see langword="null"/> for an existing annotation.</param>
[DebuggerDisplay("AnnotationBubbleSession: Page {PageIndex}, {Heading}")]
public sealed record AnnotationBubbleSession(
    int PageIndex,
    PageRect Bounds,
    uint Color,
    string Heading,
    string RepliesText,
    PageAnnotation? Annotation,
    PagePoint? NewNoteLocation)
{
    /// <summary>Gets a value indicating whether this bubble is for an existing annotation that can be replied to or deleted.</summary>
    public bool IsExisting => Annotation is not null;

    /// <summary>Gets a value indicating whether there are existing replies to show.</summary>
    public bool HasReplies => RepliesText.Length > 0;
}
