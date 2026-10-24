// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A transform matrix and how to pick the basis functions of one block size from it.</summary>
internal readonly ref struct H265TransformTable
{
    /// <summary>Initializes a new instance of the <see cref="H265TransformTable"/> struct.</summary>
    /// <param name="matrix">The matrix, row after row.</param>
    /// <param name="rowStride">The entries in a row of the matrix.</param>
    /// <param name="step">How many rows of the matrix lie between two frequencies of the block.</param>
    internal H265TransformTable(ReadOnlySpan<int> matrix, int rowStride, int step)
    {
        Matrix = matrix;
        RowStride = rowStride;
        Step = step;
    }

    /// <summary>Gets the matrix, row after row.</summary>
    internal ReadOnlySpan<int> Matrix { get; }

    /// <summary>Gets the entries in a row of the matrix.</summary>
    internal int RowStride { get; }

    /// <summary>Gets how many rows of the matrix lie between two frequencies of the block.</summary>
    internal int Step { get; }
}
