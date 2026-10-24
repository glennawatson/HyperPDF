// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Video;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that NV12 memory is split into the planes of a frame.</summary>
public sealed class Nv12PlanesTests
{
    /// <summary>The bytes of padding at the end of each row.</summary>
    private const int Padding = 5;

    /// <summary>The value written into the padding, which must never reach a frame.</summary>
    private const byte PaddingValue = 0xEE;

    /// <summary>The offset added to the value of the colour samples so Cb and Cr differ from luma.</summary>
    private const int ColourOffset = 100;

    /// <summary>The multiplier that makes a sample depend on its row.</summary>
    private const int RowStep = 37;

    /// <summary>The number of values a byte holds.</summary>
    private const int ByteRange = 256;

    /// <summary>The samples in a pair of Cb and Cr, and the number of luma samples to each colour sample across.</summary>
    private const int PairSize = 2;

    /// <summary>The rows a test picture has beyond its width, so it is not square.</summary>
    private const int HeightExtra = 3;

    /// <summary>The side of the picture used for the short memory check.</summary>
    private const int PictureSide = 16;

    /// <summary>The step between the values of the pairs split by the deinterleave check.</summary>
    private const int PatternStep = 7;

    /// <summary>The widths tested: whole vectors, a tail, odd and tiny.</summary>
    private static readonly int[] Widths = [16, 34, 35, 2, 1];

    /// <summary>Gets the widths and heights tested.</summary>
    /// <returns>The sizes.</returns>
    public static IEnumerable<Func<Size>> Sizes() => Widths.Select(static width => (Func<Size>)(() => new(width, width + HeightExtra)));

    /// <summary>One contiguous run of NV12 memory gives the same frame as the same rows in two runs.</summary>
    /// <param name="size">The size of the picture.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Sizes))]
    public async Task ContiguousAndSeparatePlanesGiveTheSameFrame(Size size)
    {
        var (luma, colour, lumaStride, colourStride) = Build(size);
        var joined = new byte[luma.Length + colour.Length];
        luma.CopyTo(joined, 0);
        colour.CopyTo(joined, luma.Length);
        var first = new VideoFrame(size.Width, size.Height, 0);
        var second = new VideoFrame(size.Width, size.Height, 0);

        Nv12Planes.Copy(joined, lumaStride, size.Height, first);
        Nv12Planes.Copy(luma, lumaStride, colour, colourStride, second);

        await Assert.That(second.Y.AsSpan().SequenceEqual(first.Y)).IsTrue();
        await Assert.That(second.U.AsSpan().SequenceEqual(first.U)).IsTrue();
        await Assert.That(second.V.AsSpan().SequenceEqual(first.V)).IsTrue();
    }

    /// <summary>Each sample lands in the plane and place the NV12 layout puts it, and the row padding is dropped.</summary>
    /// <param name="size">The size of the picture.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Sizes))]
    public async Task SamplesLandInTheirPlanes(Size size)
    {
        var (luma, colour, lumaStride, colourStride) = Build(size);
        var frame = new VideoFrame(size.Width, size.Height, 0);

        Nv12Planes.Copy(luma, lumaStride, colour, colourStride, frame);

        var mismatches = 0;
        for (var row = 0; row < size.Height; row++)
        {
            for (var column = 0; column < size.Width; column++)
            {
                mismatches += frame.Y[(row * size.Width) + column] == LumaSample(row, column) ? 0 : 1;
            }
        }

        for (var row = 0; row < frame.ChromaHeight; row++)
        {
            for (var column = 0; column < frame.ChromaWidth; column++)
            {
                var at = (row * frame.ChromaWidth) + column;
                mismatches += frame.U[at] == ColourSample(row, column, 0) ? 0 : 1;
                mismatches += frame.V[at] == ColourSample(row, column, 1) ? 0 : 1;
            }
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Memory that is too short for the picture is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortMemoryIsRefused()
    {
        var frame = new VideoFrame(PictureSide, PictureSide, 0);
        var luma = new byte[PictureSide * PictureSide];
        var colour = new byte[(PictureSide * (PictureSide / PairSize)) - 1];

        await Assert.That(() => Nv12Planes.Copy(luma, PictureSide, colour, PictureSide, frame)).Throws<ArgumentException>();
    }

    /// <summary>Splitting pairs gives a run of each sample, for lengths either side of a vector.</summary>
    /// <param name="count">The number of pairs.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(9)]
    [Arguments(33)]
    public async Task DeinterleaveSplitsPairs(int count)
    {
        var pairs = new byte[count * PairSize];
        for (var i = 0; i < pairs.Length; i++)
        {
            pairs[i] = (byte)(i * PatternStep);
        }

        var cb = new byte[count];
        var cr = new byte[count];

        Nv12Planes.Deinterleave(pairs, cb, cr);

        var mismatches = 0;
        for (var i = 0; i < count; i++)
        {
            mismatches += cb[i] == pairs[i * PairSize] && cr[i] == pairs[(i * PairSize) + 1] ? 0 : 1;
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Gets the luma sample at a place.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The sample.</returns>
    private static byte LumaSample(int row, int column) => (byte)(((row * RowStep) + column) % ByteRange);

    /// <summary>Gets a colour sample at a place.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <param name="channel">0 for Cb, 1 for Cr.</param>
    /// <returns>The sample.</returns>
    private static byte ColourSample(int row, int column, int channel) => (byte)((ColourOffset + (row * RowStep) + (column * PairSize) + channel) % ByteRange);

    /// <summary>Builds NV12 memory whose rows end in padding.</summary>
    /// <param name="size">The size of the picture.</param>
    /// <returns>The luma rows, the colour rows, and the stride of each.</returns>
    private static Nv12Memory Build(Size size)
    {
        var chromaWidth = (size.Width + 1) / PairSize;
        var chromaHeight = (size.Height + 1) / PairSize;
        var colourStride = (chromaWidth * PairSize) + Padding;
        var lumaStride = colourStride;
        var luma = new byte[lumaStride * size.Height];
        var colour = new byte[colourStride * chromaHeight];
        Array.Fill(luma, PaddingValue);
        Array.Fill(colour, PaddingValue);
        for (var row = 0; row < size.Height; row++)
        {
            for (var column = 0; column < size.Width; column++)
            {
                luma[(row * lumaStride) + column] = LumaSample(row, column);
            }
        }

        for (var row = 0; row < chromaHeight; row++)
        {
            for (var column = 0; column < chromaWidth; column++)
            {
                colour[(row * colourStride) + (column * PairSize)] = ColourSample(row, column, 0);
                colour[(row * colourStride) + (column * PairSize) + 1] = ColourSample(row, column, 1);
            }
        }

        return new(luma, colour, lumaStride, colourStride);
    }

    /// <summary>The size of a test picture.</summary>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    public readonly record struct Size(int Width, int Height);

    /// <summary>NV12 memory in two runs.</summary>
    /// <param name="Luma">The luma rows.</param>
    /// <param name="Colour">The colour rows.</param>
    /// <param name="LumaStride">The distance between luma rows.</param>
    /// <param name="ColourStride">The distance between colour rows.</param>
    private readonly record struct Nv12Memory(byte[] Luma, byte[] Colour, int LumaStride, int ColourStride);
}
