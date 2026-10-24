// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Mp3;

/// <summary>The four byte header at the start of every MPEG audio frame.</summary>
/// <param name="Version">The MPEG version.</param>
/// <param name="Layer">The layer, 1 to 3.</param>
/// <param name="HasCrc">Whether a 16 bit check follows the header.</param>
/// <param name="BitRate">The bits a second the frame is coded at.</param>
/// <param name="SampleRate">The samples a second for each channel.</param>
/// <param name="SampleRateIndex">The two bit sample rate code, which also picks the scale factor band tables.</param>
/// <param name="HasPadding">Whether the frame holds one extra byte (four for layer 1).</param>
/// <param name="ChannelMode">0 stereo, 1 joint stereo, 2 two separate channels, 3 one channel.</param>
/// <param name="ModeExtension">For joint stereo in layer 3, bit 0 is intensity stereo and bit 1 is mid and side stereo.</param>
/// <param name="FrameLength">The bytes in the frame, including the header.</param>
/// <param name="SamplesPerFrame">The samples for each channel in the frame.</param>
[DebuggerDisplay("Mp3FrameHeader: MPEG {Version} layer {Layer}, {SampleRate} Hz, {BitRate} bps, {FrameLength} bytes")]
internal readonly record struct Mp3FrameHeader(
    MpegVersion Version,
    int Layer,
    bool HasCrc,
    int BitRate,
    int SampleRate,
    int SampleRateIndex,
    bool HasPadding,
    int ChannelMode,
    int ModeExtension,
    int FrameLength,
    int SamplesPerFrame)
{
    /// <summary>The bytes of a header.</summary>
    internal const int Length = 4;

    /// <summary>The channel mode of a single channel.</summary>
    internal const int MonoMode = 3;

    /// <summary>The channel mode of joint stereo.</summary>
    internal const int JointStereoMode = 1;

    /// <summary>The bytes of the check that follows a header when the frame is protected.</summary>
    internal const int CrcLength = 2;

    /// <summary>The number of the layer this reader decodes.</summary>
    internal const int Layer3 = 3;

    /// <summary>The first sync byte.</summary>
    private const byte SyncFirst = 0xFF;

    /// <summary>The top three bits of the second byte, which finish the eleven bit sync.</summary>
    private const int SyncMask = 0xE0;

    /// <summary>The shift of the version code in the second byte.</summary>
    private const int VersionShift = 3;

    /// <summary>The shift of the layer code in the second byte.</summary>
    private const int LayerShift = 1;

    /// <summary>The shift of the bit rate code in the third byte.</summary>
    private const int BitRateShift = 4;

    /// <summary>The shift of the sample rate code in the third byte.</summary>
    private const int RateShift = 2;

    /// <summary>The shift of the padding flag in the third byte.</summary>
    private const int PaddingShift = 1;

    /// <summary>The shift of the channel mode in the fourth byte.</summary>
    private const int ChannelModeShift = 6;

    /// <summary>The shift of the mode extension in the fourth byte.</summary>
    private const int ModeExtensionShift = 4;

    /// <summary>The mask of a four bit field.</summary>
    private const int NibbleMask = 0x0F;

    /// <summary>The mask of a two bit field.</summary>
    private const int TwoBitMask = 3;

    /// <summary>The set of bit rates of layer 1 in MPEG 2 and 2.5.</summary>
    private const int Mpeg2Layer1Set = 3;

    /// <summary>The set of bit rates of layers 2 and 3 in MPEG 2 and 2.5.</summary>
    private const int Mpeg2Layer23Set = 4;

    /// <summary>The bit rates in each set of the bit rate table.</summary>
    private const int BitRatesPerSet = 15;

    /// <summary>The sample rates of each version.</summary>
    private const int RatesPerVersion = 3;

    /// <summary>The bits in a byte, which divides the samples in a frame into the slots a bit rate fills.</summary>
    private const int SlotDivisor = 8;

    /// <summary>The number of channels of a stereo frame.</summary>
    private const int StereoChannels = 2;

    /// <summary>The factor in the length of a layer 1 frame: 384 samples over 32 samples a slot.</summary>
    private const int Layer1Factor = 12;

    /// <summary>The bit rate code that means the rate is free, which this reader does not support.</summary>
    private const int FreeBitRate = 0;

    /// <summary>The bit rate code that is not allowed.</summary>
    private const int InvalidBitRate = 15;

    /// <summary>The sample rate code that is not allowed.</summary>
    private const int InvalidSampleRate = 3;

    /// <summary>The version code that is not allowed.</summary>
    private const int ReservedVersion = 1;

    /// <summary>The version code of MPEG 1.</summary>
    private const int Mpeg1Code = 3;

    /// <summary>The version code of MPEG 2.</summary>
    private const int Mpeg2Code = 2;

    /// <summary>The number the layer code is taken from to give the layer: layer 1 is code 3.</summary>
    private const int LayerBase = 4;

    /// <summary>The samples for each channel in a layer 1 frame.</summary>
    private const int Layer1Samples = 384;

    /// <summary>The samples for each channel in a layer 2 frame, or a layer 3 frame of MPEG 1.</summary>
    private const int LongFrameSamples = 1_152;

    /// <summary>The samples for each channel in a layer 3 frame of MPEG 2 or 2.5.</summary>
    private const int ShortFrameSamples = 576;

    /// <summary>The bytes of a layer 1 slot.</summary>
    private const int Layer1SlotBytes = 4;

    /// <summary>The bits a kilobit holds.</summary>
    private const int Kilo = 1_000;

    /// <summary>Gets the number of channels.</summary>
    internal int Channels => ChannelMode == MonoMode ? 1 : StereoChannels;

    /// <summary>Gets a value indicating whether the stream is MPEG 1, which codes two granules in each frame.</summary>
    internal bool IsMpeg1 => Version == MpegVersion.Mpeg1;

    /// <summary>Gets the bytes of the side information of a layer 3 frame.</summary>
    internal int SideInfoLength => SideInfoSizes[(IsMpeg1 ? 0 : StereoChannels) + Channels - 1];

    /// <summary>Gets the bit rates in kilobits, 15 for each of five sets: MPEG 1 layers 1, 2 and 3, then MPEG 2 and 2.5 layer 1, then layers 2 and 3.</summary>
    private static ReadOnlySpan<ushort> BitRates =>
    [
        0x0000, 0x0020, 0x0040, 0x0060, 0x0080, 0x00A0, 0x00C0, 0x00E0, 0x0100, 0x0120, 0x0140, 0x0160, 0x0180, 0x01A0, 0x01C0,
        0x0000, 0x0020, 0x0030, 0x0038, 0x0040, 0x0050, 0x0060, 0x0070, 0x0080, 0x00A0, 0x00C0, 0x00E0, 0x0100, 0x0140, 0x0180,
        0x0000, 0x0020, 0x0028, 0x0030, 0x0038, 0x0040, 0x0050, 0x0060, 0x0070, 0x0080, 0x00A0, 0x00C0, 0x00E0, 0x0100, 0x0140,
        0x0000, 0x0020, 0x0030, 0x0038, 0x0040, 0x0050, 0x0060, 0x0070, 0x0080, 0x0090, 0x00A0, 0x00B0, 0x00C0, 0x00E0, 0x0100,
        0x0000, 0x0008, 0x0010, 0x0018, 0x0020, 0x0028, 0x0030, 0x0038, 0x0040, 0x0050, 0x0060, 0x0070, 0x0080, 0x0090, 0x00A0,
    ];

    /// <summary>Gets the bytes of side information: MPEG 1 with one channel and with two, then MPEG 2 and 2.5 with one and with two.</summary>
    private static ReadOnlySpan<byte> SideInfoSizes => [0x11, 0x20, 0x09, 0x11];

    /// <summary>Gets the sample rates of MPEG 1, then MPEG 2, then MPEG 2.5, three for each.</summary>
    private static ReadOnlySpan<ushort> SampleRates =>
    [
        0xAC44, 0xBB80, 0x7D00,
        0x5622, 0x5DC0, 0x3E80,
        0x2B11, 0x2EE0, 0x1F40,
    ];

    /// <summary>Reads a header.</summary>
    /// <param name="data">The bytes from the start of the frame; at least <see cref="Length"/>.</param>
    /// <param name="header">Receives the header.</param>
    /// <returns><see langword="true"/> when the bytes are a header this reader supports.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> data, out Mp3FrameHeader header)
    {
        header = default;
        if (data.Length < Length || data[0] != SyncFirst || (data[1] & SyncMask) != SyncMask)
        {
            return false;
        }

        var versionCode = (data[1] >> VersionShift) & TwoBitMask;
        var layerCode = (data[1] >> LayerShift) & TwoBitMask;
        var bitRateCode = (data[2] >> BitRateShift) & NibbleMask;
        var rateCode = (data[2] >> RateShift) & TwoBitMask;
        if (versionCode == ReservedVersion || layerCode == 0 || bitRateCode is FreeBitRate or InvalidBitRate || rateCode == InvalidSampleRate)
        {
            return false;
        }

        var version = VersionOf(versionCode);
        var layer = LayerBase - layerCode;
        var bitRate = BitRates[(BitRateSet(version, layer) * BitRatesPerSet) + bitRateCode] * Kilo;
        var sampleRate = SampleRates[(((int)version - 1) * RatesPerVersion) + rateCode];
        var hasPadding = ((data[2] >> PaddingShift) & 1) != 0;
        header = new(
            version,
            layer,
            (data[1] & 1) == 0,
            bitRate,
            sampleRate,
            rateCode,
            hasPadding,
            data[3] >> ChannelModeShift,
            (data[3] >> ModeExtensionShift) & TwoBitMask,
            LengthOf(version, layer, bitRate, sampleRate, hasPadding),
            SamplesOf(version, layer));
        return true;
    }

    /// <summary>Tells whether another header belongs to the same stream: the same version, layer, sample rate and channel count.</summary>
    /// <param name="other">The other header.</param>
    /// <returns>Whether they match.</returns>
    internal bool IsSameStream(in Mp3FrameHeader other) =>
        Version == other.Version && Layer == other.Layer && SampleRate == other.SampleRate && Channels == other.Channels;

    /// <summary>Gets the version a code stands for.</summary>
    /// <param name="code">The two bit version code.</param>
    /// <returns>The version.</returns>
    private static MpegVersion VersionOf(int code) => code switch
    {
        Mpeg1Code => MpegVersion.Mpeg1,
        Mpeg2Code => MpegVersion.Mpeg2,
        _ => MpegVersion.Mpeg25,
    };

    /// <summary>Picks the set of bit rates.</summary>
    /// <param name="version">The version.</param>
    /// <param name="layer">The layer.</param>
    /// <returns>The set, 0 to 4.</returns>
    private static int BitRateSet(MpegVersion version, int layer)
    {
        if (version == MpegVersion.Mpeg1)
        {
            return layer - 1;
        }

        return layer == 1 ? Mpeg2Layer1Set : Mpeg2Layer23Set;
    }

    /// <summary>Gets the samples for each channel in a frame.</summary>
    /// <param name="version">The version.</param>
    /// <param name="layer">The layer.</param>
    /// <returns>The samples.</returns>
    private static int SamplesOf(MpegVersion version, int layer)
    {
        if (layer == 1)
        {
            return Layer1Samples;
        }

        return layer == Layer3 && version != MpegVersion.Mpeg1 ? ShortFrameSamples : LongFrameSamples;
    }

    /// <summary>Gets the length of a frame in bytes.</summary>
    /// <param name="version">The version.</param>
    /// <param name="layer">The layer.</param>
    /// <param name="bitRate">The bits a second.</param>
    /// <param name="sampleRate">The samples a second.</param>
    /// <param name="hasPadding">Whether the frame is padded.</param>
    /// <returns>The length in bytes.</returns>
    private static int LengthOf(MpegVersion version, int layer, int bitRate, int sampleRate, bool hasPadding)
    {
        var padding = hasPadding ? 1 : 0;
        if (layer == 1)
        {
            return ((Layer1Factor * bitRate / sampleRate) + padding) * Layer1SlotBytes;
        }

        var slots = SamplesOf(version, layer) / SlotDivisor;
        return (slots * bitRate / sampleRate) + padding;
    }
}
