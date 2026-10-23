// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Checks that damaged and hostile JBIG2 data never throws or hangs, and keeps what decoded.</summary>
public sealed class Jbig2RobustnessTests
{
    /// <summary>The truncation points tried per small sample.</summary>
    private const int Cuts = 8;

    /// <summary>The truncation points tried per large sample.</summary>
    private const int LargeCuts = 2;

    /// <summary>The damaged copies tried per sample.</summary>
    private const int Corruptions = 24;

    /// <summary>The most bytes changed in one damaged copy.</summary>
    private const int MaxChanges = 3;

    /// <summary>The seed of the damage.</summary>
    private const int Seed = 20_261_009;

    /// <summary>The pixels above which a sample is only truncated twice.</summary>
    private const long LargePage = 1_000_000;

    /// <summary>The side of the hand-built pages.</summary>
    private const int PageSide = 64;

    /// <summary>The bytes in a row of a hand-built page.</summary>
    private const int PageStride = 8;

    /// <summary>The largest region side JBIG2 allows.</summary>
    private const int HugeSide = 65_535;

    /// <summary>The most symbols a dictionary may define.</summary>
    private const int ManySymbols = 1 << 20;

    /// <summary>The adaptive pixel bytes of generic template 0.</summary>
    private const int Template0AtBytes = 8;

    /// <summary>The highest pattern index allowed.</summary>
    private const int MaxGray = 65_535;

    /// <summary>The widest pattern.</summary>
    private const byte WidePattern = 0xFF;

    /// <summary>The fraction of the generic sample kept when it is cut short.</summary>
    private const int KeepPercent = 70;

    /// <summary>The number that turns a percentage into a fraction.</summary>
    private const int Percent = 100;

    /// <summary>The rows compared after a cut, which decode before the data ends.</summary>
    private const int EarlyRows = 4;

    /// <summary>A byte with every pixel white in the output.</summary>
    private const byte White = 0xFF;

    /// <summary>The leading bytes of each sample, which hold the first segment headers and region fields, that are set to extreme values.</summary>
    private const int HeaderBytes = 48;

    /// <summary>Gets the values tried in each of the leading bytes.</summary>
    private static ReadOnlySpan<byte> ExtremeBytes => [0x00, 0x7F, 0x80, 0xFF];

    /// <summary>Every sample, cut short anywhere, decodes without throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedSamplesDoNotThrow()
    {
        var decodes = 0;
        foreach (var sample in Jbig2Samples.All)
        {
            var cuts = (long)sample.Width * sample.Height > LargePage ? LargeCuts : Cuts;
            for (var i = 0; i < cuts; i++)
            {
                DecodeTruncated(sample, sample.Data.Length * i / cuts);
                decodes++;
            }
        }

        await Assert.That(decodes).IsGreaterThan(Jbig2Samples.All.Count);
    }

    /// <summary>Every small sample with a few bytes changed decodes without throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedSamplesDoNotThrow()
    {
        var random = new JpegTestRandom(Seed);
        var decodes = 0;
        foreach (var sample in Jbig2Samples.All)
        {
            if ((long)sample.Width * sample.Height > LargePage)
            {
                continue;
            }

            for (var i = 0; i < Corruptions; i++)
            {
                DecodeDamaged(sample, random);
                decodes++;
            }
        }

        await Assert.That(decodes).IsGreaterThan(Corruptions);
    }

    /// <summary>Every small sample with each of its first bytes set to an extreme value decodes without throwing: this reaches every field of the first segment headers and region headers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedHeadersDoNotThrow()
    {
        var decodes = 0;
        foreach (var sample in Jbig2Samples.All)
        {
            if ((long)sample.Width * sample.Height > LargePage)
            {
                continue;
            }

            for (var offset = 0; offset < Math.Min(HeaderBytes, sample.Data.Length); offset++)
            {
                foreach (var value in ExtremeBytes)
                {
                    var data = sample.Data.ToArray();
                    data[offset] = value;
                    var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
                    _ = Jbig2Decoder.TryDecode(data, sample.Globals, sample.Width, sample.Height, rows);
                    decodes++;
                }
            }
        }

        await Assert.That(decodes).IsGreaterThan(HeaderBytes);
    }

    /// <summary>A generic region whose data ends early keeps the rows decoded before the end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedGenericRegionKeepsRows()
    {
        var sample = Jbig2Samples.GenericTemplate2;
        var stride = Jbig2Decoder.GetRowBytes(sample.Width);
        var full = new byte[stride * sample.Height];
        var partial = new byte[stride * sample.Height];
        _ = Jbig2Decoder.TryDecode(sample.Data, sample.Width, sample.Height, full);

        var decoded = Jbig2Decoder.TryDecode(sample.Data.AsSpan(0, sample.Data.Length * KeepPercent / Percent), sample.Width, sample.Height, partial);

        await Assert.That(decoded).IsTrue();
        await Assert.That(partial.AsSpan(0, stride * EarlyRows).SequenceEqual(full.AsSpan(0, stride * EarlyRows))).IsTrue();
        await Assert.That(partial.AsSpan().ContainsAnyExcept(White)).IsTrue();
    }

    /// <summary>Segments that ask for huge regions, symbol counts or patterns fail fast and leave the page white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HostileSegmentsFailFast()
    {
        byte[][] streams = [HugeGenericRegion(), ManyNewSymbols(), HugePatternDictionary(), HugeHalftoneGrid(), InvertedTable(), EndlessTextRegion()];
        foreach (var stream in streams)
        {
            var rows = new byte[PageStride * PageSide];

            var decoded = Jbig2Decoder.TryDecode(stream, PageSide, PageSide, rows);

            await Assert.That(decoded).IsTrue();
            await Assert.That(rows.AsSpan().ContainsAnyExcept(White)).IsFalse();
        }
    }

    /// <summary>Decodes a sample cut to a length.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="length">The bytes kept.</param>
    private static void DecodeTruncated(Jbig2Sample sample, int length)
    {
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        _ = Jbig2Decoder.TryDecode(sample.Data.AsSpan(0, length), sample.Globals, sample.Width, sample.Height, rows);
    }

    /// <summary>Decodes a sample with a few random bytes changed.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="random">The source of damage.</param>
    private static void DecodeDamaged(Jbig2Sample sample, JpegTestRandom random)
    {
        var data = sample.Data.ToArray();
        var changes = random.Next(1, MaxChanges + 1);
        for (var i = 0; i < changes; i++)
        {
            data[random.Next(0, data.Length)] = (byte)random.Next(0, byte.MaxValue + 1);
        }

        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        _ = Jbig2Decoder.TryDecode(data, sample.Globals, sample.Width, sample.Height, rows);
    }

    /// <summary>Builds a page with a generic region of the largest size.</summary>
    /// <returns>The stream.</returns>
    private static byte[] HugeGenericRegion()
    {
        var data = Jbig2StreamBuilder.RegionInfo(HugeSide, HugeSide);
        data.Add(0);
        data.AddRange(new byte[Template0AtBytes]);
        return new Jbig2StreamBuilder().Page(PageSide, PageSide).Segment(Jbig2StreamBuilder.ImmediateGeneric, [], [.. data]).ToArray();
    }

    /// <summary>Builds a page with a symbol dictionary that claims the most symbols but has no data.</summary>
    /// <returns>The stream.</returns>
    private static byte[] ManyNewSymbols()
    {
        var data = new List<byte> { 0, 0 };
        data.AddRange(new byte[Template0AtBytes]);
        Jbig2StreamBuilder.AddInt(data, ManySymbols);
        Jbig2StreamBuilder.AddInt(data, ManySymbols);
        return new Jbig2StreamBuilder().Page(PageSide, PageSide).Segment(Jbig2StreamBuilder.SymbolDictionary, [], [.. data]).ToArray();
    }

    /// <summary>Builds a page with a pattern dictionary whose collective bitmap is too wide.</summary>
    /// <returns>The stream.</returns>
    private static byte[] HugePatternDictionary()
    {
        var data = new List<byte> { 0, WidePattern, WidePattern };
        Jbig2StreamBuilder.AddInt(data, MaxGray);
        return new Jbig2StreamBuilder().Page(PageSide, PageSide).Segment(Jbig2StreamBuilder.PatternDictionary, [], [.. data]).ToArray();
    }

    /// <summary>Builds a page with a halftone region whose grid is the largest size and that has no patterns.</summary>
    /// <returns>The stream.</returns>
    private static byte[] HugeHalftoneGrid()
    {
        var data = Jbig2StreamBuilder.RegionInfo(PageSide, PageSide);
        data.Add(0);
        Jbig2StreamBuilder.AddInt(data, HugeSide);
        Jbig2StreamBuilder.AddInt(data, HugeSide);
        Jbig2StreamBuilder.AddInt(data, 0);
        Jbig2StreamBuilder.AddInt(data, 0);
        Jbig2StreamBuilder.AddShort(data, 0);
        Jbig2StreamBuilder.AddShort(data, 0);
        return new Jbig2StreamBuilder().Page(PageSide, PageSide).Segment(Jbig2StreamBuilder.ImmediateHalftone, [], [.. data]).ToArray();
    }

    /// <summary>Builds a page with a Huffman table segment whose lowest value is above its highest.</summary>
    /// <returns>The stream.</returns>
    private static byte[] InvertedTable()
    {
        var data = new List<byte> { 0 };
        Jbig2StreamBuilder.AddInt(data, PageSide);
        Jbig2StreamBuilder.AddInt(data, -PageSide);
        return new Jbig2StreamBuilder().Page(PageSide, PageSide).Segment(Jbig2StreamBuilder.Table, [], [.. data]).ToArray();
    }

    /// <summary>Builds a page with a text region that claims four billion instances.</summary>
    /// <returns>The stream.</returns>
    private static byte[] EndlessTextRegion()
    {
        var data = Jbig2StreamBuilder.RegionInfo(PageSide, PageSide);
        Jbig2StreamBuilder.AddShort(data, 0);
        Jbig2StreamBuilder.AddInt(data, -1);
        return new Jbig2StreamBuilder().Page(PageSide, PageSide).Segment(Jbig2StreamBuilder.ImmediateText, [], [.. data]).ToArray();
    }
}
