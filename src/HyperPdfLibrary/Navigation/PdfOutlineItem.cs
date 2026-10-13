// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>An outline (bookmark) entry.</summary>
/// <param name="Title">The title.</param>
/// <param name="Action">Where the entry leads.</param>
/// <param name="Children">The entries below it.</param>
/// <param name="IsOpen">Whether the document asks for the entry to start expanded.</param>
[DebuggerDisplay("PdfOutlineItem: {Title}")]
public sealed record PdfOutlineItem(string Title, PdfAction Action, PdfOutlineItem[] Children, bool IsOpen)
{
    /// <summary>Gets the entry's object, which the outline editing methods take to name the entry.</summary>
    public PdfObjectId Id { get; init; }

    /// <summary>Gets the colour and style flags of the title, or <see langword="null"/> when the entry sets neither.</summary>
    public PdfBookmarkStyle? Style { get; init; }
}
