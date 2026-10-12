// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// Recolours rendered pages so white paper becomes a softer paper colour and black ink a softer ink colour, with every
/// shade in between mapped linearly per channel. A dark paper with light ink gives a night tone. The remap uses lookup
/// tables built once, so applying a tone allocates nothing.
/// </summary>
[DebuggerDisplay("PageTone: Paper {Paper:X6} Ink {Ink:X6}")]
public sealed record PageTone : IEquatable<PageTone>
{
    /// <summary>The values per colour channel.</summary>
    private const int ChannelValues = 256;

    /// <summary>The maximum channel value.</summary>
    private const int ChannelMax = 255;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the green table.</summary>
    private const int GreenTable = ChannelValues;

    /// <summary>The offset of the red table.</summary>
    private const int RedTable = GreenTable + ChannelValues;

    /// <summary>The size of all three tables.</summary>
    private const int TableSize = RedTable + ChannelValues;

    /// <summary>The offset of green within a pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>The offset of red within a pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of the colour bits.</summary>
    private const uint ColorMask = 0xFFFFFFU;

    /// <summary>Opaque alpha in a little-endian BGRA pixel read as a 32 bit value; 0xRRGGBB already lies in BGR order.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>The FNV prime used to derive identifiers.</summary>
    private const uint IdPrime = 16_777_619U;

    /// <summary>The bias added before dividing by 255 so the result rounds to nearest.</summary>
    private const ushort DivideBias = 128;

    /// <summary>The shift used by the divide-by-255 approximation.</summary>
    private const int DivideShift = 8;

    /// <summary>The bit offset of a pixel's second 16 bit lane in its 64 bit lane.</summary>
    private const int FirstLane = 16;

    /// <summary>The bit offset of a pixel's third 16 bit lane.</summary>
    private const int SecondLane = 32;

    /// <summary>The bit offset of a pixel's fourth 16 bit lane.</summary>
    private const int ThirdLane = 48;

    /// <summary>The Rec. 601 weight of blue in brightness, out of 256.</summary>
    private const ushort BlueWeight = 29;

    /// <summary>The Rec. 601 weight of green in brightness, out of 256.</summary>
    private const ushort GreenWeight = 150;

    /// <summary>The Rec. 601 weight of red in brightness, out of 256.</summary>
    private const ushort RedWeight = 77;

    /// <summary>The brightness weights of two pixels' B, G, R, A lanes.</summary>
    private static readonly Vector128<ushort> TwoPixelWeights = Vector128.Create<ushort>([BlueWeight, GreenWeight, RedWeight, 0, BlueWeight, GreenWeight, RedWeight, 0]);

    /// <summary>The brightness weights of four pixels' B, G, R, A lanes.</summary>
    private static readonly Vector256<ushort> LumaWeights = Vector256.Create(TwoPixelWeights, TwoPixelWeights);

    /// <summary>Masks the low 16 bit lane of each 64 bit pixel.</summary>
    private static readonly Vector256<ulong> LowLane = Vector256.Create((ulong)ushort.MaxValue);

    /// <summary>Selects the alpha lanes, which are kept as they are.</summary>
    private static readonly Vector256<ushort> AlphaLanes = Vector256.Create(0, 0, 0, ushort.MaxValue, 0, 0, 0, ushort.MaxValue, 0, 0, 0, ushort.MaxValue, 0, 0, 0, ushort.MaxValue);

    /// <summary>The blue, green and red lookup tables, in that order, used for the scalar tail.</summary>
    private readonly byte[] _table;

    /// <summary>Eight opaque paper pixels, written for eight white ones.</summary>
    private readonly Vector256<byte> _paperBlock;

    /// <summary>The ink weight of each BGRA lane, repeated (alpha 0 so alpha is kept).</summary>
    private readonly Vector256<ushort> _inkLanes;

    /// <summary>The paper weight of each BGRA lane, repeated (alpha 255 so alpha is kept).</summary>
    private readonly Vector256<ushort> _paperLanes;

    /// <summary>Initializes a new instance of the <see cref="PageTone"/> record.</summary>
    /// <param name="paper">The colour white paper becomes, as 0xRRGGBB.</param>
    /// <param name="ink">The colour black ink becomes, as 0xRRGGBB.</param>
    public PageTone(uint paper, uint ink)
    {
        Paper = paper & ColorMask;
        Ink = ink & ColorMask;
        IsIdentity = Paper == ColorMask && Ink == 0;
        Id = IsIdentity ? 0 : (int)(((Paper * IdPrime) ^ Ink) | 1U);
        _table = new byte[TableSize];
        Fill(0, Paper & ChannelMax, Ink & ChannelMax);
        Fill(GreenTable, (Paper >> GreenShift) & ChannelMax, (Ink >> GreenShift) & ChannelMax);
        Fill(RedTable, (Paper >> RedShift) & ChannelMax, (Ink >> RedShift) & ChannelMax);
        _paperBlock = Vector256.Create(OpaqueAlpha | Paper).AsByte();
        _inkLanes = Lanes(Ink, 0);
        _paperLanes = Lanes(Paper, ChannelMax);
    }

    /// <summary>Gets the tone that leaves pages as the document draws them.</summary>
    public static PageTone None { get; } = new(ColorMask, 0);

    /// <summary>Gets the paper colour as 0xRRGGBB.</summary>
    public uint Paper { get; }

    /// <summary>Gets the ink colour as 0xRRGGBB.</summary>
    public uint Ink { get; }

    /// <summary>Gets a stable identifier used to key cached tiles; 0 for <see cref="None"/>.</summary>
    public int Id { get; }

    /// <summary>Gets a value indicating whether the tone leaves pixels unchanged.</summary>
    public bool IsIdentity { get; }

    /// <inheritdoc/>
    public bool Equals(PageTone? other) => other is not null && Paper == other.Paper && Ink == other.Ink;

    /// <inheritdoc/>
    public override int GetHashCode() => Id;

    /// <summary>Recolours an opaque BGRA buffer in place.</summary>
    /// <param name="target">The buffer.</param>
    public void Apply(RenderTarget target)
    {
        if (IsIdentity)
        {
            return;
        }

        var rowBytes = target.Width * BytesPerPixel;
        for (var y = 0; y < target.Height; y++)
        {
            Apply(target.Pixels.Slice(y * target.Stride, rowBytes));
        }
    }

    /// <summary>Recolours opaque BGRA pixels in place.</summary>
    /// <param name="pixels">The pixel bytes, four per pixel.</param>
    public void Apply(Span<byte> pixels)
    {
        if (IsIdentity)
        {
            return;
        }

        var i = ApplyVectorized(pixels);
        ReadOnlySpan<byte> blue = _table.AsSpan(0, ChannelValues);
        ReadOnlySpan<byte> green = _table.AsSpan(GreenTable, ChannelValues);
        ReadOnlySpan<byte> red = _table.AsSpan(RedTable, ChannelValues);
        for (; i + BytesPerPixel <= pixels.Length; i += BytesPerPixel)
        {
            var b = pixels[i];
            var g = pixels[i + GreenByte];
            var r = pixels[i + RedByte];
            var luma = ((b * BlueWeight) + (g * GreenWeight) + (r * RedWeight)) >> DivideShift;
            pixels[i] = Shift(blue[luma], b, luma);
            pixels[i + GreenByte] = Shift(green[luma], g, luma);
            pixels[i + RedByte] = Shift(red[luma], r, luma);
        }
    }

    /// <summary>Adds a channel's offset from the pixel's brightness back onto the toned brightness, so hue survives.</summary>
    /// <param name="toned">The toned brightness for this channel.</param>
    /// <param name="value">The channel value.</param>
    /// <param name="luma">The pixel's brightness.</param>
    /// <returns>The toned channel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Shift(byte toned, byte value, int luma) => (byte)Math.Clamp(toned + value - luma, 0, ChannelMax);

    /// <summary>Builds lane weights repeating B, G, R, A for one colour.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <param name="alpha">The alpha weight.</param>
    /// <returns>The lanes.</returns>
    private static Vector256<ushort> Lanes(uint rgb, ushort alpha)
    {
        var blue = (ushort)(rgb & ChannelMax);
        var green = (ushort)((rgb >> GreenShift) & ChannelMax);
        var red = (ushort)((rgb >> RedShift) & ChannelMax);
        return Vector256.Create(blue, green, red, alpha, blue, green, red, alpha, blue, green, red, alpha, blue, green, red, alpha);
    }

    /// <summary>Tones two pixels held as eight 16 bit B, G, R, A lanes; the 128 bit form of <see cref="Blend(Vector256{ushort})"/>.</summary>
    /// <param name="value">The channel values.</param>
    /// <param name="inkLanes">The ink weight of each lane.</param>
    /// <param name="paperLanes">The paper weight of each lane.</param>
    /// <returns>The toned values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> Blend(Vector128<ushort> value, Vector128<ushort> inkLanes, Vector128<ushort> paperLanes)
    {
        var weighted = (value * TwoPixelWeights).AsUInt64();
        var total = (weighted + (weighted >>> FirstLane) + (weighted >>> SecondLane) + (weighted >>> ThirdLane)) & LowLane.GetLower();
        var low = total >>> DivideShift;
        var luma = (low | (low << FirstLane) | (low << SecondLane) | (low << ThirdLane)).AsUInt16();

        var max = Vector128.Create((ushort)ChannelMax);
        var sum = (inkLanes * (max - luma)) + (paperLanes * luma) + Vector128.Create(DivideBias);
        var toned = (sum + (sum >>> DivideShift)) >>> DivideShift;
        var shifted = Vector128.Min(Vector128.Max(toned.AsInt16() + value.AsInt16() - luma.AsInt16(), Vector128<short>.Zero), max.AsInt16()).AsUInt16();
        return Vector128.ConditionalSelect(AlphaLanes.GetLower(), value, shifted);
    }

    /// <summary>
    /// Tones eight pixels at a time. Each pixel's brightness <c>L</c> is mapped along the ink to paper ramp as
    /// <c>(ink * (255 - L) + paper * L) / 255</c>, rounded, and each channel's offset from <c>L</c> is added back so
    /// colours keep their hue: a yellow highlight stays yellow on dark paper. Every intermediate fits 16 bit lanes.
    /// </summary>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The number of bytes processed; the rest is left for the scalar loop.</returns>
    private int ApplyVectorized(Span<byte> pixels)
    {
        var processed = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            ref var start = ref MemoryMarshal.GetReference(pixels);
            var white = Vector256<byte>.AllBitsSet;
            for (; processed + Vector256<byte>.Count <= pixels.Length; processed += Vector256<byte>.Count)
            {
                var source = Vector256.LoadUnsafe(ref start, (nuint)processed);

                // Most of a page is blank paper; eight white pixels become eight paper pixels without any arithmetic.
                if (source == white)
                {
                    _paperBlock.StoreUnsafe(ref start, (nuint)processed);
                    continue;
                }

                var (lower, upper) = Vector256.Widen(source);
                Vector256.Narrow(Blend(lower), Blend(upper)).StoreUnsafe(ref start, (nuint)processed);
            }
        }

        // ARM64 has 128 bit vectors only, and x64 finishes a row's last four to seven pixels here.
        return Vector128.IsHardwareAccelerated ? ApplyVectorized128(pixels, processed) : processed;
    }

    /// <summary>Tones four pixels at a time with 128 bit vectors, using the same arithmetic as the 256 bit path.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="processed">The bytes already toned.</param>
    /// <returns>The number of bytes processed; the rest is left for the scalar loop.</returns>
    private int ApplyVectorized128(Span<byte> pixels, int processed)
    {
        ref var start = ref MemoryMarshal.GetReference(pixels);
        var white = Vector128<byte>.AllBitsSet;
        var paper = _paperBlock.GetLower();
        var ink = _inkLanes.GetLower();
        var paperLanes = _paperLanes.GetLower();
        for (; processed + Vector128<byte>.Count <= pixels.Length; processed += Vector128<byte>.Count)
        {
            var source = Vector128.LoadUnsafe(ref start, (nuint)processed);
            if (source == white)
            {
                paper.StoreUnsafe(ref start, (nuint)processed);
                continue;
            }

            var (lower, upper) = Vector128.Widen(source);
            Vector128.Narrow(Blend(lower, ink, paperLanes), Blend(upper, ink, paperLanes)).StoreUnsafe(ref start, (nuint)processed);
        }

        return processed;
    }

    /// <summary>Tones four pixels held as sixteen 16 bit B, G, R, A lanes.</summary>
    /// <param name="value">The channel values.</param>
    /// <returns>The toned values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector256<ushort> Blend(Vector256<ushort> value)
    {
        // Brightness: a pixel's four weighted 16 bit lanes fill one 64 bit lane, so shifts sum them into the low 16 bits
        // (carries only move upwards) and spread the result back to all four lanes, with no shuffles.
        var weighted = (value * LumaWeights).AsUInt64();
        var total = (weighted + (weighted >>> FirstLane) + (weighted >>> SecondLane) + (weighted >>> ThirdLane)) & LowLane;
        var low = total >>> DivideShift;
        var luma = (low | (low << FirstLane) | (low << SecondLane) | (low << ThirdLane)).AsUInt16();

        var max = Vector256.Create((ushort)ChannelMax);
        var sum = (_inkLanes * (max - luma)) + (_paperLanes * luma) + Vector256.Create(DivideBias);
        var toned = (sum + (sum >>> DivideShift)) >>> DivideShift;
        var shifted = Vector256.Min(Vector256.Max(toned.AsInt16() + value.AsInt16() - luma.AsInt16(), Vector256<short>.Zero), max.AsInt16()).AsUInt16();
        return Vector256.ConditionalSelect(AlphaLanes, value, shifted);
    }

    /// <summary>Fills one channel's table with a linear ramp from ink (0) to paper (255).</summary>
    /// <param name="offset">The table offset.</param>
    /// <param name="paper">The paper channel value.</param>
    /// <param name="ink">The ink channel value.</param>
    private void Fill(int offset, uint paper, uint ink)
    {
        var span = _table.AsSpan(offset, ChannelValues);
        for (var value = 0; value < ChannelValues; value++)
        {
            var sum = (int)((ink * (uint)(ChannelMax - value)) + (paper * (uint)value) + DivideBias);
            span[value] = (byte)((sum + (sum >> DivideShift)) >> DivideShift);
        }
    }
}
