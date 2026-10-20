// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>
/// Folded page text held in caller-owned buffers, so a search can fold and scan a page without allocating a string or a map
/// for it. It reads like <see cref="FoldedText"/>.
/// </summary>
/// <param name="text">The folded characters.</param>
/// <param name="map">The page text index of each folded character, or empty when folded characters keep their indexes.</param>
/// <param name="sourceLength">The length of the page text before folding.</param>
internal readonly ref struct FoldedView(ReadOnlySpan<char> text, ReadOnlySpan<int> map, int sourceLength)
{
    /// <summary>Gets the folded characters.</summary>
    internal ReadOnlySpan<char> Text { get; } = text;

    /// <summary>Gets the page text index of each folded character, or empty when folded characters keep their indexes.</summary>
    internal ReadOnlySpan<int> Map { get; } = map;

    /// <summary>Gets the length of the page text before folding.</summary>
    internal int SourceLength { get; } = sourceLength;

    /// <summary>Gets the page text index of the first character of a folded character.</summary>
    /// <param name="index">The folded index.</param>
    /// <returns>The page text index.</returns>
    internal int StartOf(int index) => Map.IsEmpty ? index : Map[index];

    /// <summary>Gets the page text index of the last character that belongs to a folded character.</summary>
    /// <param name="index">The folded index.</param>
    /// <returns>The page text index.</returns>
    internal int EndOf(int index)
    {
        if (Map.IsEmpty)
        {
            return index;
        }

        var next = index + 1 < Map.Length ? Map[index + 1] : SourceLength;
        return Math.Max(Map[index], next - 1);
    }
}
