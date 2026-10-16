// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Bounds-checked big-endian reads. Font programs in PDF files are often truncated or damaged, so a read past the end
/// returns zero rather than throwing.
/// </summary>
internal static class FontBytes
{
    /// <summary>The size of a 16-bit value.</summary>
    internal const int U16Size = 2;

    /// <summary>The size of a 32-bit value.</summary>
    internal const int U32Size = 4;

    /// <summary>The scale of a signed 2.14 fixed-point number.</summary>
    private const float F2Dot14Scale = 16384F;

    /// <summary>Reads an unsigned byte.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or zero past the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int U8(ReadOnlySpan<byte> data, int offset) => (uint)offset < (uint)data.Length ? data[offset] : 0;

    /// <summary>Reads a signed byte.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or zero past the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int S8(ReadOnlySpan<byte> data, int offset) => (sbyte)U8(data, offset);

    /// <summary>Reads an unsigned 16-bit value.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or zero past the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int U16(ReadOnlySpan<byte> data, int offset) =>
        offset >= 0 && offset <= data.Length - U16Size ? BinaryPrimitives.ReadUInt16BigEndian(data[offset..]) : 0;

    /// <summary>Reads a signed 16-bit value.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or zero past the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int S16(ReadOnlySpan<byte> data, int offset) => (short)U16(data, offset);

    /// <summary>Reads an unsigned 32-bit value.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or zero past the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint U32(ReadOnlySpan<byte> data, int offset) =>
        offset >= 0 && offset <= data.Length - U32Size ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : 0;

    /// <summary>Reads a 32-bit offset, clamped so a damaged value cannot be negative.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or <see cref="int.MaxValue"/> when it does not fit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Offset32(ReadOnlySpan<byte> data, int offset) => (int)Math.Min(U32(data, offset), int.MaxValue);

    /// <summary>Adds a parsed unsigned relative offset after bounding it against the data.</summary>
    /// <param name="data">The font bytes.</param>
    /// <param name="basis">The start the parsed offset is relative to.</param>
    /// <param name="relative">The parsed offset.</param>
    /// <returns>The absolute offset.</returns>
    /// <exception cref="PdfException">The relative offset exceeds the font bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AddOffset(ReadOnlySpan<byte> data, int basis, uint relative)
    {
        if (basis < 0 || basis > data.Length || relative > data.Length - basis)
        {
            throw new PdfException(PdfError.Format, "The font relative offset exceeds its data.");
        }

        return basis + (int)relative;
    }

    /// <summary>Reads a signed 2.14 fixed-point number.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or zero past the end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float F2Dot14(ReadOnlySpan<byte> data, int offset) => S16(data, offset) / F2Dot14Scale;

    /// <summary>Takes a slice after validating the range against the data.</summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The start.</param>
    /// <param name="length">The length.</param>
    /// <returns>The slice.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
        {
            throw new PdfException(PdfError.Format, "The font byte range exceeds its data.");
        }

        return data.Slice(offset, length);
    }
}
