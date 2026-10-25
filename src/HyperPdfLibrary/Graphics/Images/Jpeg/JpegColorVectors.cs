// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Converts sixteen YCbCr pixels at a time to interleaved RGB with the same fixed-point constants and rounding as the scalar conversion.</summary>
internal static class JpegColorVectors
{
    /// <summary>The pixels converted by one step.</summary>
    private const int Step = 16;

    /// <summary>The fixed-point bits of the constants.</summary>
    private const int FixedBits = 16;

    /// <summary>Half a unit in fixed point, for rounding.</summary>
    private const int Half = 1 << (FixedBits - 1);

    /// <summary>The offset of chroma samples.</summary>
    private const int ChromaCenter = 128;

    /// <summary>1.40200 in fixed point.</summary>
    private const int RedFromCr = 91_881;

    /// <summary>1.77200 in fixed point.</summary>
    private const int BlueFromCb = 116_130;

    /// <summary>0.71414 in fixed point.</summary>
    private const int GreenFromCr = 46_802;

    /// <summary>0.34414 in fixed point.</summary>
    private const int GreenFromCb = 22_554;

    /// <summary>The largest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The bytes in a pixel of RGB.</summary>
    private const int ThreeComponents = 3;

    /// <summary>The byte count of one output vector.</summary>
    private const int VectorBytes = 16;

    /// <summary>The index that makes a shuffle write zero.</summary>
    private const byte Zeroed = 0x80;

    /// <summary>Where each red sample goes in the three output vectors, with zero where it does not.</summary>
    private static readonly Vector128<byte> RedFirst = Spread(0, 0);

    /// <summary>Where each red sample goes in the second output vector.</summary>
    private static readonly Vector128<byte> RedSecond = Spread(0, 1);

    /// <summary>Where each red sample goes in the third output vector.</summary>
    private static readonly Vector128<byte> RedThird = Spread(0, 2);

    /// <summary>Where each green sample goes in the first output vector.</summary>
    private static readonly Vector128<byte> GreenFirst = Spread(1, 0);

    /// <summary>Where each green sample goes in the second output vector.</summary>
    private static readonly Vector128<byte> GreenSecond = Spread(1, 1);

    /// <summary>Where each green sample goes in the third output vector.</summary>
    private static readonly Vector128<byte> GreenThird = Spread(1, 2);

    /// <summary>Where each blue sample goes in the first output vector.</summary>
    private static readonly Vector128<byte> BlueFirst = Spread(2, 0);

    /// <summary>Where each blue sample goes in the second output vector.</summary>
    private static readonly Vector128<byte> BlueSecond = Spread(2, 1);

    /// <summary>Where each blue sample goes in the third output vector.</summary>
    private static readonly Vector128<byte> BlueThird = Spread(2, 2);

    /// <summary>Converts whole groups of sixteen pixels.</summary>
    /// <param name="luma">The Y row.</param>
    /// <param name="blue">The Cb row.</param>
    /// <param name="red">The Cr row.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="destination">Receives the RGB pixels.</param>
    /// <returns>The pixels converted, a multiple of sixteen; the caller converts the rest.</returns>
    internal static int YccToRgb(ReadOnlySpan<byte> luma, ReadOnlySpan<byte> blue, ReadOnlySpan<byte> red, int width, Span<byte> destination)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return 0;
        }

        ref var lumaStart = ref MemoryMarshal.GetReference(luma);
        ref var blueStart = ref MemoryMarshal.GetReference(blue);
        ref var redStart = ref MemoryMarshal.GetReference(red);
        ref var output = ref MemoryMarshal.GetReference(destination);
        var x = 0;
        for (; x + Step <= width; x += Step)
        {
            var y = new Samples(Vector128.LoadUnsafe(ref lumaStart, (nuint)x));
            var cb = new Samples(Vector128.LoadUnsafe(ref blueStart, (nuint)x));
            var cr = new Samples(Vector128.LoadUnsafe(ref redStart, (nuint)x));
            var r = Pack(Red(y.A, cr.A), Red(y.B, cr.B), Red(y.C, cr.C), Red(y.D, cr.D));
            var g = Pack(Green(y.A, cb.A, cr.A), Green(y.B, cb.B, cr.B), Green(y.C, cb.C, cr.C), Green(y.D, cb.D, cr.D));
            var b = Pack(Blue(y.A, cb.A), Blue(y.B, cb.B), Blue(y.C, cb.C), Blue(y.D, cb.D));
            ref var pixel = ref Unsafe.Add(ref output, x * ThreeComponents);
            Vector128.StoreUnsafe(Interleave(r, g, b, RedFirst, GreenFirst, BlueFirst), ref pixel);
            Vector128.StoreUnsafe(Interleave(r, g, b, RedSecond, GreenSecond, BlueSecond), ref pixel, VectorBytes);
            Vector128.StoreUnsafe(Interleave(r, g, b, RedThird, GreenThird, BlueThird), ref pixel, VectorBytes + VectorBytes);
        }

        return x;
    }

    /// <summary>Builds the shuffle that places a channel's samples among the interleaved bytes of one output vector.</summary>
    /// <param name="channel">The channel: 0 for red, 1 for green, 2 for blue.</param>
    /// <param name="vector">Which of the three output vectors.</param>
    /// <returns>The shuffle indices.</returns>
    private static Vector128<byte> Spread(int channel, int vector)
    {
        Span<byte> indices = stackalloc byte[VectorBytes];
        for (var i = 0; i < VectorBytes; i++)
        {
            var position = (vector * VectorBytes) + i;
            indices[i] = position % ThreeComponents == channel ? (byte)(position / ThreeComponents) : Zeroed;
        }

        return Vector128.Create(indices);
    }

    /// <summary>Weaves the three channels into one vector of the interleaved output.</summary>
    /// <param name="red">The sixteen red samples.</param>
    /// <param name="green">The sixteen green samples.</param>
    /// <param name="blue">The sixteen blue samples.</param>
    /// <param name="redSpread">Where red goes.</param>
    /// <param name="greenSpread">Where green goes.</param>
    /// <param name="blueSpread">Where blue goes.</param>
    /// <returns>Sixteen output bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Interleave(Vector128<byte> red, Vector128<byte> green, Vector128<byte> blue, Vector128<byte> redSpread, Vector128<byte> greenSpread, Vector128<byte> blueSpread) =>
        Vector128.Shuffle(red, redSpread) | Vector128.Shuffle(green, greenSpread) | Vector128.Shuffle(blue, blueSpread);

    /// <summary>Narrows four vectors of four clamped samples to sixteen bytes.</summary>
    /// <param name="a">The first four samples.</param>
    /// <param name="b">The second four.</param>
    /// <param name="c">The third four.</param>
    /// <param name="d">The last four.</param>
    /// <returns>The sixteen samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Pack(Vector128<int> a, Vector128<int> b, Vector128<int> c, Vector128<int> d) =>
        Vector128.Narrow(Vector128.Narrow(a.AsUInt32(), b.AsUInt32()), Vector128.Narrow(c.AsUInt32(), d.AsUInt32()));

    /// <summary>Converts the red of four pixels.</summary>
    /// <param name="y">The luma.</param>
    /// <param name="cr">The red chroma.</param>
    /// <returns>The clamped red.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Red(Vector128<int> y, Vector128<int> cr) =>
        Clamp(y + Vector128.ShiftRightArithmetic((Vector128.Create(RedFromCr) * (cr - Vector128.Create(ChromaCenter))) + Vector128.Create(Half), FixedBits));

    /// <summary>Converts the green of four pixels.</summary>
    /// <param name="y">The luma.</param>
    /// <param name="cb">The blue chroma.</param>
    /// <param name="cr">The red chroma.</param>
    /// <returns>The clamped green.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Green(Vector128<int> y, Vector128<int> cb, Vector128<int> cr)
    {
        var center = Vector128.Create(ChromaCenter);
        var fromBlue = Vector128.Create(GreenFromCb) * (cb - center);
        var fromRed = Vector128.Create(GreenFromCr) * (cr - center);
        return Clamp(y + Vector128.ShiftRightArithmetic(Vector128.Create(Half) - fromBlue - fromRed, FixedBits));
    }

    /// <summary>Converts the blue of four pixels.</summary>
    /// <param name="y">The luma.</param>
    /// <param name="cb">The blue chroma.</param>
    /// <returns>The clamped blue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Blue(Vector128<int> y, Vector128<int> cb) =>
        Clamp(y + Vector128.ShiftRightArithmetic((Vector128.Create(BlueFromCb) * (cb - Vector128.Create(ChromaCenter))) + Vector128.Create(Half), FixedBits));

    /// <summary>Clamps each lane to a byte.</summary>
    /// <param name="value">The lanes.</param>
    /// <returns>The lanes from 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Clamp(Vector128<int> value) => Vector128.Min(Vector128.Max(value, Vector128<int>.Zero), Vector128.Create(MaxSample));

    /// <summary>Sixteen samples widened to four vectors of four integers.</summary>
    private readonly struct Samples
    {
        /// <summary>Initializes a new instance of the <see cref="Samples"/> struct.</summary>
        /// <param name="bytes">The sixteen samples.</param>
        public Samples(Vector128<byte> bytes)
        {
            var low = Vector128.WidenLower(bytes);
            var high = Vector128.WidenUpper(bytes);
            A = Vector128.WidenLower(low).AsInt32();
            B = Vector128.WidenUpper(low).AsInt32();
            C = Vector128.WidenLower(high).AsInt32();
            D = Vector128.WidenUpper(high).AsInt32();
        }

        /// <summary>Gets the first four samples.</summary>
        public Vector128<int> A { get; }

        /// <summary>Gets the second four samples.</summary>
        public Vector128<int> B { get; }

        /// <summary>Gets the third four samples.</summary>
        public Vector128<int> C { get; }

        /// <summary>Gets the last four samples.</summary>
        public Vector128<int> D { get; }
    }
}
