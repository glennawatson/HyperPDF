// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>One tag of the document's structure, listed in reading order.</summary>
/// <param name="Id">The tag's object number; 0 when the file writes it inside its parent, which makes it read-only.</param>
/// <param name="Type">The type as the file spells it, such as <c>H1</c>.</param>
/// <param name="Depth">How deep the tag is: 0 for a top-level tag.</param>
/// <param name="PageIndex">The zero based page the tag's content is on; -1 when unknown.</param>
/// <param name="AlternateText">The description for a screen reader, or <see langword="null"/>.</param>
/// <param name="ActualText">The text that replaces the content, or <see langword="null"/>.</param>
/// <param name="Expansion">The words an abbreviation stands for, or <see langword="null"/>.</param>
/// <param name="Title">The tag's title, or <see langword="null"/>.</param>
/// <param name="Language">The language the tag sets for itself, or <see langword="null"/> when it inherits one.</param>
/// <param name="ContentCount">How many pieces of page content or objects the tag holds directly.</param>
/// <param name="ChildTagCount">How many tags are directly inside this one.</param>
/// <param name="IsHeaderCell">Whether the tag is a table header cell, which can have a scope.</param>
/// <param name="Scope">Which cells a header cell labels; <see cref="DocumentTableScope.None"/> for any other tag.</param>
/// <param name="IsCell">Whether the tag is a table cell (a header or data cell), which can cover several rows and columns.</param>
/// <param name="RowSpan">How many rows a cell covers, at least 1.</param>
/// <param name="ColumnSpan">How many columns a cell covers, at least 1.</param>
/// <param name="IsList">Whether the tag is a list, which can have a numbering.</param>
/// <param name="ListNumbering">How a list's items are labelled, such as <c>Decimal</c>; empty when not given.</param>
/// <param name="Placement">How the tag sits in the flow of content, such as <c>Block</c> or <c>Inline</c>; empty when not given.</param>
[DebuggerDisplay("DocumentTag: {Type} depth {Depth} page {PageIndex}")]
public sealed record DocumentTag(
    int Id,
    string Type,
    int Depth,
    int PageIndex,
    string? AlternateText,
    string? ActualText,
    string? Expansion,
    string? Title,
    string? Language,
    int ContentCount,
    int ChildTagCount,
    bool IsHeaderCell,
    DocumentTableScope Scope,
    bool IsCell,
    int RowSpan,
    int ColumnSpan,
    bool IsList,
    string ListNumbering,
    string Placement)
{
    /// <summary>Gets a value indicating whether the tag can be changed.</summary>
    public bool CanEdit => Id > 0;

    /// <summary>Gets a value indicating whether the tag can be removed without losing page content.</summary>
    public bool CanRemove => Id > 0 && ContentCount == 0;
}
