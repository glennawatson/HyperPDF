// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Moves a tile's samples from the interleaved image into tile-component buffers, level-shifted so unsigned samples are
/// centred on zero (ISO 15444-1 G.2), and applies the forward component transform (annex G).
/// </summary>
internal static class JpxTileSamples
{
    /// <summary>The bits of a byte: samples of at most this many bits take one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a sample of more than eight bits.</summary>
    private const int WideBytes = 2;

    /// <summary>The weight of green in the forward RCT and of the second sample in the luma sum.</summary>
    private const int GreenWeight = 2;

    /// <summary>The shift that divides the RCT luma sum by four.</summary>
    private const int QuarterShift = 2;

    /// <summary>The luma weight of red in the ICT (equation G-9).</summary>
    private const float LumaRed = 0.299F;

    /// <summary>The luma weight of green in the ICT.</summary>
    private const float LumaGreen = 0.587F;

    /// <summary>The luma weight of blue in the ICT.</summary>
    private const float LumaBlue = 0.114F;

    /// <summary>The weight of red in the blue chroma.</summary>
    private const float BlueChromaRed = -0.16875F;

    /// <summary>The weight of green in the blue chroma.</summary>
    private const float BlueChromaGreen = -0.33126F;

    /// <summary>The weight of blue in the blue chroma.</summary>
    private const float BlueChromaBlue = 0.5F;

    /// <summary>The weight of red in the red chroma.</summary>
    private const float RedChromaRed = 0.5F;

    /// <summary>The weight of green in the red chroma.</summary>
    private const float RedChromaGreen = -0.41869F;

    /// <summary>The weight of blue in the red chroma.</summary>
    private const float RedChromaBlue = -0.08131F;

    /// <summary>Loads one component of a tile as integers, less the level shift.</summary>
    /// <param name="samples">The interleaved image samples.</param>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="component">The component.</param>
    /// <param name="area">The tile on the reference grid.</param>
    /// <param name="destination">Receives the samples row by row, as wide as the tile.</param>
    internal static void LoadIntegers(ReadOnlySpan<byte> samples, JpxEncodeSettings settings, int component, in JpxRectangle area, Span<int> destination)
    {
        var shift = 1 << (settings.Precision - 1);
        var bytes = settings.Precision > ByteBits ? WideBytes : 1;
        var step = settings.Components * bytes;
        for (var y = 0; y < area.Height; y++)
        {
            var source = ((((area.Y0 + y) * settings.Width) + area.X0) * step) + (component * bytes);
            var row = destination.Slice(y * area.Width, area.Width);
            for (var x = 0; x < row.Length; x++)
            {
                row[x] = ReadSample(samples, source, bytes) - shift;
                source += step;
            }
        }
    }

    /// <summary>Loads one component of a tile as floats, less the level shift.</summary>
    /// <param name="samples">The interleaved image samples.</param>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="component">The component.</param>
    /// <param name="area">The tile on the reference grid.</param>
    /// <param name="destination">Receives the samples row by row, as wide as the tile.</param>
    internal static void LoadFloats(ReadOnlySpan<byte> samples, JpxEncodeSettings settings, int component, in JpxRectangle area, Span<float> destination)
    {
        var shift = 1 << (settings.Precision - 1);
        var bytes = settings.Precision > ByteBits ? WideBytes : 1;
        var step = settings.Components * bytes;
        for (var y = 0; y < area.Height; y++)
        {
            var source = ((((area.Y0 + y) * settings.Width) + area.X0) * step) + (component * bytes);
            var row = destination.Slice(y * area.Width, area.Width);
            for (var x = 0; x < row.Length; x++)
            {
                row[x] = ReadSample(samples, source, bytes) - shift;
                source += step;
            }
        }
    }

    /// <summary>Applies the forward reversible component transform (equation G-5): luma and two differences, with no loss.</summary>
    /// <param name="red">The first component, replaced by luma.</param>
    /// <param name="green">The second component, replaced by blue less green.</param>
    /// <param name="blue">The third component, replaced by red less green.</param>
    internal static void ForwardReversible(Span<int> red, Span<int> green, Span<int> blue)
    {
        for (var i = 0; i < red.Length; i++)
        {
            var luma = (red[i] + (GreenWeight * green[i]) + blue[i]) >> QuarterShift;
            var blueDifference = blue[i] - green[i];
            var redDifference = red[i] - green[i];
            red[i] = luma;
            green[i] = blueDifference;
            blue[i] = redDifference;
        }
    }

    /// <summary>Applies the forward irreversible component transform (equation G-9).</summary>
    /// <param name="red">The first component, replaced by luma.</param>
    /// <param name="green">The second component, replaced by the blue chroma.</param>
    /// <param name="blue">The third component, replaced by the red chroma.</param>
    internal static void ForwardIrreversible(Span<float> red, Span<float> green, Span<float> blue)
    {
        for (var i = 0; i < red.Length; i++)
        {
            var r = red[i];
            var g = green[i];
            var b = blue[i];
            red[i] = (LumaRed * r) + (LumaGreen * g) + (LumaBlue * b);
            green[i] = (BlueChromaRed * r) + (BlueChromaGreen * g) + (BlueChromaBlue * b);
            blue[i] = (RedChromaRed * r) + (RedChromaGreen * g) + (RedChromaBlue * b);
        }
    }

    /// <summary>Reads one sample.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="position">The position of its first byte.</param>
    /// <param name="bytes">One for a sample of up to eight bits, two for more.</param>
    /// <returns>The sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadSample(ReadOnlySpan<byte> samples, int position, int bytes) =>
        bytes == 1 ? samples[position] : BinaryPrimitives.ReadUInt16BigEndian(samples[position..]);
}
