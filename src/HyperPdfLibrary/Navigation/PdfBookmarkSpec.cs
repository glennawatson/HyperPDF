// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>A bookmark to add, with the bookmarks below it.</summary>
/// <param name="Title">The title.</param>
/// <param name="Target">Where the bookmark leads.</param>
[DebuggerDisplay("PdfBookmarkSpec: {Title}")]
public sealed record PdfBookmarkSpec(string Title, PdfBookmarkTarget Target)
{
    /// <summary>Gets the style, or <see langword="null"/> for the viewer's default.</summary>
    public PdfBookmarkStyle? Style { get; init; }

    /// <summary>Gets a value indicating whether the bookmark starts expanded to show the bookmarks below it.</summary>
    public bool IsOpen { get; init; }

    /// <summary>Gets the bookmarks below this one.</summary>
    public IReadOnlyList<PdfBookmarkSpec> Children { get; init; } = [];

    /// <summary>Gets the structure element the bookmark stands for (<c>/SE</c>), or an invalid id for none.</summary>
    public PdfObjectId StructureElement { get; init; }
}
