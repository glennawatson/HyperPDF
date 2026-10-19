// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>Page text folded for searching.</summary>
/// <param name="Text">The folded text.</param>
/// <param name="Map">
/// The page text index of each folded character, or <see langword="null"/> when each folded character came from the source
/// character at its own index. A source character that decomposed gives several entries with its index.
/// </param>
/// <param name="SourceLength">The length of the page text the folded text came from.</param>
[DebuggerDisplay("FoldedText: {Text.Length} chars")]
internal sealed record FoldedText(string Text, int[]? Map, int SourceLength)
{
    /// <summary>Initializes a new instance of the <see cref="FoldedText"/> class for text that kept its length.</summary>
    /// <param name="text">The folded text.</param>
    /// <param name="map">The page text index of each folded character, or <see langword="null"/>.</param>
    internal FoldedText(string text, int[]? map)
        : this(text, map, text.Length)
    {
    }

    /// <summary>Gets the page text index of the first character of a folded character.</summary>
    /// <param name="index">The folded index.</param>
    /// <returns>The page text index.</returns>
    internal int StartOf(int index) => Map is null ? index : Map[index];

    /// <summary>
    /// Gets the page text index of the last character that belongs to a folded character: the one before the next folded
    /// character's source, so combining marks dropped after a letter are part of it.
    /// </summary>
    /// <param name="index">The folded index.</param>
    /// <returns>The page text index.</returns>
    internal int EndOf(int index)
    {
        if (Map is null)
        {
            return index;
        }

        var next = index + 1 < Map.Length ? Map[index + 1] : SourceLength;
        return Math.Max(Map[index], next - 1);
    }
}
