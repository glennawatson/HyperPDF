// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks reading WAVE files and decoding the uncompressed, a-law and mu-law sound in them.</summary>
public sealed class WaveContainerTests
{
    /// <summary>The samples each second of the test files.</summary>
    private const int Rate = 8_000;

    /// <summary>The frames in the long test file, which is more than two packets.</summary>
    private const int LongFrames = 10_000;

    /// <summary>The packets the long test file is read in.</summary>
    private const int LongPackets = 3;

    /// <summary>The format code of whole number sound.</summary>
    private const int Whole = 1;

    /// <summary>The format code of floating point sound.</summary>
    private const int Floating = 3;

    /// <summary>The format code of a-law sound.</summary>
    private const int ALaw = 6;

    /// <summary>The format code of mu-law sound.</summary>
    private const int MuLaw = 7;

    /// <summary>The number of channels of stereo sound.</summary>
    private const int Stereo = 2;

    /// <summary>The bits in a 16 bit sample.</summary>
    private const int Sixteen = 16;

    /// <summary>The bytes in a 16 bit sample.</summary>
    private const int TwoBytes = 2;

    /// <summary>The bits in an 8 bit sample.</summary>
    private const int Eight = 8;

    /// <summary>The bits in a 24 bit sample.</summary>
    private const int TwentyFour = 24;

    /// <summary>The bits in a 32 bit sample.</summary>
    private const int ThirtyTwo = 32;

    /// <summary>Half of the range of a 16 bit sample.</summary>
    private const short HalfRange16 = 16_384;

    /// <summary>The scale from a 16 bit sample to a float.</summary>
    private const float Scale16 = 32_768F;

    /// <summary>The scale from a 24 bit sample to a float.</summary>
    private const float Scale24 = 8_388_608F;

    /// <summary>The largest 24 bit sample.</summary>
    private const float Largest24 = 8_388_607F;

    /// <summary>The largest 8 bit sample over its scale.</summary>
    private const float Largest8 = 127F / 128F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>A quarter.</summary>
    private const float QuarterFloat = 0.25F;

    /// <summary>Three quarters.</summary>
    private const float ThreeQuarters = 0.75F;

    /// <summary>The position the seek test moves to, in seconds.</summary>
    private const double HalfSecond = 0.5;

    /// <summary>The a-law value of the quietest positive byte, in 16 bit units.</summary>
    private const float ALawQuiet = 8F;

    /// <summary>The a-law value of the next byte, in 16 bit units.</summary>
    private const float ALawNext = 24F;

    /// <summary>The mu-law value of the loudest negative byte, in 16 bit units.</summary>
    private const float MuLawLoudest = 32_124F;

    /// <summary>Verifies that stereo 16 bit sound reads back as the same numbers scaled to between -1 and 1.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Sixteen_BitStereo_DecodesToFloats()
    {
        var data = new byte[Stereo * TwoBytes * TwoBytes];
        BinaryPrimitives.WriteInt16LittleEndian(data, short.MinValue);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(TwoBytes), short.MaxValue);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(TwoBytes * Stereo), HalfRange16);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(data.Length - TwoBytes), 0);

        var samples = DecodeAll(WaveFile.Build(Whole, Stereo, Rate, Sixteen, data));

        await Assert.That(samples).IsEquivalentTo([-1F, short.MaxValue / Scale16, Half, 0F]);
    }

    /// <summary>Verifies that 8 bit sound, which is unsigned, is centred on zero.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Eight_Bit_IsCentredOnZero()
    {
        var samples = DecodeAll(WaveFile.Build(Whole, 1, Rate, Eight, [0x00, 0x80, 0xFF]));

        await Assert.That(samples).IsEquivalentTo([-1F, 0F, Largest8]);
    }

    /// <summary>Verifies that 24 bit sound keeps its sign.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TwentyFour_Bit_KeepsItsSign()
    {
        var samples = DecodeAll(WaveFile.Build(Whole, 1, Rate, TwentyFour, [0x00, 0x00, 0x80, 0x00, 0x00, 0x40, 0xFF, 0xFF, 0x7F]));

        await Assert.That(samples).IsEquivalentTo([-1F, Half, Largest24 / Scale24]);
    }

    /// <summary>Verifies that 32 bit floating point sound is passed through.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Float_IsPassedThrough()
    {
        var data = new byte[TwoBytes * TwoBytes * TwoBytes];
        BinaryPrimitives.WriteSingleLittleEndian(data, QuarterFloat);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(TwoBytes * TwoBytes), -ThreeQuarters);

        var samples = DecodeAll(WaveFile.Build(Floating, 1, Rate, ThirtyTwo, data));

        await Assert.That(samples).IsEquivalentTo([QuarterFloat, -ThreeQuarters]);
    }

    /// <summary>Verifies the a-law and mu-law values G.711 gives for silence and for the loudest bytes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Companded_Sound_FollowsG711()
    {
        var alawValues = DecodeAll(WaveFile.Build(ALaw, 1, Rate, Eight, [0xD5, 0xD4, 0x55]));
        var mulawValues = DecodeAll(WaveFile.Build(MuLaw, 1, Rate, Eight, [0xFF, 0x7F, 0x00]));

        await Assert.That(alawValues).IsEquivalentTo([ALawQuiet / Scale16, ALawNext / Scale16, -ALawQuiet / Scale16]);
        await Assert.That(mulawValues).IsEquivalentTo([0F, 0F, -MuLawLoudest / Scale16]);
    }

    /// <summary>Verifies that the container reports the track, the length, and packets that add up to the whole sound.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Container_ReportsTrackAndSplitsIntoPackets()
    {
        await using var stream = new MemoryStream(WaveFile.Build(Whole, 1, Rate, Sixteen, new byte[LongFrames * TwoBytes]));
        using var container = MediaContainers.Open(stream)!;
        var packet = new MediaPacket();
        var packets = 0;
        var frames = 0;
        var lastTime = -1D;
        while (container.ReadPacket(packet))
        {
            packets++;
            frames += packet.Length / TwoBytes;
            await Assert.That(packet.Time).IsGreaterThan(lastTime);
            lastTime = packet.Time;
        }

        await Assert.That(container.Tracks.Count).IsEqualTo(1);
        await Assert.That(container.Tracks[0].SampleRate).IsEqualTo(Rate);
        await Assert.That(container.Duration.TotalSeconds).IsEqualTo((double)LongFrames / Rate);
        await Assert.That(packets).IsEqualTo(LongPackets);
        await Assert.That(frames).IsEqualTo(LongFrames);
    }

    /// <summary>Verifies that seeking moves to the frame at the position.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Seek_MovesToThePosition()
    {
        await using var stream = new MemoryStream(WaveFile.Build(Whole, 1, Rate, Sixteen, new byte[LongFrames * TwoBytes]));
        using var container = MediaContainers.Open(stream)!;
        var packet = new MediaPacket();

        var moved = container.Seek(TimeSpan.FromSeconds(HalfSecond));
        var read = container.ReadPacket(packet);

        await Assert.That(moved).IsTrue();
        await Assert.That(read).IsTrue();
        await Assert.That(packet.Time).IsEqualTo(HalfSecond);
    }

    /// <summary>Verifies that a sound chunk with an unknown length, as a streaming writer leaves it, runs to the end of the file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnknownLength_RunsToTheEndOfTheFile()
    {
        var samples = DecodeAll(WaveFile.Build(Whole, 1, Rate, Sixteen, new byte[TwoBytes * TwoBytes * TwoBytes], uint.MaxValue));

        await Assert.That(samples.Length).IsEqualTo(TwoBytes * TwoBytes);
    }

    /// <summary>Verifies that something that is not a WAVE file is not opened and the stream is left where it was.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotAWaveFile_IsNotOpened()
    {
        await using var stream = new MemoryStream("This is plain text, not a sound file at all."u8.ToArray());

        var container = MediaContainers.Open(stream);

        await Assert.That(container).IsNull();
        await Assert.That(stream.Position).IsEqualTo(0);
    }

    /// <summary>Decodes a whole file.</summary>
    /// <param name="file">The WAVE file.</param>
    /// <returns>All the samples.</returns>
    private static float[] DecodeAll(byte[] file)
    {
        using var stream = new MemoryStream(file);
        using var container = MediaContainers.Open(stream)!;
        using var decoder = AudioDecoders.Create(container.Tracks[0])!;
        var packet = new MediaPacket();
        var output = new PcmBuffer();
        while (container.ReadPacket(packet))
        {
            decoder.Decode(packet, output);
        }

        return output.Samples.ToArray();
    }
}
