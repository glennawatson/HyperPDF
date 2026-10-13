// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Supplies the packed sample rows of an image one at a time, so a decoder need not hold every row at once.</summary>
internal interface IImageRows
{
    /// <summary>Gets a value indicating whether <see cref="GetRow"/> writes into the buffer it is given.</summary>
    bool UsesBuffer { get; }

    /// <summary>Gets one row of packed samples.</summary>
    /// <param name="y">The row, from the top.</param>
    /// <param name="buffer">Room for the row when <see cref="UsesBuffer"/> is set; otherwise empty.</param>
    /// <returns>The row's bytes: the buffer's start, or the source's own row.</returns>
    ReadOnlySpan<byte> GetRow(int y, Span<byte> buffer);
}
