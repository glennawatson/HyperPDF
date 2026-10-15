// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The contexts of every arithmetic integer decoder and the symbol ID decoder used by one symbol dictionary or text
/// region, in one pooled buffer.
/// </summary>
[DebuggerDisplay("Jbig2IntegerContexts: {IdCodeLength} ID bits")]
internal readonly record struct Jbig2IntegerContexts : IDisposable
{
    /// <summary>The number of integer decoders.</summary>
    private const int DecoderCount = 13;

    /// <summary>The cache this instance returns its buffer to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The integer contexts, followed by the symbol ID contexts.</summary>
    private readonly byte[] _buffer;

    /// <summary>Initializes a new instance of the <see cref="Jbig2IntegerContexts"/> struct with cleared contexts.</summary>
    /// <param name="idCodeLength">The bits in a symbol ID, from 0 to 31.</param>
    internal Jbig2IntegerContexts(int idCodeLength)
        : this(idCodeLength, new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Jbig2IntegerContexts"/> struct with an explicit cache.</summary>
    /// <param name="idCodeLength">The bits in a symbol ID.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal Jbig2IntegerContexts(int idCodeLength, ScratchPools scratchPools)
    {
        IdCodeLength = idCodeLength;
        _scratchPools = scratchPools;
        var length = (DecoderCount * Jbig2IntegerDecoder.ContextCount) + (1 << idCodeLength);
        _buffer = scratchPools.Bytes.Rent(length);
        _buffer.AsSpan(0, length).Clear();
    }

    /// <summary>Gets the bits in a symbol ID.</summary>
    internal int IdCodeLength { get; }

    /// <summary>Gets the symbol ID contexts.</summary>
    internal Span<byte> Id => _buffer.AsSpan(DecoderCount * Jbig2IntegerDecoder.ContextCount, 1 << IdCodeLength);

    /// <summary>Returns the buffer to the pool. Only the owner that created the contexts disposes them, once.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _scratchPools.Bytes.Return(_buffer);

    /// <summary>Gets the contexts of one integer decoder.</summary>
    /// <param name="kind">The decoder.</param>
    /// <returns>The contexts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> Get(Jbig2IntegerKind kind) =>
        _buffer.AsSpan((int)kind * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
}
