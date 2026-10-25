// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests the JFIF rules of the ITU-T Recommendation for JFIF (ISO/IEC 10918-5): the APP0 segment, its extensions, and the YCbCr conversion.</summary>
public sealed class JpegJfifConformanceTests
{
    /// <summary>The width of the pictures.</summary>
    private const int Width = 24;

    /// <summary>The height of the pictures.</summary>
    private const int Height = 16;

    /// <summary>The components of a colour picture.</summary>
    private const int Colour = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of the JFIF segment with its marker: marker, length field and fourteen bytes.</summary>
    private const int JfifSegmentBytes = 18;

    /// <summary>The length field of a JFIF segment with no thumbnail: 16.</summary>
    private const int JfifLength = 16;

    /// <summary>The version the encoder writes, 1.02.</summary>
    private const int JfifVersion = 0x0102;

    /// <summary>The offset of the APP0 marker: straight after SOI.</summary>
    private const int AppMarkerOffset = 2;

    /// <summary>The offset of the APP0 length field.</summary>
    private const int AppLengthOffset = 4;

    /// <summary>The offset of the identifier in the file: SOI, marker and length field.</summary>
    private const int IdentifierOffset = 6;

    /// <summary>The offset of the version in the file.</summary>
    private const int VersionOffset = 11;

    /// <summary>The offset of the units byte.</summary>
    private const int UnitsOffset = 13;

    /// <summary>The offset of the horizontal density.</summary>
    private const int DensityOffset = 14;

    /// <summary>The offset of the horizontal thumbnail size.</summary>
    private const int ThumbnailOffset = 18;

    /// <summary>The offset of the first component id after the start of the frame marker.</summary>
    private const int ComponentIdOffset = 10;

    /// <summary>The bytes of a component entry in a frame header.</summary>
    private const int ComponentEntryBytes = 3;

    /// <summary>The largest difference from the formulas of clause 7, which give four decimal places.</summary>
    private const int ConversionTolerance = 1;

    /// <summary>The step of the sample grid.</summary>
    private const int GridStep = 51;

    /// <summary>The offset that centres chroma.</summary>
    private const double ChromaCenter = 128;

    /// <summary>The weight of red in luma.</summary>
    private const double LumaRed = 0.299;

    /// <summary>The weight of green in luma.</summary>
    private const double LumaGreen = 0.587;

    /// <summary>The weight of blue in luma.</summary>
    private const double LumaBlue = 0.114;

    /// <summary>The weight of red in blue chroma.</summary>
    private const double BlueFromRed = -0.1687;

    /// <summary>The weight of green in blue chroma.</summary>
    private const double BlueFromGreen = -0.3313;

    /// <summary>The weight of blue in blue chroma, and of red in red chroma.</summary>
    private const double Half = 0.5;

    /// <summary>The weight of green in red chroma.</summary>
    private const double RedFromGreen = -0.4187;

    /// <summary>The weight of blue in red chroma.</summary>
    private const double RedFromBlue = -0.0813;

    /// <summary>The weight of red chroma in red.</summary>
    private const double RedFromCr = 1.402;

    /// <summary>The weight of blue chroma in green.</summary>
    private const double GreenFromCb = -0.3441;

    /// <summary>The weight of red chroma in green.</summary>
    private const double GreenFromCr = -0.7141;

    /// <summary>The weight of blue chroma in blue.</summary>
    private const double BlueFromCb = 1.772;

    /// <summary>The side of the thumbnails.</summary>
    private const int ThumbnailSide = 2;

    /// <summary>The bytes of the three-byte-per-pixel thumbnail.</summary>
    private const int ThumbnailBytes = ThumbnailSide * ThumbnailSide * Colour;

    /// <summary>The side of the embedded thumbnail picture.</summary>
    private const int EmbeddedSide = 16;

    /// <summary>The bytes of a colour palette of 256 entries.</summary>
    private const int PaletteBytes = 768;

    /// <summary>The bytes of an extension segment's length field and code that count towards its length, besides the data: length, identifier and code.</summary>
    private const int ExtensionOverhead = 8;

    /// <summary>The offset of the length's low byte in a segment.</summary>
    private const int LengthLowOffset = 3;

    /// <summary>The JFXX thumbnail extension that holds a baseline JPEG (clause 10.3).</summary>
    private const byte JpegThumbnail = 0x10;

    /// <summary>The JFXX thumbnail extension that holds an eight-bit palette picture (clause 10.4).</summary>
    private const byte PaletteThumbnail = 0x11;

    /// <summary>The JFXX thumbnail extension that holds a 24-bit RGB picture (clause 10.5).</summary>
    private const byte RgbThumbnail = 0x13;

    /// <summary>The application marker APP0.</summary>
    private const byte App0 = 0xE0;

    /// <summary>The encoder writes a conforming APP0 segment straight after SOI, with version 1.02, a pixel aspect of one and no thumbnail.</summary>
    /// <param name="format">The pixel format; gray and colour files both carry JFIF.</param>
    /// <param name="components">The component count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpegPixelFormat.Gray8, 1)]
    [Arguments(PdfJpegPixelFormat.Rgb24, Colour)]
    public async Task EncoderWritesAConformingJfifSegment(PdfJpegPixelFormat format, int components)
    {
        var jpeg = PdfJpegEncoder.Encode(JpegEncodeSamples.Pixels(format, Width, Height), format, Width, Height, PdfJpegOptions.Default, CancellationToken.None);

        await Assert.That(jpeg[0]).IsEqualTo(JpegMarkers.MarkerPrefix);
        await Assert.That(jpeg[1]).IsEqualTo(JpegMarkers.StartOfImage);
        await Assert.That(jpeg[AppMarkerOffset]).IsEqualTo(JpegMarkers.MarkerPrefix);
        await Assert.That(jpeg[AppMarkerOffset + 1]).IsEqualTo(App0);
        await Assert.That((jpeg[AppLengthOffset] << ByteBits) | jpeg[AppLengthOffset + 1]).IsEqualTo(JfifLength);
        await Assert.That(IdentifierIs(jpeg, IdentifierOffset, 'J', 'F', 'I', 'F')).IsTrue();
        await Assert.That((jpeg[VersionOffset] << ByteBits) | jpeg[VersionOffset + 1]).IsEqualTo(JfifVersion);
        await Assert.That(jpeg[UnitsOffset]).IsEqualTo((byte)0);
        await Assert.That((jpeg[DensityOffset] << ByteBits) | jpeg[DensityOffset + 1]).IsNotEqualTo(0);
        await Assert.That((jpeg[DensityOffset + ThumbnailSide] << ByteBits) | jpeg[DensityOffset + ThumbnailSide + 1]).IsNotEqualTo(0);
        await Assert.That(jpeg[ThumbnailOffset]).IsEqualTo((byte)0);
        await Assert.That(jpeg[ThumbnailOffset + 1]).IsEqualTo((byte)0);
        await Assert.That(JpegMarkers.TryReadInfo(jpeg, out var info)).IsTrue();
        await Assert.That(info.HasJfif).IsTrue();
        await Assert.That(info.Components).IsEqualTo(components);
        var ids = ComponentIds(jpeg, components);
        for (var i = 0; i < components; i++)
        {
            await Assert.That(ids[i]).IsEqualTo((byte)(i + 1));
        }
    }

    /// <summary>The JFIF segment is the 18 bytes the standard gives for a file with no thumbnail, and the next segment follows it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JfifSegmentHasTheStandardSize()
    {
        var jpeg = PdfJpegEncoder.Encode(JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height), PdfJpegPixelFormat.Rgb24, Width, Height, PdfJpegOptions.Default, CancellationToken.None);

        await Assert.That(jpeg[JfifSegmentBytes + ThumbnailSide]).IsEqualTo(JpegMarkers.MarkerPrefix);
        await Assert.That(jpeg[JfifSegmentBytes + ThumbnailSide + 1]).IsEqualTo(JpegMarkers.QuantizationTables);
    }

    /// <summary>A CMYK file has the Adobe marker and no JFIF segment, which is only for one or three components.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykFilesHaveNoJfifSegment()
    {
        var jpeg = PdfJpegEncoder.Encode(JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Cmyk32, Width, Height), PdfJpegPixelFormat.Cmyk32, Width, Height, PdfJpegOptions.Default, CancellationToken.None);

        await Assert.That(JpegMarkers.TryReadInfo(jpeg, out var info)).IsTrue();
        await Assert.That(info.HasJfif).IsFalse();
        await Assert.That(info.HasAdobe).IsTrue();
    }

    /// <summary>The encoder's RGB to YCbCr conversion follows the formulas of clause 7 to within rounding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncoderConversionFollowsTheStandard()
    {
        var worst = 0;
        for (var r = 0; r <= byte.MaxValue; r += GridStep)
        {
            for (var g = 0; g <= byte.MaxValue; g += GridStep)
            {
                for (var b = 0; b <= byte.MaxValue; b += GridStep)
                {
                    JpegPixelConverter.ToYcc(r, g, b, out var y, out var cb, out var cr);
                    worst = Math.Max(worst, Math.Abs(y - Clamp((LumaRed * r) + (LumaGreen * g) + (LumaBlue * b))));
                    worst = Math.Max(worst, Math.Abs(cb - Clamp((BlueFromRed * r) + (BlueFromGreen * g) + (Half * b) + ChromaCenter)));
                    worst = Math.Max(worst, Math.Abs(cr - Clamp((Half * r) + (RedFromGreen * g) + (RedFromBlue * b) + ChromaCenter)));
                }
            }
        }

        await Assert.That(worst).IsLessThanOrEqualTo(ConversionTolerance);
    }

    /// <summary>Neutral grays have both chroma samples at exactly 128.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeutralGraysHaveCentredChroma()
    {
        for (var level = 0; level <= byte.MaxValue; level++)
        {
            JpegPixelConverter.ToYcc(level, level, level, out var y, out var cb, out var cr);

            await Assert.That(y).IsEqualTo((byte)level);
            await Assert.That(cb).IsEqualTo((byte)ChromaCenter);
            await Assert.That(cr).IsEqualTo((byte)ChromaCenter);
        }
    }

    /// <summary>The decoder's YCbCr to RGB conversion follows the inverse formulas of clause 7 to within rounding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecoderConversionFollowsTheStandard()
    {
        var worst = 0;
        for (var y = 0; y <= byte.MaxValue; y += GridStep)
        {
            for (var cb = 0; cb <= byte.MaxValue; cb += GridStep)
            {
                for (var cr = 0; cr <= byte.MaxValue; cr += GridStep)
                {
                    var rgb = DecodeFlat(y, cb, cr);
                    worst = Math.Max(worst, Math.Abs(rgb[0] - Clamp(y + (RedFromCr * (cr - ChromaCenter)))));
                    worst = Math.Max(worst, Math.Abs(rgb[1] - Clamp(y + (GreenFromCb * (cb - ChromaCenter)) + (GreenFromCr * (cr - ChromaCenter)))));
                    worst = Math.Max(worst, Math.Abs(rgb[ThumbnailSide] - Clamp(y + (BlueFromCb * (cb - ChromaCenter)))));
                }
            }
        }

        await Assert.That(worst).IsLessThanOrEqualTo(ConversionTolerance);
    }

    /// <summary>JFIF thumbnails of every kind (clauses 10.1 and 10.3 to 10.5) are skipped; the main picture decodes as if they were absent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThumbnailsAreSkipped()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);
        var plain = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, PdfJpegOptions.Default, CancellationToken.None);
        var small = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, EmbeddedSide, EmbeddedSide);
        var embedded = PdfJpegEncoder.Encode(small, PdfJpegPixelFormat.Rgb24, EmbeddedSide, EmbeddedSide, PdfJpegOptions.Default, CancellationToken.None);

        var rich = WithThumbnails(plain, embedded);

        await Assert.That(JpegMarkers.TryReadInfo(rich, out var info)).IsTrue();
        await Assert.That(info.Width).IsEqualTo(Width);
        await Assert.That(info.Height).IsEqualTo(Height);
        await Assert.That(info.HasJfif).IsTrue();
        await Assert.That(Decode(rich)).IsEquivalentTo(Decode(plain));
    }

    /// <summary>A file with a JFIF segment is YCbCr whatever the component ids say; without one, R, G, B ids mean RGB.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JfifMakesThreeComponentsYcbcr()
    {
        var withJfif = new JpegInfo(1, 1, Colour, ByteBits, JpegProcess.Sequential, -1, true) { HasJfif = true };
        var without = new JpegInfo(1, 1, Colour, ByteBits, JpegProcess.Sequential, -1, true);

        await Assert.That(withJfif.UsesColorTransform(JpegInfo.NoColorTransform)).IsTrue();
        await Assert.That(without.UsesColorTransform(JpegInfo.NoColorTransform)).IsFalse();
    }

    /// <summary>With neither a JFIF nor an Adobe segment, three components are YCbCr and one is gray, as ISO 32000-2 Table 13 gives.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultWithoutJfifOrAdobeFollowsThePdfRule()
    {
        var three = new JpegInfo(1, 1, Colour, ByteBits, JpegProcess.Sequential, -1, false);
        var one = new JpegInfo(1, 1, 1, ByteBits, JpegProcess.Sequential, -1, false);

        await Assert.That(three.UsesColorTransform(JpegInfo.NoColorTransform)).IsTrue();
        await Assert.That(one.UsesColorTransform(JpegInfo.NoColorTransform)).IsFalse();
    }

    /// <summary>Rounds and clamps a value to a sample, as clause 7 defines.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The sample.</returns>
    private static int Clamp(double value) => (int)Math.Min(Math.Max(0, Math.Floor(value + Half)), byte.MaxValue);

    /// <summary>Decodes a one-block flat file whose three components have the given samples.</summary>
    /// <param name="y">The luma.</param>
    /// <param name="cb">The blue chroma.</param>
    /// <param name="cr">The red chroma.</param>
    /// <returns>The first pixel's red, green and blue.</returns>
    private static byte[] DecodeFlat(int y, int cb, int cr)
    {
        var jpeg = JpegTestEncoder.Encode(new(1, 1, Colour, JpegTestEncoder.NoAdobe, 0), [(byte)y, (byte)cb, (byte)cr]);
        var output = new byte[JpegTestEncoder.BlockSide * JpegTestEncoder.BlockSide * Colour];
        _ = JpegDecoder.TryDecode(jpeg, true, output);
        return output.AsSpan(0, Colour).ToArray();
    }

    /// <summary>Decodes a picture the size of the test pictures to RGB.</summary>
    /// <param name="jpeg">The file.</param>
    /// <returns>The samples.</returns>
    private static byte[] Decode(byte[] jpeg)
    {
        var output = new byte[Width * Height * Colour];
        _ = JpegDecoder.TryDecode(jpeg, true, output);
        return output;
    }

    /// <summary>Reads the component ids of the frame header.</summary>
    /// <param name="jpeg">The file.</param>
    /// <param name="count">The number of components.</param>
    /// <returns>The ids.</returns>
    private static byte[] ComponentIds(byte[] jpeg, int count)
    {
        var frame = 0;
        while (!(jpeg[frame] == JpegMarkers.MarkerPrefix && jpeg[frame + 1] is JpegMarkers.BaselineFrame or JpegMarkers.ProgressiveFrame))
        {
            frame++;
        }

        var ids = new byte[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = jpeg[frame + ComponentIdOffset + (i * ComponentEntryBytes)];
        }

        return ids;
    }

    /// <summary>Inserts a JFIF thumbnail and one of each extension segment after the JFIF segment of a file.</summary>
    /// <param name="plain">The file, which starts with SOI and an 18-byte JFIF segment.</param>
    /// <param name="embedded">A complete JPEG to use as the JPEG thumbnail.</param>
    /// <returns>The new file.</returns>
    private static byte[] WithThumbnails(byte[] plain, byte[] embedded)
    {
        List<byte> output = [];
        output.Add(plain[0]);
        output.Add(plain[1]);

        // A JFIF segment that carries a 2 x 2 RGB thumbnail of its own: the plain segment with the sizes and pixels added.
        var jfif = plain.AsSpan(ThumbnailSide, JfifSegmentBytes).ToArray();
        jfif[LengthLowOffset] = JfifLength + ThumbnailBytes;
        jfif[ThumbnailOffset - ThumbnailSide] = ThumbnailSide;
        jfif[ThumbnailOffset - 1] = ThumbnailSide;
        output.AddRange(jfif);
        output.AddRange(new byte[ThumbnailBytes]);
        AddExtension(output, JpegThumbnail, embedded);
        AddExtension(output, PaletteThumbnail, new byte[ThumbnailSide + ThumbnailSide + PaletteBytes + (ThumbnailSide * ThumbnailSide)]);
        AddExtension(output, RgbThumbnail, new byte[ThumbnailSide + ThumbnailSide + ThumbnailBytes]);
        output.AddRange(plain.AsSpan(JfifSegmentBytes + ThumbnailSide).ToArray());
        return output.ToArray();
    }

    /// <summary>Determines whether the bytes at a position spell a four-character identifier and a zero.</summary>
    /// <param name="jpeg">The file.</param>
    /// <param name="position">The offset.</param>
    /// <param name="a">The first character.</param>
    /// <param name="b">The second character.</param>
    /// <param name="c">The third character.</param>
    /// <param name="d">The fourth character.</param>
    /// <returns><see langword="true"/> when the identifier is there.</returns>
    private static bool IdentifierIs(byte[] jpeg, int position, char a, char b, char c, char d) =>
        jpeg[position] == a && jpeg[position + 1] == b && jpeg[position + ThumbnailSide] == c && jpeg[position + Colour] == d && jpeg[position + Colour + 1] == 0;

    /// <summary>Adds one JFIF extension segment.</summary>
    /// <param name="output">The file being built.</param>
    /// <param name="code">The extension code.</param>
    /// <param name="data">The extension data.</param>
    private static void AddExtension(List<byte> output, byte code, byte[] data)
    {
        var length = data.Length + ExtensionOverhead;
        output.AddRange([JpegMarkers.MarkerPrefix, App0, (byte)(length >> ByteBits), (byte)length]);
        output.AddRange("JFXX\0"u8.ToArray());
        output.Add(code);
        output.AddRange(data);
    }
}
