// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Serves the rows of samples that are already in memory, one after another with equal strides.</summary>
internal readonly ref struct SpanImageRows : IImageRows
{
    /// <summary>The samples.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The bytes in each row.</summary>
    private readonly int _rowBytes;

    /// <summary>Initializes a new instance of the <see cref="SpanImageRows"/> struct.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="rowBytes">The bytes in each row.</param>
    internal SpanImageRows(ReadOnlySpan<byte> data, int rowBytes)
    {
        _data = data;
        _rowBytes = rowBytes;
    }

    /// <inheritdoc/>
    public bool UsesBuffer => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetRow(int y, Span<byte> buffer) => _data.Slice(y * _rowBytes, _rowBytes);
}
