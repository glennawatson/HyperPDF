// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>A pooled buffer of arithmetic coding contexts, fresh or copied on creation and returned on disposal.</summary>
internal ref struct Jbig2ContextBuffer
{
    /// <summary>The cache to return the array to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The rented array, or <see langword="null"/> when empty or disposed.</summary>
    private byte[]? _array;

    /// <summary>Initializes a new instance of the <see cref="Jbig2ContextBuffer"/> struct with fresh contexts.</summary>
    /// <param name="length">The number of contexts; zero for none.</param>
    internal Jbig2ContextBuffer(int length)
        : this(length, default, new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Jbig2ContextBuffer"/> struct with an explicit cache.</summary>
    /// <param name="length">The number of contexts.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal Jbig2ContextBuffer(int length, ScratchPools scratchPools)
        : this(length, default, scratchPools)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Jbig2ContextBuffer"/> struct.</summary>
    /// <param name="length">The number of contexts; zero for none.</param>
    /// <param name="initial">The contexts to start from, of <paramref name="length"/> bytes, or empty for fresh contexts.</param>
    internal Jbig2ContextBuffer(int length, ReadOnlySpan<byte> initial)
        : this(length, initial, new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Jbig2ContextBuffer"/> struct with initial bytes and an explicit cache.</summary>
    /// <param name="length">The number of contexts.</param>
    /// <param name="initial">The initial bytes, or empty.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal Jbig2ContextBuffer(int length, ReadOnlySpan<byte> initial, ScratchPools scratchPools)
    {
        _scratchPools = scratchPools;
        _array = length > 0 ? scratchPools.Bytes.Rent(length) : null;
        Span = _array is null ? default : _array.AsSpan(0, length);
        if (initial.Length == length)
        {
            initial.CopyTo(Span);
            return;
        }

        Span.Clear();
    }

    /// <summary>Gets the contexts.</summary>
    internal Span<byte> Span { get; private set; }

    /// <summary>Returns the buffer to the pool.</summary>
    internal void Dispose()
    {
        if (_array is null)
        {
            return;
        }

        _scratchPools.Bytes.Return(_array);
        _array = null;
        Span = default;
    }
}
