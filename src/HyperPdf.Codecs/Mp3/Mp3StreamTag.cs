// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdf.Codecs.Mp3;

/// <summary>What the first frame of an MP3 file says about the file when an encoder put a Xing, Info or VBRI tag there instead of sound.</summary>
/// <param name="Frames">The sound frames in the file, or 0 when the tag does not say.</param>
/// <param name="EncoderDelay">The silent samples the encoder put in front of the sound, or 0 when not known.</param>
/// <param name="EncoderPadding">The silent samples the encoder put after the sound, or 0 when not known.</param>
[DebuggerDisplay("Mp3StreamTag: {Frames} frames, delay {EncoderDelay}, padding {EncoderPadding}")]
internal readonly record struct Mp3StreamTag(int Frames, int EncoderDelay, int EncoderPadding)
{
    /// <summary>The bytes of the configuration that carries the tag to the decoder: three little endian numbers.</summary>
    internal const int ConfigurationLength = 12;

    /// <summary>The samples of the frame the decoder skips after the start delay: the filterbank's own delay.</summary>
    internal const int DecoderDelay = 529;

    /// <summary>Where the frame count sits in the configuration.</summary>
    private const int FramesAt = 8;

    /// <summary>The index, within the three bytes of delay and padding, of the last byte.</summary>
    private const int PaddingByte = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in two bytes.</summary>
    private const int TwoByteBits = 16;

    /// <summary>The bytes of the tag's name.</summary>
    private const int NameLength = 4;

    /// <summary>The bytes of a number in the tag.</summary>
    private const int NumberLength = 4;

    /// <summary>The flag that says the frame count is present.</summary>
    private const int FramesFlag = 1;

    /// <summary>The flag that says the byte count is present.</summary>
    private const int BytesFlag = 2;

    /// <summary>The flag that says the 100 byte table of contents is present.</summary>
    private const int TableOfContentsFlag = 4;

    /// <summary>The flag that says the quality number is present.</summary>
    private const int QualityFlag = 8;

    /// <summary>The bytes of the table of contents.</summary>
    private const int TableOfContentsLength = 100;

    /// <summary>The bytes of the encoder's name at the start of the extra tag.</summary>
    private const int EncoderNameLength = 4;

    /// <summary>Where the delay and padding sit from the start of the extra tag.</summary>
    private const int DelayOffset = 21;

    /// <summary>The bytes the extra tag needs to hold the delay and padding.</summary>
    private const int DelayEnd = 24;

    /// <summary>The bits of the delay.</summary>
    private const int DelayBits = 12;

    /// <summary>The mask of the padding.</summary>
    private const int PaddingMask = 0xFFF;

    /// <summary>The bytes from the start of the VBRI tag to its frame count.</summary>
    private const int VbriFramesOffset = 14;

    /// <summary>The bytes of the side information that precede a Xing tag in an MPEG 1 stereo frame, as the tag's first possible place.</summary>
    private const int VbriOffset = 32;

    /// <summary>Gets a value indicating whether the tag holds a number to trim the start and end by.</summary>
    internal bool HasTrim => EncoderDelay > 0 || EncoderPadding > 0;

    /// <summary>Looks for a tag in a frame.</summary>
    /// <param name="frame">The whole first frame.</param>
    /// <param name="header">Its header.</param>
    /// <param name="tag">Receives the tag.</param>
    /// <returns><see langword="true"/> when the frame holds a tag, so it is not sound and is not decoded.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> frame, in Mp3FrameHeader header, out Mp3StreamTag tag)
    {
        tag = default;
        var xing = Mp3FrameHeader.Length + header.SideInfoLength;
        if (TryParseXing(frame, xing, out tag) || (header.HasCrc && TryParseXing(frame, xing + Mp3FrameHeader.CrcLength, out tag)))
        {
            return true;
        }

        var vbri = Mp3FrameHeader.Length + VbriOffset;
        if (frame.Length >= vbri + VbriFramesOffset + NumberLength && frame.Slice(vbri, NameLength).SequenceEqual("VBRI"u8))
        {
            tag = new(BinaryPrimitives.ReadInt32BigEndian(frame[(vbri + VbriFramesOffset)..]), 0, 0);
            return true;
        }

        return false;
    }

    /// <summary>Makes the tag for a container that says how many samples at the start are not sound, as an MP4 edit list does.</summary>
    /// <param name="startSkip">The samples at the start that are not sound, counting the decoder's own delay.</param>
    /// <returns>The tag, or one with no trim when there is nothing to skip.</returns>
    internal static Mp3StreamTag FromStartSkip(int startSkip) => startSkip <= 0 ? default : new(0, Math.Max(1, startSkip - DecoderDelay), 0);

    /// <summary>Reads the tag from the configuration.</summary>
    /// <param name="configuration">The configuration; shorter than <see cref="ConfigurationLength"/> means no tag.</param>
    /// <returns>The tag, with zeros when there is none.</returns>
    internal static Mp3StreamTag FromConfiguration(ReadOnlySpan<byte> configuration) =>
        configuration.Length < ConfigurationLength
            ? default
            : new(
                BinaryPrimitives.ReadInt32LittleEndian(configuration[FramesAt..]),
                BinaryPrimitives.ReadInt32LittleEndian(configuration),
                BinaryPrimitives.ReadInt32LittleEndian(configuration[NumberLength..]));

    /// <summary>Writes the tag as the decoder's configuration.</summary>
    /// <returns>The bytes: the delay, the padding and the frame count.</returns>
    internal byte[] ToConfiguration()
    {
        var bytes = new byte[ConfigurationLength];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, EncoderDelay);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(NumberLength), EncoderPadding);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(FramesAt), Frames);
        return bytes;
    }

    /// <summary>Reads a Xing or Info tag at an offset.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="offset">Where the tag's name would start.</param>
    /// <param name="tag">Receives the tag.</param>
    /// <returns>Whether there is a tag.</returns>
    private static bool TryParseXing(ReadOnlySpan<byte> frame, int offset, out Mp3StreamTag tag)
    {
        tag = default;
        if (frame.Length < offset + NameLength + NumberLength)
        {
            return false;
        }

        var name = frame.Slice(offset, NameLength);
        if (!name.SequenceEqual("Xing"u8) && !name.SequenceEqual("Info"u8))
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadInt32BigEndian(frame[(offset + NameLength)..]);
        var at = offset + NameLength + NumberLength;
        var frames = 0;
        if ((flags & FramesFlag) != 0 && frame.Length >= at + NumberLength)
        {
            frames = BinaryPrimitives.ReadInt32BigEndian(frame[at..]);
        }

        at += (flags & FramesFlag) != 0 ? NumberLength : 0;
        at += (flags & BytesFlag) != 0 ? NumberLength : 0;
        at += (flags & TableOfContentsFlag) != 0 ? TableOfContentsLength : 0;
        at += (flags & QualityFlag) != 0 ? NumberLength : 0;
        var (delay, padding) = ReadDelay(frame, at);
        tag = new(frames, delay, padding);
        return true;
    }

    /// <summary>Reads the encoder delay and padding from the extra tag that LAME and a few other encoders write after the Xing fields.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="at">Where the extra tag starts.</param>
    /// <returns>The delay and the padding, or zeros when the tag is not from an encoder known to write them.</returns>
    private static DelayAndPadding ReadDelay(ReadOnlySpan<byte> frame, int at)
    {
        if (frame.Length < at + DelayEnd)
        {
            return default;
        }

        var name = frame.Slice(at, EncoderNameLength);
        if (!name.SequenceEqual("LAME"u8) && !name.SequenceEqual("Lavf"u8) && !name.SequenceEqual("Lavc"u8))
        {
            return default;
        }

        var packed = (frame[at + DelayOffset] << TwoByteBits) | (frame[at + DelayOffset + 1] << ByteBits) | frame[at + DelayOffset + PaddingByte];
        return new(packed >> DelayBits, packed & PaddingMask);
    }

    /// <summary>The delay and padding of an encoder.</summary>
    /// <param name="Delay">The samples in front.</param>
    /// <param name="Padding">The samples at the end.</param>
    private readonly record struct DelayAndPadding(int Delay, int Padding);
}
