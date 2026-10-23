// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>
/// Tests for the decoder rules in ISO 32000-2 7.4.8 and ISO/IEC 10918-1: processes that must be refused cleanly, 12-bit
/// frames, a deferred line count, damaged and hostile files.
/// </summary>
public sealed class JpegStandardComplianceTests
{
    /// <summary>The blocks across the hand-built images.</summary>
    private const int BlocksAcross = 4;

    /// <summary>The blocks down the hand-built images.</summary>
    private const int BlocksDown = 2;

    /// <summary>The pixels in a block side.</summary>
    private const int Side = JpegTestEncoder.BlockSide;

    /// <summary>The restart interval of the resynchronisation test, in blocks.</summary>
    private const int RestartInterval = 3;

    /// <summary>The bytes cut from the first restart interval.</summary>
    private const int CutBytes = 4;

    /// <summary>The first block after the damaged interval.</summary>
    private const int FirstIntactBlock = 3;

    /// <summary>The bits per sample of the test headers.</summary>
    private const int SampleBits = 8;

    /// <summary>The mutated copies made of each file.</summary>
    private const int Mutations = 300;

    /// <summary>The seed of the damage generator.</summary>
    private const uint Seed = 0x1234_5678;

    /// <summary>The largest managed allocation a refused hostile file may cause.</summary>
    private const long MaxRefusalBytes = 256 * 1024;

    /// <summary>The side a hostile header claims.</summary>
    private const int HostileSide = 16_000;

    /// <summary>The length field of a hand-built frame segment with one component: the field, six fixed bytes and three for the component.</summary>
    private const int ProbeFrameLength = 11;

    /// <summary>The sample precision of an extended sequential frame.</summary>
    private const int ExtendedPrecision = 12;

    /// <summary>The components of a two-channel file.</summary>
    private const int TwoComponents = 2;

    /// <summary>The components of an RGB file.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The values a byte can hold.</summary>
    private const int ByteValues = 256;

    /// <summary>The longest run of bytes one mutation overwrites.</summary>
    private const int LongestFill = 16;

    /// <summary>The ways a file is damaged: cut short, one byte changed, a run overwritten.</summary>
    private const int DamageKinds = 3;

    /// <summary>The room for decoded samples in the damage test.</summary>
    private const int OutputBytes = 16_000;

    /// <summary>Gets the first restart marker.</summary>
    private static ReadOnlySpan<byte> FirstRestartMarker => [0xFF, 0xD0];

    /// <summary>Names the coding process of every frame marker the standard defines.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <param name="expected">The process.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((byte)0xC0, (int)JpegProcess.Sequential)]
    [Arguments((byte)0xC1, (int)JpegProcess.Sequential)]
    [Arguments((byte)0xC2, (int)JpegProcess.Progressive)]
    [Arguments((byte)0xC3, (int)JpegProcess.Lossless)]
    [Arguments((byte)0xC5, (int)JpegProcess.Hierarchical)]
    [Arguments((byte)0xC6, (int)JpegProcess.Hierarchical)]
    [Arguments((byte)0xC7, (int)JpegProcess.Hierarchical)]
    [Arguments((byte)0xC9, (int)JpegProcess.Arithmetic)]
    [Arguments((byte)0xCA, (int)JpegProcess.Arithmetic)]
    [Arguments((byte)0xCB, (int)JpegProcess.Lossless)]
    [Arguments((byte)0xCD, (int)JpegProcess.Arithmetic)]
    [Arguments((byte)0xCE, (int)JpegProcess.Arithmetic)]
    [Arguments((byte)0xCF, (int)JpegProcess.Arithmetic)]
    public async Task FrameMarkersNameTheirProcess(byte marker, int expected)
    {
        var found = JpegMarkers.TryReadInfo(FrameOnly(marker), out var info);

        await Assert.That(found).IsTrue();
        await Assert.That((int)info.Process).IsEqualTo(expected);
        await Assert.That(info.IsSupported).IsEqualTo(expected is (int)JpegProcess.Sequential or (int)JpegProcess.Progressive);
    }

    /// <summary>Arithmetic, lossless and hierarchical frames fail cleanly and the image layer reports the codec as unsupported.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((byte)0xC3)]
    [Arguments((byte)0xC5)]
    [Arguments((byte)0xC9)]
    [Arguments((byte)0xCA)]
    [Arguments((byte)0xCB)]
    [Arguments((byte)0xCF)]
    public async Task UnsupportedProcessesFailCleanly(byte marker)
    {
        var jpeg = FrameOnly(marker);
        var output = new byte[Side * Side];
        var header = new ImageHeader(Side, Side, SampleBits, false, false, PdfColorSpace.DeviceGray, null, null);

        var image = JpegImageDecoder.Decode(header, jpeg);

        await Assert.That(JpegDecoder.TryDecode(jpeg, false, output)).IsFalse();
        await Assert.That(image).IsNotNull();
        await Assert.That(image!.UnsupportedCodec).IsEqualTo(PdfImageCodec.Jpeg);
    }

    /// <summary>A 12-bit extended sequential frame decodes to 8-bit samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwelveBitFramesAreReducedToEightBits()
    {
        var values = JpegDecoderTests.BlockValues(1);
        var jpeg = JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, 1, JpegTestEncoder.NoAdobe, 0) { Precision = ExtendedPrecision }, values);
        var output = new byte[BlocksAcross * BlocksDown * Side * Side];

        var found = JpegMarkers.TryReadInfo(jpeg, out var info);

        await Assert.That(found).IsTrue();
        await Assert.That(info.Precision).IsEqualTo(ExtendedPrecision);
        await Assert.That(info.IsSupported).IsTrue();
        await Assert.That(info.IsPdfCarryable).IsFalse();
        await Assert.That(JpegDecoder.TryDecode(jpeg, false, output)).IsTrue();
        await Assert.That(BlocksMatch(output, values, 1, 0)).IsTrue();
    }

    /// <summary>A zero line count in the frame header is taken from the DNL marker after the first scan.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DnlMarkerSuppliesTheHeight()
    {
        var values = JpegDecoderTests.BlockValues(1);
        var jpeg = JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, 1, JpegTestEncoder.NoAdobe, 0) { UseDnl = true }, values);
        var output = new byte[BlocksAcross * BlocksDown * Side * Side];

        var found = JpegMarkers.TryReadInfo(jpeg, out var info);

        await Assert.That(found).IsTrue();
        await Assert.That(info.Height).IsEqualTo(BlocksDown * Side);
        await Assert.That(JpegDecoder.TryDecode(jpeg, false, output)).IsTrue();
        await Assert.That(BlocksMatch(output, values, 1, 0)).IsTrue();
    }

    /// <summary>A zero line count with no DNL marker is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZeroHeightWithoutDnlFails()
    {
        var jpeg = FrameOnly(JpegMarkers.BaselineFrame, 0);
        var output = new byte[Side * Side];

        await Assert.That(JpegDecoder.TryDecode(jpeg, false, output)).IsFalse();
    }

    /// <summary>Two-component files decode to interleaved raw samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoComponentFilesDecodeRaw()
    {
        var values = JpegDecoderTests.BlockValues(TwoComponents);
        var jpeg = JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, TwoComponents, JpegTestEncoder.NoAdobe, 0), values);
        var output = new byte[BlocksAcross * BlocksDown * Side * Side * TwoComponents];

        var found = JpegMarkers.TryReadInfo(jpeg, out var info);

        await Assert.That(found).IsTrue();
        await Assert.That(info.IsSupported).IsTrue();
        await Assert.That(info.UsesColorTransform(1)).IsFalse();
        await Assert.That(JpegDecoder.TryDecode(jpeg, true, output)).IsTrue();
        await Assert.That(BlocksMatch(output, values, TwoComponents, 0)).IsTrue();
    }

    /// <summary>Damage inside one restart interval costs only that interval.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestartMarkersResynchroniseAfterDamage()
    {
        var values = JpegDecoderTests.BlockValues(1);
        var jpeg = JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, 1, JpegTestEncoder.NoAdobe, RestartInterval), values);
        var restart = jpeg.AsSpan().IndexOf(FirstRestartMarker);
        var damaged = jpeg.AsSpan(0, restart - CutBytes).ToArray().Concat(jpeg.AsSpan(restart).ToArray()).ToArray();
        var output = new byte[BlocksAcross * BlocksDown * Side * Side];

        await Assert.That(JpegDecoder.TryDecode(damaged, false, output)).IsTrue();
        await Assert.That(BlocksMatch(output, values, 1, FirstIntactBlock)).IsTrue();
    }

    /// <summary>A header that claims a huge image over a few bytes of data is refused before any large buffer is rented.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HostileDimensionsAreRefusedWithoutAllocating()
    {
        var jpeg = FrameOnly(JpegMarkers.BaselineFrame, HostileSide, HostileSide);
        var header = new ImageHeader(Side, Side, SampleBits, false, false, PdfColorSpace.DeviceGray, null, null);
        var output = new byte[Side * Side];

        var before = GC.GetAllocatedBytesForCurrentThread();
        var decoded = JpegDecoder.TryDecode(jpeg, false, output);
        var image = JpegImageDecoder.Decode(header, jpeg);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(decoded).IsFalse();
        await Assert.That(image!.UnsupportedCodec).IsEqualTo(PdfImageCodec.Jpeg);
        await Assert.That(allocated).IsLessThan(MaxRefusalBytes);
    }

    /// <summary>An explicit /ColorTransform of 1 converts even when the components are labelled R, G and B.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplicitColorTransformBeatsRgbIds()
    {
        var info = new JpegInfo(1, 1, RgbComponents, SampleBits, JpegProcess.Sequential, -1, true);

        await Assert.That(info.UsesColorTransform(1)).IsTrue();
        await Assert.That(info.UsesColorTransform(0)).IsFalse();
        await Assert.That(info.UsesColorTransform(JpegInfo.NoColorTransform)).IsFalse();
    }

    /// <summary>Damaged copies of real files never throw; the decoder returns whatever it could read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedFilesNeverThrow()
    {
        var random = new JpegTestRandom(Seed);
        byte[][] files =
        [
            JpegFixtures.ProgressiveRgb,
            JpegFixtures.CmykCyan,
            JpegFixtures.CmykProgressive,
            JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, RgbComponents, JpegTestEncoder.NoAdobe, RestartInterval), JpegDecoderTests.BlockValues(RgbComponents)),
        ];
        var output = new byte[OutputBytes];
        foreach (var file in files)
        {
            for (var i = 0; i < Mutations; i++)
            {
                var copy = Damage(file, random);
                _ = JpegMarkers.TryReadInfo(copy, out _);
                _ = JpegDecoder.TryDecode(copy, (i & 1) == 0, output);
            }
        }

        await Assert.That(output.Length).IsGreaterThan(0);
    }

    /// <summary>Builds a file that holds only a frame header, with a square image.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <returns>The bytes.</returns>
    private static byte[] FrameOnly(byte marker) => FrameOnly(marker, Side, Side);

    /// <summary>Builds a file that holds only a frame header, with a given height and the default width.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bytes.</returns>
    private static byte[] FrameOnly(byte marker, int height) => FrameOnly(marker, Side, height);

    /// <summary>Builds a file that holds only a frame header.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bytes.</returns>
    private static byte[] FrameOnly(byte marker, int width, int height) =>
    [
        0xFF, 0xD8, 0xFF, marker, 0, ProbeFrameLength, SampleBits,
        (byte)(height >> ByteBits), (byte)height, (byte)(width >> ByteBits), (byte)width, 1, 1, 0x11, 0, 0xFF, 0xD9,
    ];

    /// <summary>Changes, cuts or fills part of a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>A damaged copy.</returns>
    private static byte[] Damage(byte[] file, JpegTestRandom random)
    {
        var copy = file.ToArray();
        switch (random.Next(0, DamageKinds))
        {
            case 0:
            {
                return copy.AsSpan(0, random.Next(1, copy.Length)).ToArray();
            }

            case 1:
            {
                copy[random.Next(0, copy.Length)] = (byte)random.Next(0, ByteValues);
                return copy;
            }

            default:
            {
                var start = random.Next(0, copy.Length);
                copy.AsSpan(start, Math.Min(random.Next(1, LongestFill), copy.Length - start)).Fill((byte)random.Next(0, ByteValues));
                return copy;
            }
        }
    }

    /// <summary>Checks the decoded flat blocks against the values that were encoded.</summary>
    /// <param name="samples">The decoded interleaved samples.</param>
    /// <param name="values">One value per block and component.</param>
    /// <param name="components">The components per pixel.</param>
    /// <param name="firstBlock">The first block to check.</param>
    /// <returns><see langword="true"/> when every checked sample matches.</returns>
    private static bool BlocksMatch(byte[] samples, byte[] values, int components, int firstBlock)
    {
        const int width = BlocksAcross * Side;
        for (var i = 0; i < samples.Length; i++)
        {
            var pixel = i / components;
            var block = (((pixel / width) / Side) * BlocksAcross) + ((pixel % width) / Side);
            if (block >= firstBlock && samples[i] != values[(block * components) + (i % components)])
            {
                return false;
            }
        }

        return true;
    }
}
