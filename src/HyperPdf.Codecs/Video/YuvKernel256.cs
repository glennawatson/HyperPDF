// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.Video;

/// <summary>Converts 16 pixels at a time from Y, Cb and Cr to blue, green, red and alpha with 256 bit vectors; it gives the same pixels as <see cref="YuvConverter.Pixel"/>.</summary>
internal static class YuvKernel256
{
    /// <summary>The pixels converted in each step.</summary>
    private const int Step = 16;

    /// <summary>The pixels in one vector of 32 bit values.</summary>
    private const int HalfStep = Step >> 1;

    /// <summary>Gets the lanes that repeat each of the first four colour samples twice.</summary>
    private static Vector256<int> LowerDuplicate => Vector256.Create(0x0, 0x0, 0x1, 0x1, 0x2, 0x2, 0x3, 0x3);

    /// <summary>Gets the lanes that repeat each of the last four colour samples twice.</summary>
    private static Vector256<int> UpperDuplicate => Vector256.Create(0x4, 0x4, 0x5, 0x5, 0x6, 0x6, 0x7, 0x7);

    /// <summary>Converts as many whole steps of a row as fit.</summary>
    /// <param name="y">The luma samples.</param>
    /// <param name="u">The Cb samples.</param>
    /// <param name="v">The Cr samples.</param>
    /// <param name="matrix">The weights.</param>
    /// <param name="pixels">Receives the pixels.</param>
    /// <returns>The number of pixels converted, a multiple of 16.</returns>
    internal static int Convert(ReadOnlySpan<byte> y, ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, in ColorMatrix matrix, Span<int> pixels)
    {
        var weights = YuvWeights256.From(matrix);
        ref readonly var lumaPlane = ref MemoryMarshal.GetReference(y);
        ref readonly var blueSamples = ref MemoryMarshal.GetReference(u);
        ref readonly var redSamples = ref MemoryMarshal.GetReference(v);
        ref var output = ref MemoryMarshal.GetReference(pixels);
        var x = 0;
        for (; x + Step <= y.Length; x += Step)
        {
            var luma = Vector256.WidenLower(Vector128.LoadUnsafe(in lumaPlane, (nuint)x).ToVector256Unsafe());
            var cb = Colour(in blueSamples, x >> 1);
            var cr = Colour(in redSamples, x >> 1);
            weights.Pixels(Vector256.WidenLower(luma).AsInt32(), Vector256.Shuffle(cb, LowerDuplicate), Vector256.Shuffle(cr, LowerDuplicate)).StoreUnsafe(ref output, (nuint)x);
            weights.Pixels(Vector256.WidenUpper(luma).AsInt32(), Vector256.Shuffle(cb, UpperDuplicate), Vector256.Shuffle(cr, UpperDuplicate)).StoreUnsafe(ref output, (nuint)(x + HalfStep));
        }

        return x;
    }

    /// <summary>Reads eight colour samples as 32 bit numbers.</summary>
    /// <param name="plane">The first sample of the colour plane.</param>
    /// <param name="at">The index of the first sample to read.</param>
    /// <returns>The samples, one in each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Colour(ref readonly byte plane, int at) =>
        Vector256.WidenLower(Vector256.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(in Unsafe.Add(ref Unsafe.AsRef(in plane), at))).AsByte().ToVector256Unsafe())).AsInt32();
}
