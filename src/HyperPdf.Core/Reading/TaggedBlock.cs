// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>A block-level element of a tagged PDF's structure tree, in the document's logical order.</summary>
/// <param name="Kind">What the element is.</param>
/// <param name="Level">The heading level, 1 to 6, for a heading; zero otherwise.</param>
/// <param name="Characters">The page characters the element covers, in logical order.</param>
/// <param name="ReplacementText">The element's <c>/ActualText</c>, or a figure's <c>/Alt</c> text, read instead of its characters.</param>
[DebuggerDisplay("TaggedBlock: {Kind} {Level}: {Characters.Length} characters")]
public sealed record TaggedBlock(ReadingBlockKind Kind, int Level, int[] Characters, string? ReplacementText)
{
    /// <summary>Gets the table row of a table cell, from 1; zero when the block is not a table cell or its row is not known.</summary>
    public int Row { get; init; }

    /// <summary>Gets the table column of a table cell, from 1; zero when the block is not a table cell or its column is not known.</summary>
    public int Column { get; init; }
}
