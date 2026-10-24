// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Segment termination of the JPEG 2000 entropy encoder (ISO 15444-1 D.6).</summary>
public sealed class JpxMqEncoderTests
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits after a 0xFF byte.</summary>
    private const int StuffedBits = 7;

    /// <summary>A byte of all ones.</summary>
    private const byte AllOnes = 0xFF;

    /// <summary>The stuffed zero bit then alternating padding.</summary>
    private const byte Padding = 0x2A;

    /// <summary>Seven ones after a 0xFF byte.</summary>
    private const byte SevenOnes = 0x7F;

    /// <summary>The expected segment of a raw byte of ones.</summary>
    private static readonly byte[] PaddedAllOnes = [AllOnes, Padding];

    /// <summary>The expected segment of a raw byte of ones then seven ones.</summary>
    private static readonly byte[] AllOnesThenSeven = [AllOnes, SevenOnes];

    /// <summary>A raw segment whose last byte is 0xFF ends with the stuffed byte of a zero bit and alternating padding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RawSegmentEndingInAllOnesGetsAlternatingPadding()
    {
        using var coder = new JpxMqEncoder();
        coder.StartRaw();
        for (var i = 0; i < ByteBits; i++)
        {
            coder.EncodeRaw(1);
        }

        coder.FinishRaw();

        await Assert.That(coder.Segment.ToArray()).IsEquivalentTo(PaddedAllOnes);
    }

    /// <summary>A byte after a 0xFF byte holds seven bits, so seven ones make 0x7F and no padding is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ByteAfterAllOnesHoldsSevenBits()
    {
        using var coder = new JpxMqEncoder();
        coder.StartRaw();
        for (var i = 0; i < ByteBits + StuffedBits; i++)
        {
            coder.EncodeRaw(1);
        }

        coder.FinishRaw();

        await Assert.That(coder.Segment.ToArray()).IsEquivalentTo(AllOnesThenSeven);
    }
}
