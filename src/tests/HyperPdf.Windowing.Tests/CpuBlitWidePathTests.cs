// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.Rendering.Cpu;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that the scaler gives the same pixels whether it stretches a line several pixels at once or one at a time. A
/// frame too narrow for the wide path is scaled one pixel at a time, so a window of that width moved across the picture gives
/// what every pixel of a wide frame must be.
/// </summary>
public sealed class CpuBlitWidePathTests
{
    /// <summary>The source picture's width in pixels.</summary>
    private const int SourceWidth = 37;

    /// <summary>The source picture's height in pixels.</summary>
    private const int SourceHeight = 9;

    /// <summary>The height the picture is drawn at.</summary>
    private const int DrawnHeight = 13;

    /// <summary>The width of the narrow frame, less than the pixels the wide path takes at once.</summary>
    private const int NarrowWidth = 3;

    /// <summary>The bytes of one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The seed of the picture's pixels, so each run draws the same picture.</summary>
    private const int Seed = 7919;

    /// <summary>The multiplier of the step from one byte of the picture to the next.</summary>
    private const int Multiplier = 1_103_515_245;

    /// <summary>The amount added in the step from one byte of the picture to the next.</summary>
    private const int Increment = 12_345;

    /// <summary>The shift that takes the byte used from the stepped value.</summary>
    private const int HighByteShift = 16;

    /// <summary>The first source column of the flat band, where neighbouring pixels are the same.</summary>
    private const int FlatStart = 12;

    /// <summary>The source column after the flat band.</summary>
    private const int FlatEnd = 24;

    /// <summary>The source row that the rows after it repeat.</summary>
    private const int FirstRepeatedRow = 3;

    /// <summary>The rows that repeat the row before them.</summary>
    private const int RepeatedRows = 2;

    /// <summary>The colour of the flat band.</summary>
    private const uint Paper = 0xFFF4F1E8;

    /// <summary>A picture drawn wider or narrower than it is has the same pixels from the wide path as from the pixel by pixel one.</summary>
    /// <param name="drawnWidth">The width the picture is drawn at.</param>
    /// <param name="smooth">Whether neighbouring pixels are mixed, or the nearest one is picked.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(101, true)]
    [Arguments(64, true)]
    [Arguments(41, true)]
    [Arguments(23, true)]
    [Arguments(101, false)]
    [Arguments(64, false)]
    [Arguments(41, false)]
    [Arguments(23, false)]
    public async Task WidePathMatchesPixelByPixel(int drawnWidth, bool smooth)
    {
        var source = NewPicture();
        var sourceGrid = new PixelGrid(SourceWidth, SourceHeight, SourceWidth * BytesPerPixel);
        var wideGrid = new PixelGrid(drawnWidth, DrawnHeight, drawnWidth * BytesPerPixel);
        var wide = new byte[wideGrid.Stride * DrawnHeight];
        CpuBlit.Draw(wide, wideGrid, source, sourceGrid, new(0, 0, drawnWidth, DrawnHeight, smooth, true));

        var narrowGrid = new PixelGrid(NarrowWidth, DrawnHeight, NarrowWidth * BytesPerPixel);
        var narrow = new byte[narrowGrid.Stride * DrawnHeight];
        var differences = 0;
        for (var left = 0; left <= drawnWidth - NarrowWidth; left++)
        {
            CpuBlit.Draw(narrow, narrowGrid, source, sourceGrid, new(-left, 0, drawnWidth, DrawnHeight, smooth, true));
            differences += CountDifferences(wide, wideGrid, narrow, narrowGrid, left);
        }

        await Assert.That(differences).IsEqualTo(0);
    }

    /// <summary>Counts the bytes of a narrow frame that differ from the same pixels of the wide one.</summary>
    /// <param name="wide">The wide frame.</param>
    /// <param name="wideGrid">The wide frame's shape.</param>
    /// <param name="narrow">The narrow frame.</param>
    /// <param name="narrowGrid">The narrow frame's shape.</param>
    /// <param name="left">The column of the wide frame the narrow one starts at.</param>
    /// <returns>The number of bytes that differ.</returns>
    private static int CountDifferences(byte[] wide, in PixelGrid wideGrid, byte[] narrow, in PixelGrid narrowGrid, int left)
    {
        var differences = 0;
        for (var y = 0; y < narrowGrid.Height; y++)
        {
            var expected = narrow.AsSpan(y * narrowGrid.Stride, narrowGrid.Stride);
            var actual = wide.AsSpan((y * wideGrid.Stride) + (left * BytesPerPixel), narrowGrid.Stride);
            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    differences++;
                }
            }
        }

        return differences;
    }

    /// <summary>Makes a picture of unlike pixels with a flat band through it.</summary>
    /// <returns>The picture's BGRA pixels.</returns>
    private static byte[] NewPicture()
    {
        var pixels = new byte[SourceWidth * SourceHeight * BytesPerPixel];
        var value = Seed;
        for (var i = 0; i < pixels.Length; i++)
        {
            // A fixed run of unlike bytes, so each run draws the same picture.
            value = unchecked((value * Multiplier) + Increment);
            pixels[i] = (byte)(value >> HighByteShift);
        }

        for (var y = 0; y < SourceHeight; y++)
        {
            for (var x = FlatStart; x < FlatEnd; x++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(pixels.AsSpan(((y * SourceWidth) + x) * BytesPerPixel, BytesPerPixel), Paper);
            }
        }

        // Rows that repeat, as rows of paper do, take the scaler's copy of the line before.
        const int rowBytes = SourceWidth * BytesPerPixel;
        for (var copy = 1; copy <= RepeatedRows; copy++)
        {
            pixels.AsSpan(FirstRepeatedRow * rowBytes, rowBytes).CopyTo(pixels.AsSpan((FirstRepeatedRow + copy) * rowBytes, rowBytes));
        }

        return pixels;
    }
}
