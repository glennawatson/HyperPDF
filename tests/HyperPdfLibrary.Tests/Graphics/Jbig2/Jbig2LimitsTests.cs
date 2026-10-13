// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Tests the JBIG2 size limits and the segment header bounds, just under and just over each.</summary>
public sealed class Jbig2LimitsTests
{
    /// <summary>A segment number whose references take one byte each.</summary>
    private const int SmallNumber = 5;

    /// <summary>A segment number whose references take four bytes each.</summary>
    private const int LargeNumber = 0x100000;

    /// <summary>The page association of the test segments.</summary>
    private const byte Page = 1;

    /// <summary>The bits the short referred-segment count is shifted by.</summary>
    private const int CountShift = 5;

    /// <summary>The top three bits of the long form of the referred-segment count.</summary>
    private const uint LongForm = 0xE0000000;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The side of the bitmaps in the offset test.</summary>
    private const int Side = 8;

    /// <summary>The offset that leaves one pixel of the source on the target's top left corner.</summary>
    private const int ReachBack = 1 - Side;

    /// <summary>The half of the largest 64-bit offset, far below the target.</summary>
    private const long FarBelowOffset = long.MaxValue / 2;

    /// <summary>The widest region is accepted when the area fits, and one pixel wider is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RegionSideLimitIsExact()
    {
        const long area = Jbig2Limits.MaxRegionPixels / Jbig2Limits.MaxImageSide;

        await Assert.That(Jbig2Limits.IsValidSize(Jbig2Limits.MaxImageSide, area)).IsTrue();
        await Assert.That(Jbig2Limits.IsValidSize(Jbig2Limits.MaxImageSide + 1, 1)).IsFalse();
        await Assert.That(Jbig2Limits.IsValidSize(1, Jbig2Limits.MaxImageSide + 1)).IsFalse();
    }

    /// <summary>The pixel budget is exact: a region of exactly the budget is accepted, one pixel more is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RegionAreaLimitIsExact()
    {
        const long side = 16_384;

        await Assert.That(Jbig2Limits.IsValidSize(side, side)).IsTrue();
        await Assert.That(Jbig2Limits.IsValidSize(side + 1, side)).IsFalse();
        await Assert.That(Jbig2Limits.IsValidSize(0, 1)).IsFalse();
        await Assert.That(Jbig2Limits.IsValidSize(1, -1)).IsFalse();
    }

    /// <summary>A segment may refer to as many lower segments as exist, and no more.</summary>
    /// <param name="number">The segment number.</param>
    /// <param name="count">The referred-to segment count.</param>
    /// <param name="accepted">Whether the header parses.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SmallNumber, 4, true)]
    [Arguments(2, 2, true)]
    [Arguments(2, 3, false)]
    [Arguments(0, 1, false)]
    public async Task ShortCountIsBoundedByTheSegmentNumber(int number, int count, bool accepted)
    {
        var header = new List<byte> { 0, 0, 0, (byte)number, 0, (byte)(count << CountShift) };
        for (var i = 0; i < count; i++)
        {
            header.Add((byte)Math.Max(0, number - 1 - i));
        }

        header.AddRange([Page, 0, 0, 0, 0]);
        var reader = new Jbig2Reader(header.ToArray());

        await Assert.That(Jbig2Segment.Parse(ref reader) is not null).IsEqualTo(accepted);
    }

    /// <summary>The long form counts the references against both the segment number and the bytes left.</summary>
    /// <param name="number">The segment number.</param>
    /// <param name="count">The referred-to segment count.</param>
    /// <param name="dataBytes">The reference bytes supplied after the count and retention flags.</param>
    /// <param name="accepted">Whether the header parses.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SmallNumber, 3, 3, true)]
    [Arguments(SmallNumber, 3, 2, false)]
    [Arguments(2, 3, 3, false)]
    [Arguments(LargeNumber, 100_000, 8, false)]
    public async Task LongCountIsBoundedByNumberAndData(int number, int count, int dataBytes, bool accepted)
    {
        var header = new List<byte>();
        AddBigEndian(header, (uint)number);
        header.Add(0);
        AddBigEndian(header, LongForm | (uint)count);
        header.AddRange(new byte[(count + ByteBits) / ByteBits]);
        header.AddRange(new byte[dataBytes]);
        header.AddRange([Page, 0, 0, 0, 0]);
        var reader = new Jbig2Reader(header.ToArray());

        await Assert.That(Jbig2Segment.Parse(ref reader) is not null).IsEqualTo(accepted);
    }

    /// <summary>A bitmap placed at an offset far outside the target changes nothing and does not overflow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FarOffsetsComposeNothing()
    {
        using var target = Jbig2Bitmap.Create(Side, Side)!;
        using var source = Jbig2Bitmap.Create(Side, Side)!;
        source.Fill(true);

        var farRight = Jbig2Composer.Compose(target, source.View, int.MaxValue, 0, Jbig2ComposeOperator.Or);
        var farLeft = Jbig2Composer.Compose(target, source.View, int.MinValue, 0, Jbig2ComposeOperator.Or);
        var farBelow = Jbig2Composer.Compose(target, source.View, 0, FarBelowOffset, Jbig2ComposeOperator.Or);
        var corner = Jbig2Composer.Compose(target, source.View, ReachBack, ReachBack, Jbig2ComposeOperator.Or);

        await Assert.That(farRight).IsEqualTo(0);
        await Assert.That(farLeft).IsEqualTo(0);
        await Assert.That(farBelow).IsEqualTo(0);
        await Assert.That(corner).IsEqualTo(1);
    }

    /// <summary>Appends a 32-bit value, most significant byte first.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="value">The value.</param>
    private static void AddBigEndian(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }
}
