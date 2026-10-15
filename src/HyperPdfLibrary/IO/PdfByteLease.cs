// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.IO;

/// <summary>
/// A read-only window of a <see cref="PdfByteSource"/>. Dispose it once the bytes are no longer needed: it may hold a
/// pooled buffer or keep a file mapping alive. The span must not be used after disposal.
/// </summary>
[DebuggerDisplay("PdfByteLease: {Span.Length} bytes")]
public ref struct PdfByteLease
{
    /// <summary>The pooled buffer the bytes were copied into, or <see langword="null"/>.</summary>
    private byte[]? _rented;

    /// <summary>The mapping handle whose reference this lease holds, or <see langword="null"/>.</summary>
    private SafeHandle? _handle;

    /// <summary>The pooled-file source whose lease count this lease holds, or <see langword="null"/>.</summary>
    private MemoryPdfByteSource? _memory;

    /// <summary>Initializes a new instance of the <see cref="PdfByteLease"/> struct over bytes that outlive it.</summary>
    /// <param name="span">The bytes.</param>
    internal PdfByteLease(ReadOnlySpan<byte> span) => Span = span;

    /// <summary>Initializes a new instance of the <see cref="PdfByteLease"/> struct over a pooled buffer it returns.</summary>
    /// <param name="rented">The buffer rented from <see cref="ArrayPool{T}.Shared"/>.</param>
    /// <param name="length">The number of bytes used.</param>
    internal PdfByteLease(byte[] rented, int length)
    {
        _rented = rented;
        Span = rented.AsSpan(0, length);
    }

    /// <summary>Initializes a new instance of the <see cref="PdfByteLease"/> struct over a file held in a pooled array.</summary>
    /// <param name="span">The bytes.</param>
    /// <param name="memory">The source whose lease count this lease holds; released on disposal.</param>
    internal PdfByteLease(ReadOnlySpan<byte> span, MemoryPdfByteSource memory)
    {
        _memory = memory;
        Span = span;
    }

    /// <summary>Initializes a new instance of the <see cref="PdfByteLease"/> struct over mapped memory.</summary>
    /// <param name="span">The mapped bytes.</param>
    /// <param name="handle">The handle whose reference was added for this lease; released on disposal.</param>
    internal PdfByteLease(ReadOnlySpan<byte> span, SafeHandle handle)
    {
        _handle = handle;
        Span = span;
    }

    /// <summary>Gets the bytes.</summary>
    public ReadOnlySpan<byte> Span { get; private set; }

    /// <summary>Releases the pooled buffer or the mapping reference.</summary>
    public void Dispose()
    {
        Span = default;
        var rented = _rented;
        _rented = null;
        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        var handle = _handle;
        _handle = null;
        handle?.DangerousRelease();

        var memory = _memory;
        _memory = null;
        memory?.ReleaseLease();
    }
}
