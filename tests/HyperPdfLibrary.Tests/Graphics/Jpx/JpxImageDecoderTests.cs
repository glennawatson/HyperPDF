// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Tests for the PDF rules around JPXDecode: PDFium parity on corpus images, the colour space decisions, palettes,
/// <c>/SMaskInData</c>, unsupported high-throughput blocks and damaged data.
/// </summary>
public sealed class JpxImageDecoderTests
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int Bgra = 4;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The components of an RGB image with alpha.</summary>
    private const int RgbAlpha = 4;

    /// <summary>The red byte of a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The alpha byte of a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The alpha component.</summary>
    private const int AlphaComponent = 3;

    /// <summary>The sample bits.</summary>
    private const int SampleBits = 8;

    /// <summary>The largest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The palette entries of the palette tests.</summary>
    private const int PaletteEntries = 16;

    /// <summary>The highest palette index of the PDF Indexed space.</summary>
    private const int HighestIndex = PaletteEntries - 1;

    /// <summary>The enumerated colour space of sRGB.</summary>
    private const int Srgb = 16;

    /// <summary>The enumerated colour space of sYCC.</summary>
    private const int Sycc = 18;

    /// <summary>The enumerated colour space of greyscale.</summary>
    private const int GrayEnumerated = 17;

    /// <summary>The bytes of a palette box header.</summary>
    private const int PaletteHeader = 3;

    /// <summary>The offset of the column count in a palette box.</summary>
    private const int PaletteColumnsOffset = 2;

    /// <summary>The palette size byte of an 8-bit unsigned column.</summary>
    private const byte EightBitColumn = 7;

    /// <summary>The bytes of a component mapping entry.</summary>
    private const int MappingEntry = 4;

    /// <summary>The offset of the palette column in a component mapping entry.</summary>
    private const int MappingColumn = 3;

    /// <summary>The offset of the mapping type in a component mapping entry.</summary>
    private const int MappingType = 2;

    /// <summary>The step between palette values.</summary>
    private const int PaletteStep = 16;

    /// <summary>The offset of the second palette column.</summary>
    private const int GreenOffset = 7;

    /// <summary>The offset of the third palette column.</summary>
    private const int BlueOffset = 3;

    /// <summary>The fraction of a codestream kept by the truncation test, in percent.</summary>
    private const int TruncatedPercent = 60;

    /// <summary>The percent divisor.</summary>
    private const int Percent = 100;

    /// <summary>The bytes of random garbage in the damage test.</summary>
    private const int GarbageLength = 300;

    /// <summary>The seed of the garbage.</summary>
    private const uint GarbageSeed = 77;

    /// <summary>The offset of the code-block style from the COD marker.</summary>
    private const int CodStyleOffset = 12;

    /// <summary>The high-throughput and MIXED code-block style bits.</summary>
    private const byte MixedHighThroughputBits = 0xC0;

    /// <summary>The COD marker.</summary>
    private const ushort CodMarker = 0xFF52;

    /// <summary>The chroma subsampling of the sYCC test.</summary>
    private const int Halved = 2;

    /// <summary>The chroma offset of 8-bit sYCC.</summary>
    private const int ChromaOffset = 128;

    /// <summary>The largest difference allowed between the fixed-point conversion and the double-precision one.</summary>
    private const int ConversionTolerance = 1;

    /// <summary>The sYCC red coefficient of Cr.</summary>
    private const double RedFromCr = 1.402;

    /// <summary>The sYCC green coefficient of Cb.</summary>
    private const double GreenFromCb = 0.344136;

    /// <summary>The sYCC green coefficient of Cr.</summary>
    private const double GreenFromCr = 0.714136;

    /// <summary>The sYCC blue coefficient of Cb.</summary>
    private const double BlueFromCb = 1.772;

    /// <summary>A small image width for the colour tests.</summary>
    private const int SmallWidth = 21;

    /// <summary>A small image height for the colour tests.</summary>
    private const int SmallHeight = 13;

    /// <summary>The corpus 5/3 image decodes to exactly the pixels PDFium renders.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusReversibleMatchesPdfium() =>
        await Assert.That(PixelHash(JpxFixtures.Reversible, JpxFixtures.ReversibleWidth, JpxFixtures.ReversibleHeight)).IsEqualTo(JpxFixtures.ReversiblePixels);

    /// <summary>The corpus 9/7 image decodes. Its pixels are compared with PDFium's in the engine tests, with a measured tolerance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusIrreversibleDecodes() =>
        await Assert.That(PixelHash(JpxFixtures.Irreversible, JpxFixtures.IrreversibleWidth, JpxFixtures.IrreversibleHeight)).IsNotEmpty();

    /// <summary>A one-component codestream with no /ColorSpace decodes as gray.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OneComponentWithoutSpaceIsGray()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight };
        var planes = JpxDecoderTests.Planes(options);
        var image = Decode(JpxTestEncoder.Encode(options, planes), null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, planes, [0, 0, 0])).IsTrue();
    }

    /// <summary>Three components with the reversible transform and no /ColorSpace decode as RGB.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreeComponentsWithoutSpaceAreRgb()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight, Components = Rgb, Transform = true };
        var planes = JpxDecoderTests.Planes(options);
        var image = Decode(JpxTestEncoder.Encode(options, planes), null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, planes, [0, 1, RedByte])).IsTrue();
    }

    /// <summary>Three components with 4:2:0 chroma are taken as YCbCr (our rule for files that do not say) and converted to RGB.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsampledChromaConvertsFromSycc()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight, Components = Rgb, ChromaSubsampling = Halved };
        var planes = JpxDecoderTests.Planes(options);
        var image = Decode(JpxTestEncoder.Encode(options, planes), null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, SyccToRgb(planes), [0, 1, RedByte], ConversionTolerance)).IsTrue();
    }

    /// <summary>A JP2 palette expands indices to RGB when the dictionary has no /ColorSpace.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PaletteExpandsWithoutSpace()
    {
        var indices = IndexPlanes();
        var image = Decode(PaletteFile(indices), null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, PaletteColours(indices), [0, 1, RedByte])).IsTrue();
    }

    /// <summary>An Indexed /ColorSpace reads the raw indices and ignores the JP2 palette, as PDFium does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IndexedSpaceReadsRawIndices()
    {
        var indices = IndexPlanes();
        var lookup = new byte[PaletteEntries * Rgb];
        for (var i = 0; i < PaletteEntries; i++)
        {
            lookup[i * Rgb] = (byte)(MaxSample - i);
        }

        var space = PdfColorSpace.Parse(
            PdfValue.FromArray(new(null, [PdfValue.FromName(KnownName.Indexed), PdfValue.FromName(KnownName.DeviceRGB), PdfValue.FromInteger(HighestIndex), PdfValue.FromString(lookup)])),
            null);
        var image = Decode(PaletteFile(indices), space, 0);
        int[][] expected = [[.. Map(indices[0], static index => MaxSample - index)]];

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, expected, [RedByte])).IsTrue();
    }

    /// <summary>With <c>/SMaskInData 1</c> the fourth channel of an sRGB image becomes its alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMaskInDataAppliesAlpha()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight, Components = RgbAlpha };
        var planes = JpxDecoderTests.Planes(options);
        var file = JpxTestFileWriter.Wrap(JpxTestEncoder.Encode(options, planes), RgbAlpha, SmallWidth, SmallHeight, Srgb, []);
        var masked = Decode(file, PdfColorSpace.DeviceRgb, 1);
        var plain = Decode(file, PdfColorSpace.DeviceRgb, 0);

        await Assert.That(masked).IsNotNull();
        await Assert.That(plain).IsNotNull();
        await Assert.That(Matches(masked!, [planes[AlphaComponent]], [AlphaByte])).IsTrue();
        await Assert.That(Matches(plain!, planes[..Rgb], [0, 1, RedByte])).IsTrue();
    }

    /// <summary>A fourth component the dictionary does not read is not decoded, and the transform still gets its three.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusedFourthComponentIsSkippedWithTheTransform()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight, Components = RgbAlpha, Transform = true };
        var planes = JpxDecoderTests.Planes(options);
        var file = JpxTestFileWriter.Wrap(JpxTestEncoder.Encode(options, planes), RgbAlpha, SmallWidth, SmallHeight, Srgb, []);
        var plain = Decode(file, PdfColorSpace.DeviceRgb, 0);
        var masked = Decode(file, PdfColorSpace.DeviceRgb, 1);

        await Assert.That(plain).IsNotNull();
        await Assert.That(masked).IsNotNull();
        await Assert.That(Matches(plain!, planes[..Rgb], [0, 1, RedByte])).IsTrue();
        await Assert.That(Matches(masked!, [planes[AlphaComponent]], [AlphaByte])).IsTrue();
    }

    /// <summary>The MIXED mode of HT and regular code-blocks is reported as unsupported, as PDFium refuses it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedHighThroughputIsUnsupported()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight };
        var codestream = JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options));
        var cod = FindMarker(codestream, CodMarker);
        codestream[cod + CodStyleOffset] |= MixedHighThroughputBits;
        var image = Decode(codestream, null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(image!.UnsupportedCodec).IsEqualTo(PdfImageCodec.Jpeg2000);
    }

    /// <summary>A truncated codestream keeps what decoded, and garbage is refused, without exceptions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDataDoesNotThrow()
    {
        var options = new JpxTestOptions { Components = Rgb, Transform = true, Layers = Rgb };
        var codestream = JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options));
        var truncated = codestream.AsSpan(0, codestream.Length * TruncatedPercent / Percent).ToArray();
        var garbage = new byte[GarbageLength];
        var random = new JpegTestRandom(GarbageSeed);
        for (var i = 0; i < garbage.Length; i++)
        {
            garbage[i] = (byte)random.Next(0, MaxSample + 1);
        }

        await Assert.That(Decode(truncated, null, 0)).IsNotNull();
        await Assert.That(Decode(garbage, null, 0)).IsNull();
    }

    /// <summary>
    /// An image smaller than its dictionary says still decodes, at the codestream's own size: the dictionary's size only
    /// fixes how the unit square is drawn, and refusing the image would lose it (PDFium refuses it; we do not).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageSmallerThanDictionaryDecodesAtItsOwnSize()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight };
        var codestream = JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options));
        var header = new ImageHeader(SmallWidth + 1, SmallHeight, SampleBits, false, false, null, null, null);
        var image = JpxImageDecoder.Decode(header, codestream, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(image!.Width).IsEqualTo(SmallWidth);
        await Assert.That(image.Height).IsEqualTo(SmallHeight);
    }

    /// <summary>An enumerated sYCC colour box converts subsampled chroma to RGB (T.800 table I.10, value 18).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EnumeratedSyccConvertsToRgb()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight, Components = Rgb, ChromaSubsampling = Halved };
        var planes = JpxDecoderTests.Planes(options);
        var file = JpxTestFileWriter.Wrap(JpxTestEncoder.Encode(options, planes), Rgb, SmallWidth, SmallHeight, Sycc, []);
        var image = Decode(file, null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, SyccToRgb(planes), [0, 1, RedByte], ConversionTolerance)).IsTrue();
    }

    /// <summary>
    /// A greyscale colour box names one colour channel, so on a three-channel file the first channel is the grey image
    /// and the others are not colour (our rule; the channel definition box would say what they are).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GreyBoxReadsOnlyTheFirstChannel()
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight, Components = Rgb, Transform = true };
        var planes = JpxDecoderTests.Planes(options);
        var file = JpxTestFileWriter.Wrap(JpxTestEncoder.Encode(options, planes), Rgb, SmallWidth, SmallHeight, GrayEnumerated, []);
        var image = Decode(file, null, 0);

        await Assert.That(image).IsNotNull();
        await Assert.That(Matches(image!, [planes[0]], [0, 1, RedByte])).IsTrue();
    }

    /// <summary>Values scale linearly onto 0 to 255 at every bit depth, with the extremes kept.</summary>
    /// <param name="value">The value.</param>
    /// <param name="precision">The bits of the value.</param>
    /// <param name="signed">Whether the value is signed.</param>
    /// <param name="expected">The expected 8-bit sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, 1, false, 0)]
    [Arguments(1, 1, false, 255)]
    [Arguments(5, 4, false, 85)]
    [Arguments(15, 4, false, 255)]
    [Arguments(200, 8, false, 200)]
    [Arguments(4095, 12, false, 255)]
    [Arguments(2048, 12, false, 128)]
    [Arguments(65_535, 16, false, 255)]
    [Arguments(-128, 8, true, 0)]
    [Arguments(127, 8, true, 255)]
    public async Task ValuesScaleLinearlyToEightBits(int value, int precision, bool signed, int expected) =>
        await Assert.That((int)JpxSampleWriter.ToByte(value, precision, signed)).IsEqualTo(expected);

    /// <summary>Decodes a fixture with a DeviceRGB header and hashes the pixels.</summary>
    /// <param name="base64">The fixture.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The SHA-256 of the BGRA pixels, or empty when the decode failed.</returns>
    private static string PixelHash(string base64, int width, int height)
    {
        var header = new ImageHeader(width, height, SampleBits, false, false, PdfColorSpace.DeviceRgb, null, null);
        var image = JpxImageDecoder.Decode(header, Convert.FromBase64String(base64), 0);
        return image is null ? string.Empty : Convert.ToHexString(SHA256.HashData(image.Pixels));
    }

    /// <summary>Decodes JPX data with a header matching its size.</summary>
    /// <param name="data">The data.</param>
    /// <param name="space">The dictionary's /ColorSpace, or <see langword="null"/>.</param>
    /// <param name="softMaskInData">The <c>/SMaskInData</c> value.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    private static PdfImageData? Decode(byte[] data, PdfColorSpace? space, int softMaskInData)
    {
        var header = new ImageHeader(1, 1, SampleBits, false, false, space, null, null);
        return JpxImageDecoder.Decode(header, data, softMaskInData);
    }

    /// <summary>Checks that BGRA bytes equal expected planes.</summary>
    /// <param name="image">The image.</param>
    /// <param name="planes">The expected value of each checked byte, per pixel.</param>
    /// <param name="bytes">The byte within each BGRA pixel each plane is compared with.</param>
    /// <returns><see langword="true"/> when all match.</returns>
    private static bool Matches(PdfImageData image, int[][] planes, int[] bytes) => Matches(image, planes, bytes, 0);

    /// <summary>Checks the decoded pixels against expected planes, allowing a difference per sample.</summary>
    /// <param name="image">The image.</param>
    /// <param name="planes">The expected value of each checked byte, per pixel.</param>
    /// <param name="bytes">The byte within each BGRA pixel each plane is compared with.</param>
    /// <param name="tolerance">The largest allowed difference.</param>
    /// <returns><see langword="true"/> when all match.</returns>
    private static bool Matches(PdfImageData image, int[][] planes, int[] bytes, int tolerance)
    {
        for (var p = 0; p < planes.Length; p++)
        {
            var plane = planes[planes.Length == 1 ? 0 : (planes.Length - 1 - p)];
            for (var i = 0; i < plane.Length; i++)
            {
                var actual = image.Pixels[(i * Bgra) + bytes[p]];
                if (Math.Abs(actual - plane[i]) > tolerance)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Converts 4:2:0 sYCC planes to RGB in double precision with the full-range ITU-R BT.601 matrix.</summary>
    /// <param name="planes">The Y, Cb and Cr planes.</param>
    /// <returns>The red, green and blue planes.</returns>
    private static int[][] SyccToRgb(int[][] planes)
    {
        const int chromaWidth = (SmallWidth + 1) / Halved;
        int[][] rgb = [new int[SmallWidth * SmallHeight], new int[SmallWidth * SmallHeight], new int[SmallWidth * SmallHeight]];
        for (var y = 0; y < SmallHeight; y++)
        {
            for (var x = 0; x < SmallWidth; x++)
            {
                var i = (y * SmallWidth) + x;
                var c = ((y / Halved) * chromaWidth) + (x / Halved);
                var luma = planes[0][i];
                var cb = planes[1][c] - ChromaOffset;
                var cr = planes[RedByte][c] - ChromaOffset;
                rgb[0][i] = Math.Clamp((int)Math.Round(luma + (RedFromCr * cr)), 0, MaxSample);
                rgb[1][i] = Math.Clamp((int)Math.Round(luma - ((GreenFromCb * cb) + (GreenFromCr * cr))), 0, MaxSample);
                rgb[RedByte][i] = Math.Clamp((int)Math.Round(luma + (BlueFromCb * cb)), 0, MaxSample);
            }
        }

        return rgb;
    }

    /// <summary>Makes a one-component image of palette indices.</summary>
    /// <returns>The plane.</returns>
    private static int[][] IndexPlanes()
    {
        var plane = new int[SmallWidth * SmallHeight];
        for (var i = 0; i < plane.Length; i++)
        {
            plane[i] = i % PaletteEntries;
        }

        return [plane];
    }

    /// <summary>Gets the palette's colour of each index, as red, green and blue planes.</summary>
    /// <param name="indices">The index plane.</param>
    /// <returns>The colour planes.</returns>
    private static int[][] PaletteColours(int[][] indices) =>
    [
        [.. Map(indices[0], static index => Column(index, 0))],
        [.. Map(indices[0], static index => Column(index, 1))],
        [.. Map(indices[0], static index => Column(index, RedByte))],
    ];

    /// <summary>Gets a palette value.</summary>
    /// <param name="index">The palette index.</param>
    /// <param name="column">The column.</param>
    /// <returns>The value.</returns>
    private static int Column(int index, int column) => column switch
    {
        0 => index * PaletteStep,
        1 => (index * GreenOffset) % (MaxSample + 1),
        _ => MaxSample - (index * BlueOffset),
    };

    /// <summary>Maps each value of a plane.</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="map">The mapping.</param>
    /// <returns>The mapped values.</returns>
    private static int[] Map(int[] plane, Func<int, int> map)
    {
        var result = new int[plane.Length];
        for (var i = 0; i < plane.Length; i++)
        {
            result[i] = map(plane[i]);
        }

        return result;
    }

    /// <summary>Makes a JP2 file with an index codestream, a three-column palette and a component mapping.</summary>
    /// <param name="indices">The index plane.</param>
    /// <returns>The file.</returns>
    private static byte[] PaletteFile(int[][] indices)
    {
        var options = new JpxTestOptions { Width = SmallWidth, Height = SmallHeight };
        var codestream = JpxTestEncoder.Encode(options, indices);
        var palette = new byte[PaletteHeader + Rgb + (PaletteEntries * Rgb)];
        BinaryPrimitives.WriteUInt16BigEndian(palette, PaletteEntries);
        palette[PaletteColumnsOffset] = Rgb;
        palette.AsSpan(PaletteHeader, Rgb).Fill(EightBitColumn);
        for (var i = 0; i < PaletteEntries; i++)
        {
            for (var c = 0; c < Rgb; c++)
            {
                palette[PaletteHeader + Rgb + (i * Rgb) + c] = (byte)Column(i, c);
            }
        }

        var mapping = new byte[Rgb * MappingEntry];
        for (var c = 0; c < Rgb; c++)
        {
            mapping[(c * MappingEntry) + MappingType] = 1;
            mapping[(c * MappingEntry) + MappingColumn] = (byte)c;
        }

        byte[] boxes = [.. JpxTestFileWriter.Box("pclr", palette), .. JpxTestFileWriter.Box("cmap", mapping)];
        return JpxTestFileWriter.Wrap(codestream, 1, SmallWidth, SmallHeight, Srgb, boxes);
    }

    /// <summary>Finds a marker in a codestream.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="marker">The marker code.</param>
    /// <returns>The marker's position.</returns>
    private static int FindMarker(byte[] data, ushort marker)
    {
        for (var i = 0; i + 1 < data.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i)) == marker)
            {
                return i;
            }
        }

        return -1;
    }
}
