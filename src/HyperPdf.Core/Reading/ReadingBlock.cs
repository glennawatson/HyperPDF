// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>A block of text in reading order: its kind, its text joined into one line, and where each character came from.</summary>
/// <param name="Kind">What the block is.</param>
/// <param name="Text">The text, with lines joined and words broken over lines mended.</param>
/// <param name="CharIndices">The page character index of each character of <paramref name="Text"/>; -1 for joining spaces.</param>
/// <param name="Bounds">The block's box on the page.</param>
/// <param name="FontSize">The block's main font size in points.</param>
[DebuggerDisplay("ReadingBlock: {Kind}: {Text}")]
public sealed record ReadingBlock(ReadingBlockKind Kind, string Text, int[] CharIndices, PageRect Bounds, float FontSize)
{
    /// <summary>Gets the heading level, 1 to 6, for a heading; zero otherwise.</summary>
    public int Level { get; init; }

    /// <summary>Gets the table row of a table cell, from 1; zero otherwise.</summary>
    public int Row { get; init; }

    /// <summary>Gets the table column of a table cell, from 1; zero otherwise.</summary>
    public int Column { get; init; }

    /// <summary>Gets a value indicating whether the block comes from the document's own structure tags rather than its layout.</summary>
    public bool IsTagged { get; init; }

    /// <summary>Gets the language the block's tags ask for when it differs from the surrounding text, for example "fr-FR"; <see langword="null"/> otherwise.</summary>
    public string? Language { get; init; }
}
