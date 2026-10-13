// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Combines one 1-bit bitmap into another at a pixel offset with a JBIG2 combination operator (T.88 section 6.3.2).
/// The source is clipped to the target. Byte-aligned runs use Vector256 or Vector128 when available.
/// </summary>
internal static class Jbig2Composer
{
    /// <summary>A byte with every bit set.</summary>
    private const int FullByte = 0xFF;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>A combination operator on bytes and vectors of bytes.</summary>
    private interface IComposeOperation
    {
        /// <summary>Combines source and target bits.</summary>
        /// <param name="source">The source bits.</param>
        /// <param name="target">The target bits.</param>
        /// <returns>The combined bits; only the low eight are used.</returns>
        static abstract int Apply(int source, int target);

        /// <summary>Combines 16 source and target bytes.</summary>
        /// <param name="source">The source bytes.</param>
        /// <param name="target">The target bytes.</param>
        /// <returns>The combined bytes.</returns>
        static abstract Vector128<byte> Apply(Vector128<byte> source, Vector128<byte> target);

        /// <summary>Combines 32 source and target bytes.</summary>
        /// <param name="source">The source bytes.</param>
        /// <param name="target">The target bytes.</param>
        /// <returns>The combined bytes.</returns>
        static abstract Vector256<byte> Apply(Vector256<byte> source, Vector256<byte> target);
    }

    /// <summary>Combines a source bitmap into a target at an offset.</summary>
    /// <param name="target">The target bitmap.</param>
    /// <param name="source">The source bitmap.</param>
    /// <param name="x">The column of the source's left edge in the target.</param>
    /// <param name="y">The row of the source's top edge in the target.</param>
    /// <param name="operation">The combination operator.</param>
    /// <returns>The number of pixels combined.</returns>
    internal static long Compose(Jbig2Bitmap target, Jbig2BitmapView source, long x, long y, Jbig2ComposeOperator operation) => operation switch
    {
        Jbig2ComposeOperator.And => Compose<AndOperation>(target, source, x, y),
        Jbig2ComposeOperator.Xor => Compose<XorOperation>(target, source, x, y),
        Jbig2ComposeOperator.Xnor => Compose<XnorOperation>(target, source, x, y),
        Jbig2ComposeOperator.Replace => Compose<ReplaceOperation>(target, source, x, y),
        _ => Compose<OrOperation>(target, source, x, y),
    };

    /// <summary>Copies bytes, inverting every bit.</summary>
    /// <param name="source">The source bytes.</param>
    /// <param name="destination">The destination, at least as long as the source.</param>
    internal static void CopyInverted(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref var from = ref MemoryMarshal.GetReference(source);
        ref var to = ref MemoryMarshal.GetReference(destination[..source.Length]);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            for (; i + Vector256<byte>.Count <= source.Length; i += Vector256<byte>.Count)
            {
                (~Vector256.LoadUnsafe(ref from, (nuint)i)).StoreUnsafe(ref to, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i + Vector128<byte>.Count <= source.Length; i += Vector128<byte>.Count)
            {
                (~Vector128.LoadUnsafe(ref from, (nuint)i)).StoreUnsafe(ref to, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = (byte)~source[i];
        }
    }

    /// <summary>Combines a source bitmap into a target with one operator.</summary>
    /// <typeparam name="TOperation">The operator.</typeparam>
    /// <param name="target">The target bitmap.</param>
    /// <param name="source">The source bitmap.</param>
    /// <param name="x">The column of the source's left edge in the target.</param>
    /// <param name="y">The row of the source's top edge in the target.</param>
    /// <returns>The number of pixels combined.</returns>
    private static long Compose<TOperation>(Jbig2Bitmap target, Jbig2BitmapView source, long x, long y)
        where TOperation : struct, IComposeOperation
    {
        // Offsets are signed 32-bit values (T.88 section 7.4.1). A source that lies wholly outside the target changes nothing,
        // and testing that in 64-bit arithmetic keeps the clipping below within int range.
        if (source.IsEmpty || x >= target.Width || y >= target.Height || x + source.Width <= 0 || y + source.Height <= 0)
        {
            return 0;
        }

        var left = x < 0 ? (int)-x : 0;
        var top = y < 0 ? (int)-y : 0;
        var right = (int)Math.Min(source.Width, target.Width - x);
        var bottom = (int)Math.Min(source.Height, target.Height - y);
        var targetX = (int)Math.Max(x, 0);
        var targetY = (int)Math.Max(y, 0) - top;
        var width = right - left;
        for (var row = top; row < bottom; row++)
        {
            ComposeRow<TOperation>(target.Row(targetY + row), targetX, source.Row(row), left, width);
        }

        return (long)width * (bottom - top);
    }

    /// <summary>Combines a run of source pixels into a target row.</summary>
    /// <typeparam name="TOperation">The operator.</typeparam>
    /// <param name="target">The target row.</param>
    /// <param name="targetBit">The first target pixel.</param>
    /// <param name="source">The source row.</param>
    /// <param name="sourceBit">The first source pixel.</param>
    /// <param name="count">The number of pixels, at least one.</param>
    private static void ComposeRow<TOperation>(Span<byte> target, int targetBit, ReadOnlySpan<byte> source, int sourceBit, int count)
        where TOperation : struct, IComposeOperation
    {
        var first = targetBit >> Jbig2Bits.ByteShift;
        var lastBit = targetBit + count - 1;
        var last = lastBit >> Jbig2Bits.ByteShift;
        var offset = sourceBit - targetBit;
        var firstMask = FullByte >> (targetBit & Jbig2Bits.BitMask);
        var lastMask = (FullByte << (Jbig2Bits.BitMask - (lastBit & Jbig2Bits.BitMask))) & FullByte;
        if (first == last)
        {
            Apply<TOperation>(ref target[first], Fetch(source, (first << Jbig2Bits.ByteShift) + offset), firstMask & lastMask);
            return;
        }

        Apply<TOperation>(ref target[first], Fetch(source, (first << Jbig2Bits.ByteShift) + offset), firstMask);
        var middle = target[(first + 1)..last];
        var sourceStart = ((first + 1) << Jbig2Bits.ByteShift) + offset;
        if ((offset & Jbig2Bits.BitMask) == 0)
        {
            ApplyAligned<TOperation>(middle, source.Slice(sourceStart >> Jbig2Bits.ByteShift, middle.Length));
        }
        else
        {
            ApplyShifted<TOperation>(middle, source, sourceStart);
        }

        Apply<TOperation>(ref target[last], Fetch(source, (last << Jbig2Bits.ByteShift) + offset), lastMask);
    }

    /// <summary>Gets eight source pixels starting at a pixel, reading 0 before the row and past its end.</summary>
    /// <param name="source">The source row.</param>
    /// <param name="bit">The first pixel, from -7 up.</param>
    /// <returns>The pixels, the first in the most significant bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Fetch(ReadOnlySpan<byte> source, int bit)
    {
        if (bit < 0)
        {
            return source[0] >> -bit;
        }

        var index = bit >> Jbig2Bits.ByteShift;
        var shift = bit & Jbig2Bits.BitMask;
        int high = source[index];
        if (shift == 0)
        {
            return high;
        }

        var low = index + 1 < source.Length ? source[index + 1] : 0;
        return (((high << ByteBits) | low) >> (ByteBits - shift)) & FullByte;
    }

    /// <summary>Combines eight source pixels into the masked bits of a target byte.</summary>
    /// <typeparam name="TOperation">The operator.</typeparam>
    /// <param name="target">The target byte.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="mask">The bits to change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Apply<TOperation>(ref byte target, int source, int mask)
        where TOperation : struct, IComposeOperation =>
        target = (byte)((target & ~mask) | (TOperation.Apply(source, target) & mask));

    /// <summary>Combines source bytes into target bytes that share a bit alignment.</summary>
    /// <typeparam name="TOperation">The operator.</typeparam>
    /// <param name="target">The target bytes.</param>
    /// <param name="source">The source bytes, as many as the target.</param>
    private static void ApplyAligned<TOperation>(Span<byte> target, ReadOnlySpan<byte> source)
        where TOperation : struct, IComposeOperation
    {
        ref var to = ref MemoryMarshal.GetReference(target);
        ref var from = ref MemoryMarshal.GetReference(source);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            for (; i + Vector256<byte>.Count <= target.Length; i += Vector256<byte>.Count)
            {
                TOperation.Apply(Vector256.LoadUnsafe(ref from, (nuint)i), Vector256.LoadUnsafe(ref to, (nuint)i)).StoreUnsafe(ref to, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i + Vector128<byte>.Count <= target.Length; i += Vector128<byte>.Count)
            {
                TOperation.Apply(Vector128.LoadUnsafe(ref from, (nuint)i), Vector128.LoadUnsafe(ref to, (nuint)i)).StoreUnsafe(ref to, (nuint)i);
            }
        }

        for (; i < target.Length; i++)
        {
            target[i] = (byte)TOperation.Apply(source[i], target[i]);
        }
    }

    /// <summary>Combines source pixels into target bytes when the source is not byte-aligned with the target.</summary>
    /// <typeparam name="TOperation">The operator.</typeparam>
    /// <param name="target">The target bytes.</param>
    /// <param name="source">The source row.</param>
    /// <param name="sourceBit">The source pixel of the first target bit, at least 0.</param>
    private static void ApplyShifted<TOperation>(Span<byte> target, ReadOnlySpan<byte> source, int sourceBit)
        where TOperation : struct, IComposeOperation
    {
        var index = sourceBit >> Jbig2Bits.ByteShift;
        var back = ByteBits - (sourceBit & Jbig2Bits.BitMask);
        int carry = source[index];
        for (var i = 0; i < target.Length; i++)
        {
            int next = source[index + i + 1];
            target[i] = (byte)TOperation.Apply((((carry << ByteBits) | next) >> back) & FullByte, target[i]);
            carry = next;
        }
    }

    /// <summary>The OR operator.</summary>
    private readonly struct OrOperation : IComposeOperation
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int source, int target) => source | target;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Apply(Vector128<byte> source, Vector128<byte> target) => source | target;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Apply(Vector256<byte> source, Vector256<byte> target) => source | target;
    }

    /// <summary>The AND operator.</summary>
    private readonly struct AndOperation : IComposeOperation
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int source, int target) => source & target;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Apply(Vector128<byte> source, Vector128<byte> target) => source & target;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Apply(Vector256<byte> source, Vector256<byte> target) => source & target;
    }

    /// <summary>The XOR operator.</summary>
    private readonly struct XorOperation : IComposeOperation
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int source, int target) => source ^ target;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Apply(Vector128<byte> source, Vector128<byte> target) => source ^ target;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Apply(Vector256<byte> source, Vector256<byte> target) => source ^ target;
    }

    /// <summary>The XNOR operator.</summary>
    private readonly struct XnorOperation : IComposeOperation
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int source, int target) => ~(source ^ target);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Apply(Vector128<byte> source, Vector128<byte> target) => ~(source ^ target);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Apply(Vector256<byte> source, Vector256<byte> target) => ~(source ^ target);
    }

    /// <summary>The REPLACE operator.</summary>
    private readonly struct ReplaceOperation : IComposeOperation
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int source, int target) => source;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Apply(Vector128<byte> source, Vector128<byte> target) => source;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Apply(Vector256<byte> source, Vector256<byte> target) => source;
    }
}
