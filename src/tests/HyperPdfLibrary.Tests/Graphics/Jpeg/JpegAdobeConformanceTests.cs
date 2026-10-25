// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests the Adobe APP14 segment and colour transform rules of Adobe Technical Note #5116 and ISO 32000-2 Table 13.</summary>
public sealed class JpegAdobeConformanceTests
{
    /// <summary>The width of the pictures.</summary>
    private const int Width = 32;

    /// <summary>The height of the pictures.</summary>
    private const int Height = 16;

    /// <summary>The components of an ink picture.</summary>
    private const int Ink = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The offset of the marker after SOI.</summary>
    private const int MarkerOffset = 2;

    /// <summary>The offset of the length field.</summary>
    private const int LengthOffset = 4;

    /// <summary>The offset of the identifier.</summary>
    private const int IdentifierOffset = 6;

    /// <summary>The offset of the version.</summary>
    private const int VersionOffset = 11;

    /// <summary>The offset of the first flag word.</summary>
    private const int FlagsOffset = 13;

    /// <summary>The offset of the second flag word.</summary>
    private const int SecondFlagsOffset = 15;

    /// <summary>The components of a two-channel file.</summary>
    private const int Pair = 2;

    /// <summary>The highest quality.</summary>
    private const int BestQuality = 100;

    /// <summary>The offset of the transform byte.</summary>
    private const int TransformOffset = 17;

    /// <summary>The bytes of the Adobe segment with its marker.</summary>
    private const int SegmentBytes = 16;

    /// <summary>The length field of the Adobe segment: 14.</summary>
    private const int SegmentLength = 14;

    /// <summary>The version number of the DCT filters, X'65.</summary>
    private const int FilterVersion = 0x65;

    /// <summary>The application marker APP14.</summary>
    private const byte App14 = 0xEE;

    /// <summary>The Adobe transform value for YCCK.</summary>
    private const int YcckTransform = 2;

    /// <summary>The largest difference of a stored sample from the formula of section 13, after quantization at quality 100.</summary>
    private const int Tolerance = 3;

    /// <summary>The offset that centres chroma.</summary>
    private const double ChromaCenter = 128;

    /// <summary>The weight of red in luma.</summary>
    private const double LumaRed = 0.299;

    /// <summary>The weight of green in luma.</summary>
    private const double LumaGreen = 0.587;

    /// <summary>The weight of blue in luma.</summary>
    private const double LumaBlue = 0.114;

    /// <summary>The weight of red in blue chroma.</summary>
    private const double BlueFromRed = -0.168736;

    /// <summary>The weight of green in blue chroma.</summary>
    private const double BlueFromGreen = -0.331264;

    /// <summary>The weight of blue in blue chroma, and of red in red chroma.</summary>
    private const double Half = 0.5;

    /// <summary>The weight of green in red chroma.</summary>
    private const double RedFromGreen = -0.4186876;

    /// <summary>The weight of blue in red chroma.</summary>
    private const double RedFromBlue = -0.08131241;

    /// <summary>The CMYK files carry an Adobe segment of 14 bytes with the identifier, version X'65, zero flags and the transform code.</summary>
    /// <param name="transform">Whether the file is YCCK.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task EncoderWritesTheAdobeSegment(bool transform)
    {
        var jpeg = Encode(new() { TransformCmyk = transform });

        await Assert.That(jpeg[MarkerOffset]).IsEqualTo(JpegMarkers.MarkerPrefix);
        await Assert.That(jpeg[MarkerOffset + 1]).IsEqualTo(App14);
        await Assert.That((jpeg[LengthOffset] << ByteBits) | jpeg[LengthOffset + 1]).IsEqualTo(SegmentLength);
        await Assert.That(AdobeTag(jpeg)).IsTrue();
        await Assert.That((jpeg[VersionOffset] << ByteBits) | jpeg[VersionOffset + 1]).IsEqualTo(FilterVersion);
        await Assert.That((jpeg[FlagsOffset] << ByteBits) | jpeg[FlagsOffset + 1]).IsEqualTo(0);
        await Assert.That((jpeg[SecondFlagsOffset] << ByteBits) | jpeg[SecondFlagsOffset + 1]).IsEqualTo(0);
        await Assert.That(jpeg[TransformOffset]).IsEqualTo((byte)(transform ? YcckTransform : 0));
    }

    /// <summary>The YCCK samples are the RGB-to-YCC transform of 255 minus each stored colour sample, with the black sample unchanged (section 13.1).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task YcckFollowsTheCmykToYcckTransform()
    {
        var ink = JpegEncodeSamples.Patches(PdfJpegPixelFormat.Cmyk32, Width, Height);
        var jpeg = PdfJpegEncoder.Encode(ink, PdfJpegPixelFormat.Cmyk32, Width, Height, new() { Quality = BestQuality, Subsampling = PdfJpegSubsampling.Chroma444 }, CancellationToken.None);
        var raw = new byte[Width * Height * Ink];

        await Assert.That(JpegDecoder.TryDecode(jpeg, false, raw)).IsTrue();

        // The file stores 255 minus the ink; the transform takes 255 minus that stored value as its red, green and blue.
        var worst = 0;
        for (var pixel = 0; pixel < Width * Height; pixel++)
        {
            var offset = pixel * Ink;
            var r = ink[offset];
            var g = ink[offset + 1];
            var b = ink[offset + MarkerOffset];
            worst = Math.Max(worst, Math.Abs(raw[offset] - Round((LumaRed * r) + (LumaGreen * g) + (LumaBlue * b))));
            worst = Math.Max(worst, Math.Abs(raw[offset + 1] - Round((BlueFromRed * r) + (BlueFromGreen * g) + (Half * b) + ChromaCenter)));
            worst = Math.Max(worst, Math.Abs(raw[offset + MarkerOffset] - Round((Half * r) + (RedFromGreen * g) + (RedFromBlue * b) + ChromaCenter)));
            worst = Math.Max(worst, Math.Abs(raw[offset + Ink - 1] - (byte.MaxValue - ink[offset + Ink - 1])));
        }

        await Assert.That(worst).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>Without the Adobe segment a four-component file is not transformed unless /ColorTransform is 1 (the DCTDecode default); with the segment, the segment decides.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FourComponentsWithoutTheSegmentFollowTheDictionary()
    {
        var jpeg = Encode(new() { TransformCmyk = true });
        var bare = new byte[jpeg.Length - SegmentBytes];
        jpeg.AsSpan(0, MarkerOffset).CopyTo(bare);
        jpeg.AsSpan(MarkerOffset + SegmentBytes).CopyTo(bare.AsSpan(MarkerOffset));

        await Assert.That(JpegMarkers.TryReadInfo(jpeg, out var marked)).IsTrue();
        await Assert.That(JpegMarkers.TryReadInfo(bare, out var unmarked)).IsTrue();
        await Assert.That(marked.UsesColorTransform(0)).IsTrue();
        await Assert.That(unmarked.HasAdobe).IsFalse();
        await Assert.That(unmarked.UsesColorTransform(JpegInfo.NoColorTransform)).IsFalse();
        await Assert.That(unmarked.UsesColorTransform(0)).IsFalse();
        await Assert.That(unmarked.UsesColorTransform(1)).IsTrue();
    }

    /// <summary>Transform codes other than zero, and any flags, are accepted; the segment of another application is ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherAppSegmentsAndFlagsAreTolerated()
    {
        var jpeg = Encode(new() { TransformCmyk = true });
        var foreign = (byte[])jpeg.Clone();

        // A segment whose identifier is not "Adobe" is skipped, so the file reads as having no Adobe marker.
        foreign[IdentifierOffset] = (byte)'X';
        var flagged = (byte[])jpeg.Clone();
        flagged[FlagsOffset] = byte.MaxValue;
        flagged[SecondFlagsOffset] = byte.MaxValue;

        await Assert.That(JpegMarkers.TryReadInfo(foreign, out var other)).IsTrue();
        await Assert.That(JpegMarkers.TryReadInfo(flagged, out var withFlags)).IsTrue();
        await Assert.That(other.HasAdobe).IsFalse();
        await Assert.That(withFlags.AdobeTransform).IsEqualTo(YcckTransform);
    }

    /// <summary>One- and two-component files ignore /ColorTransform and any Adobe code (Table 13).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FewComponentsIgnoreColorTransform()
    {
        var one = new JpegInfo(1, 1, 1, ByteBits, JpegProcess.Sequential, 1, false);
        var two = new JpegInfo(1, 1, Pair, ByteBits, JpegProcess.Sequential, -1, false);

        await Assert.That(one.UsesColorTransform(1)).IsFalse();
        await Assert.That(two.UsesColorTransform(1)).IsFalse();
    }

    /// <summary>Encodes the CMYK test picture.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The file.</returns>
    private static byte[] Encode(PdfJpegOptions options) =>
        PdfJpegEncoder.Encode(JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Cmyk32, Width, Height), PdfJpegPixelFormat.Cmyk32, Width, Height, options, CancellationToken.None);

    /// <summary>Rounds a value to a sample.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The sample.</returns>
    private static int Round(double value) => (int)Math.Min(Math.Max(0, Math.Floor(value + Half)), byte.MaxValue);

    /// <summary>Determines whether the identifier of the segment is "Adobe".</summary>
    /// <param name="jpeg">The file.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool AdobeTag(byte[] jpeg)
    {
        const string tag = "Adobe";
        for (var i = 0; i < tag.Length; i++)
        {
            if (jpeg[IdentifierOffset + i] != tag[i])
            {
                return false;
            }
        }

        return true;
    }
}
