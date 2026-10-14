// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Tests of the extended generic template (T.88 sections 6.2.5.3 and 7.4.6.3, EXTTEMPLATE = 1, twelve adaptive pixels). Streams are
/// coded with generic template 0, then rewritten with the extended template flag and the 24-byte adaptive pixel field. Template 0's
/// adaptive pixels A1 to A4 take the nominal places of the extended pixels A10, A12, A9 and A11, so the same coded data decodes under both.
/// </summary>
public sealed class Jbig2ExtendedTemplateTests
{
    /// <summary>The extended template flag in the generic region flags.</summary>
    private const byte ExtendedFlag = 0x10;

    /// <summary>The bytes of a page information segment with its header: an 11-byte header and 19 bytes of data.</summary>
    private const int PageSegmentBytes = 30;

    /// <summary>The bytes of a segment header with no referred-to segments.</summary>
    private const int SegmentHeaderBytes = 11;

    /// <summary>The offset of the data length within a segment header.</summary>
    private const int LengthOffset = 7;

    /// <summary>The bytes of a region segment information field.</summary>
    private const int RegionInfoBytes = 17;

    /// <summary>The bytes of generic template 0's adaptive pixels.</summary>
    private const int Template0AtBytes = 8;

    /// <summary>The bytes of the extended template's adaptive pixels.</summary>
    private const int ExtendedAtBytes = 24;

    /// <summary>The offset of the generic region flags in a stream of a page information segment and one generic region.</summary>
    private const int FlagsOffset = PageSegmentBytes + SegmentHeaderBytes + RegionInfoBytes;

    /// <summary>The offset of the generic region's data length field.</summary>
    private const int LengthFieldOffset = PageSegmentBytes + LengthOffset;

    /// <summary>The bytes of adaptive pixels left in a truncated stream: half of the field.</summary>
    private const int PartialAtBytes = 12;

    /// <summary>The page the tests code.</summary>
    private static readonly Jbig2Size Page = new(120, 80);

    /// <summary>Template 0's moved adaptive pixels A1 to A4.</summary>
    private static readonly Jbig2AtPixels MovedTemplate0 = new(-8, 0, -5, -1, 4, -2, -6, -3);

    /// <summary>The extended pixels A9 to A12 that match <see cref="MovedTemplate0"/>: A9 is template 0's A3, A10 its A1, A11 its A4 and A12 its A2.</summary>
    private static readonly Jbig2AtPixels MovedThird = new(4, -2, -8, 0, -6, -3, -5, -1);

    /// <summary>The extended template with every adaptive pixel at its nominal place codes a page back to the same pixels.</summary>
    /// <param name="typical">Whether typical prediction is on.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task NominalAdaptivePixelsDecode(bool typical)
    {
        var rows = Jbig2TestPages.Create(Jbig2TestPageKind.Text, Page);
        var stream = ToExtended(CodeTemplate0(rows, new(0, typical, Jbig2AtPixels.Template0)), Jbig2ExtendedAtPixels.Nominal);
        var decoded = Jbig2TestPages.Decode(stream, null, Page);

        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Extended pixels moved from their nominal places decode to the same pixels as the template 0 pixels that share their context bits.</summary>
    /// <param name="typical">Whether typical prediction is on.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MovedAdaptivePixelsDecode(bool typical)
    {
        var rows = Jbig2TestPages.Create(Jbig2TestPageKind.Text, Page);
        var moved = new Jbig2ExtendedAtPixels(Jbig2ExtendedAtPixels.Nominal.First, Jbig2ExtendedAtPixels.Nominal.Second, MovedThird);
        var stream = ToExtended(CodeTemplate0(rows, new(0, typical, MovedTemplate0)), moved);
        var decoded = Jbig2TestPages.Decode(stream, null, Page);

        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>A stream that stops inside the 24-byte adaptive pixel field draws nothing of the region.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedAdaptivePixelsDrawNothing()
    {
        var rows = Jbig2TestPages.Create(Jbig2TestPageKind.Text, Page);
        var stream = ToExtended(CodeTemplate0(rows, new(0, false, Jbig2AtPixels.Template0)), Jbig2ExtendedAtPixels.Nominal);
        var decoded = Jbig2TestPages.Decode(stream[..(FlagsOffset + 1 + PartialAtBytes)], null, Page);

        await Assert.That(decoded is null || !decoded.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Codes a page as one generic region with template 0.</summary>
    /// <param name="rows">The page samples.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <returns>The page data: a page information segment and the region.</returns>
    private static byte[] CodeTemplate0(byte[] rows, in Jbig2GenericParameters parameters)
    {
        var page = default(PooledBuffer);
        var ink = default(PooledBuffer);
        try
        {
            Jbig2SegmentWriter.WritePageInformation(ref page, 0, Page.Width, Page.Height, true);
            Jbig2Ink.FromSamples(rows, Page.Width, Page.Height, ref ink);
            Jbig2GenericRegionWriter.Write(ref page, 1, new(ink.WrittenSpan, Page.Width, Page.Height), default, parameters, false, CancellationToken.None);
            return page.ToArray();
        }
        finally
        {
            ink.Dispose();
            page.Dispose();
        }
    }

    /// <summary>Rewrites the generic region of a template 0 stream to use the extended template.</summary>
    /// <param name="stream">The stream from <see cref="CodeTemplate0"/>.</param>
    /// <param name="at">The extended adaptive pixels to write.</param>
    /// <returns>The rewritten stream.</returns>
    private static byte[] ToExtended(byte[] stream, in Jbig2ExtendedAtPixels at)
    {
        const int DataStart = FlagsOffset + 1 + Template0AtBytes;
        var rewritten = new List<byte>(stream.Length + ExtendedAtBytes - Template0AtBytes);
        rewritten.AddRange(stream.AsSpan(0, FlagsOffset));
        rewritten.Add((byte)(stream[FlagsOffset] | ExtendedFlag));
        for (var i = 0; i < Jbig2ExtendedAtPixels.Count; i++)
        {
            rewritten.Add((byte)(sbyte)at.X(i));
            rewritten.Add((byte)(sbyte)at.Y(i));
        }

        rewritten.AddRange(stream.AsSpan(DataStart));
        var result = rewritten.ToArray();
        var length = BinaryPrimitives.ReadInt32BigEndian(result.AsSpan(LengthFieldOffset)) + ExtendedAtBytes - Template0AtBytes;
        BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(LengthFieldOffset), length);
        return result;
    }
}
