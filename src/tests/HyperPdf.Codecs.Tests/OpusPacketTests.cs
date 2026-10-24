// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Opus;

namespace HyperPdf.Codecs.Tests;

/// <summary>Tests that Opus packets are split into the frames RFC 6716 section 3 describes.</summary>
public sealed class OpusPacketTests
{
    /// <summary>The samples at 48 kHz in 20 ms.</summary>
    private const int TwentyMs = 0x3C0;

    /// <summary>The samples at 48 kHz in 60 ms.</summary>
    private const int SixtyMs = 0xB40;

    /// <summary>A CELT full band 20 ms mono table of contents (configuration 31, one frame).</summary>
    private const byte CeltSingle = 0xF8;

    /// <summary>A CELT full band 20 ms stereo table of contents with two equal frames.</summary>
    private const byte CeltTwoEqualStereo = 0xFD;

    /// <summary>A SILK wide band 60 ms mono table of contents with two frames of different sizes (configuration 11).</summary>
    private const byte SilkTwoUnequal = 0x5A;

    /// <summary>A hybrid super wide 10 ms mono table of contents with the arbitrary count header (configuration 12).</summary>
    private const byte HybridArbitrary = 0x63;

    /// <summary>The arbitrary count header of three frames with coded lengths and padding.</summary>
    private const byte VariableWithPadding = 0xC3;

    /// <summary>A single frame takes the rest of the packet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_SingleFrame_TakesTheRest()
    {
        byte[] packet = [CeltSingle, 0x01, 0x02, 0x03, 0x04];
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        var ok = OpusPacket.TryParse(packet, frames, out var info);

        await Assert.That(ok).IsTrue();
        await Assert.That(info).IsEqualTo(new(OpusMode.Celt, OpusBandwidth.Full, TwentyMs, false, 1));
        await Assert.That(frames[0]).IsEqualTo(new(1, 0x04));
    }

    /// <summary>Two equal frames split the body in half, and an odd body is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_TwoEqualFrames_SplitsInHalf()
    {
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        var even = OpusPacket.TryParse([CeltTwoEqualStereo, 0x01, 0x02, 0x03, 0x04], frames, out var info);
        var odd = OpusPacket.TryParse([CeltTwoEqualStereo, 0x01, 0x02, 0x03], frames, out _);

        await Assert.That(even).IsTrue();
        await Assert.That(info.Stereo).IsTrue();
        await Assert.That(info.FrameCount).IsEqualTo(0x02);
        await Assert.That(frames[0]).IsEqualTo(new(1, 0x02));
        await Assert.That(frames[1]).IsEqualTo(new(0x03, 0x02));
        await Assert.That(odd).IsFalse();
    }

    /// <summary>Two frames of different sizes read the first length from one byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_TwoUnequalFrames_ReadsTheFirstLength()
    {
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        var ok = OpusPacket.TryParse([SilkTwoUnequal, 0x02, 0x0A, 0x0B, 0x14, 0x15, 0x16], frames, out var info);

        await Assert.That(ok).IsTrue();
        await Assert.That(info.Mode).IsEqualTo(OpusMode.Silk);
        await Assert.That(info.Bandwidth).IsEqualTo(OpusBandwidth.Wide);
        await Assert.That(info.FrameSamples).IsEqualTo(SixtyMs);
        await Assert.That(frames[0]).IsEqualTo(new(0x02, 0x02));
        await Assert.That(frames[1]).IsEqualTo(new(0x04, 0x03));
    }

    /// <summary>A length of 252 or more takes a second byte that counts in fours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_LongFirstLength_UsesTwoBytes()
    {
        var packet = new byte[1 + 0x02 + 0x100 + 0x03];
        packet[0] = SilkTwoUnequal;
        packet[1] = 0xFC;
        packet[0x02] = 1;
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        var ok = OpusPacket.TryParse(packet, frames, out _);

        await Assert.That(ok).IsTrue();
        await Assert.That(frames[0]).IsEqualTo(new(0x03, 0x100));
        await Assert.That(frames[1]).IsEqualTo(new(0x103, 0x03));
    }

    /// <summary>The arbitrary count header gives equal frames when lengths are not coded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_ArbitraryEqualFrames_SplitsEvenly()
    {
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        var ok = OpusPacket.TryParse([HybridArbitrary, 0x03, 0x01, 0x01, 0x02, 0x02, 0x03, 0x03], frames, out var info);

        await Assert.That(ok).IsTrue();
        await Assert.That(info.Mode).IsEqualTo(OpusMode.Hybrid);
        await Assert.That(info.Bandwidth).IsEqualTo(OpusBandwidth.SuperWide);
        await Assert.That(info.FrameCount).IsEqualTo(0x03);
        await Assert.That(frames[0x02]).IsEqualTo(new(0x06, 0x02));
    }

    /// <summary>The arbitrary count header codes the lengths of all frames but the last, and padding is left off the end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_ArbitraryVariableFramesWithPadding_LeavesThePaddingOut()
    {
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        // Three frames, the first two lengths coded as 1 and 2, then a padding length of 2, then 1, 2 and 2 bytes of frames and 2 padding bytes.
        var ok = OpusPacket.TryParse([HybridArbitrary, VariableWithPadding, 0x02, 0x01, 0x02, 0x09, 0x09, 0x08, 0x08, 0x08, 0x07, 0x07], frames, out var info);

        await Assert.That(ok).IsTrue();
        await Assert.That(info.FrameCount).IsEqualTo(0x03);
        await Assert.That(frames[0]).IsEqualTo(new(0x05, 1));
        await Assert.That(frames[1]).IsEqualTo(new(0x06, 0x02));
        await Assert.That(frames[0x02]).IsEqualTo(new(0x08, 0x02));
    }

    /// <summary>An empty packet, a zero frame count and a frame past the end are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryParse_DamagedPackets_AreRefused()
    {
        var frames = new OpusFrame[OpusPacket.MaxFrames];

        await Assert.That(OpusPacket.TryParse([], frames, out _)).IsFalse();
        await Assert.That(OpusPacket.TryParse([HybridArbitrary, 0x00], frames, out _)).IsFalse();
        await Assert.That(OpusPacket.TryParse([SilkTwoUnequal, 0x09, 0x01], frames, out _)).IsFalse();
    }
}
