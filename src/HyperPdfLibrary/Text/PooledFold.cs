// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Text;

/// <summary>Folded page text in pooled buffers. Dispose it to return the buffers.</summary>
/// <param name="Chars">The rented character buffer.</param>
/// <param name="Length">The number of folded characters in it.</param>
/// <param name="Map">The rented map from folded index to page text index, or <see langword="null"/> when there is none.</param>
/// <param name="SourceLength">The length of the page text before folding.</param>
internal readonly record struct PooledFold(char[] Chars, int Length, int[]? Map, int SourceLength) : IDisposable
{
    /// <summary>Gets a view of the folded text for searching.</summary>
    internal FoldedView View => new(Chars.AsSpan(0, Length), Map is null ? default : Map.AsSpan(0, Length), SourceLength);

    /// <summary>Returns the buffers to the pool.</summary>
    public void Dispose()
    {
        ArrayPool<char>.Shared.Return(Chars);
        if (Map is not null)
        {
            ArrayPool<int>.Shared.Return(Map);
        }
    }
}
