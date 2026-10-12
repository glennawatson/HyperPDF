// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A pooled, growable buffer holding the decoded bytes of the token the PDF/A syntax reader last read. One validation
/// owns it; it is not shared between threads.
/// </summary>
[DebuggerDisplay("PdfASyntaxBuffer: {Length} bytes")]
internal sealed class PdfASyntaxBuffer : IDisposable
{
    /// <summary>The first capacity.</summary>
    private const int InitialCapacity = 256;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array.</summary>
    private byte[] _bytes = ArrayPool<byte>.Shared.Rent(InitialCapacity);

    /// <summary>Gets the number of bytes held.</summary>
    internal int Length { get; private set; }

    /// <summary>Gets the bytes held.</summary>
    internal ReadOnlySpan<byte> Span => _bytes.AsSpan(0, Length);

    /// <summary>Returns the array to the pool.</summary>
    public void Dispose()
    {
        if (_bytes.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_bytes);
        }

        _bytes = [];
        Length = 0;
    }

    /// <summary>Empties the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => Length = 0;

    /// <summary>Adds a byte.</summary>
    /// <param name="value">The byte.</param>
    internal void Add(byte value)
    {
        if (Length == _bytes.Length)
        {
            Grow();
        }

        _bytes[Length] = value;
        Length++;
    }

    /// <summary>Doubles the capacity.</summary>
    private void Grow()
    {
        var larger = ArrayPool<byte>.Shared.Rent(Math.Max(InitialCapacity, _bytes.Length * GrowthFactor));
        _bytes.AsSpan(0, Length).CopyTo(larger);
        if (_bytes.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_bytes);
        }

        _bytes = larger;
    }
}
