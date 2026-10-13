// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Where a bookmark leads, as it is written: a page position, a named destination, a web address or a viewer command.</summary>
/// <param name="Kind">What the bookmark does.</param>
/// <param name="Destination">The page position, for <see cref="PdfBookmarkTargetKind.Page"/>.</param>
/// <param name="Text">The destination name, web address or command name, for the other kinds.</param>
[DebuggerDisplay("PdfBookmarkTarget: {Kind} {Text}")]
public sealed record PdfBookmarkTarget(PdfBookmarkTargetKind Kind, PdfExplicitDestination? Destination, string? Text)
{
    /// <summary>Gets a target that leads nowhere.</summary>
    public static PdfBookmarkTarget Nowhere { get; } = new(PdfBookmarkTargetKind.None, null, null);

    /// <summary>Makes a target at a position on a page.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The target.</returns>
    public static PdfBookmarkTarget ToPage(PdfExplicitDestination destination) => new(PdfBookmarkTargetKind.Page, destination, null);

    /// <summary>Makes a target at a named destination.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The target.</returns>
    public static PdfBookmarkTarget ToNamedDestination(string name) => new(PdfBookmarkTargetKind.NamedDestination, null, name);

    /// <summary>Makes a target that opens a web address.</summary>
    /// <param name="uri">The address.</param>
    /// <returns>The target.</returns>
    public static PdfBookmarkTarget ToUri(string uri) => new(PdfBookmarkTargetKind.Uri, null, uri);

    /// <summary>Makes a target that runs a standard viewer command.</summary>
    /// <param name="name">The command: <c>NextPage</c>, <c>PrevPage</c>, <c>FirstPage</c> or <c>LastPage</c>.</param>
    /// <returns>The target.</returns>
    public static PdfBookmarkTarget ToNamedAction(string name) => new(PdfBookmarkTargetKind.NamedAction, null, name);
}
