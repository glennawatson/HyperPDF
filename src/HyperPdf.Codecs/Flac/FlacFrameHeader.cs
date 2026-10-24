// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdf.Codecs.Checksums;

namespace HyperPdf.Codecs.Flac;

/// <summary>The header at the start of every FLAC frame: the block size, rate, channel layout and sample size, and where the frame sits in the stream.</summary>
/// <param name="BlockSize">The samples for each channel in the frame.</param>
/// <param name="SampleRate">The samples each second for each channel.</param>
/// <param name="Assignment">How the channels are coded: 0 to 7 are that many plus one independent channels, 8 is left and side, 9 side and right, 10 mid and side.</param>
/// <param name="Channels">The number of channels.</param>
/// <param name="BitsPerSample">The bits in each sample.</param>
/// <param name="IsVariableBlock">Whether <paramref name="Number"/> counts samples rather than frames.</param>
/// <param name="Number">The number of the frame, or the number of its first sample when <paramref name="IsVariableBlock"/> is set.</param>
/// <param name="Length">The length of the header in bytes, including its check.</param>
[DebuggerDisplay("FlacFrameHeader: {BlockSize} samples at {Number}, {Channels} channels")]
internal readonly record struct FlacFrameHeader(
    int BlockSize,
    int SampleRate,
    int Assignment,
    int Channels,
    int BitsPerSample,
    bool IsVariableBlock,
    long Number,
    int Length)
{
    /// <summary>The fewest bytes a header takes.</summary>
    internal const int MinimumLength = 6;

    /// <summary>The most bytes a header takes.</summary>
    internal const int MaximumLength = 16;

    /// <summary>The first sync byte.</summary>
    private const byte SyncFirst = 0xFF;

    /// <summary>The mask of the second sync byte without the blocking strategy bit.</summary>
    private const int SyncMask = 0xFE;

    /// <summary>The second sync byte without the blocking strategy bit.</summary>
    private const int SyncSecond = 0xF8;

    /// <summary>The bytes before the frame or sample number: the sync, the codes and the channel and size codes.</summary>
    private const int FixedFieldsLength = 4;

    /// <summary>The block size code that means the size follows as 8 bits.</summary>
    private const int BlockSizeEightBits = 6;

    /// <summary>The block size code that means the size follows as 16 bits.</summary>
    private const int BlockSizeSixteenBits = 7;

    /// <summary>The sample rate code that means the rate follows in thousands of Hz.</summary>
    private const int RateKilohertz = 12;

    /// <summary>The sample rate code that means the rate follows in Hz.</summary>
    private const int RateHertz = 13;

    /// <summary>The sample rate code that means the rate follows in tens of Hz.</summary>
    private const int RateTensOfHertz = 14;

    /// <summary>The sample rate code that is not allowed.</summary>
    private const int RateInvalid = 15;

    /// <summary>The sample size code that is not allowed.</summary>
    private const int SampleSizeReserved = 3;

    /// <summary>The highest channel assignment the format defines.</summary>
    private const int LastAssignment = 10;

    /// <summary>The highest assignment that means independent channels.</summary>
    private const int LastIndependent = 7;

    /// <summary>The number of channels of the three stereo assignments, and the bytes of a 16 bit field.</summary>
    private const int Two = 2;

    /// <summary>The Hz in a code of kilohertz.</summary>
    private const int Thousand = 1_000;

    /// <summary>The Hz in a code of tens of Hz.</summary>
    private const int Ten = 10;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The mask of a nibble.</summary>
    private const int NibbleMask = 0x0F;

    /// <summary>The mask of the three bit sample size code.</summary>
    private const int SizeCodeMask = 0x07;

    /// <summary>The first byte values below this are a number on their own.</summary>
    private const int SingleByteLimit = 0x80;

    /// <summary>The bits in the word the first byte is moved into to count its leading ones.</summary>
    private const int WordBits = 32;

    /// <summary>The most extra bytes after the first byte of a number.</summary>
    private const int LongestExtra = 6;

    /// <summary>The mask of the number bits in the longest first byte, shifted down for shorter ones.</summary>
    private const int FirstByteMask = 0x7F;

    /// <summary>The mask of the marker bits of a continuation byte.</summary>
    private const int ContinuationMask = 0xC0;

    /// <summary>The marker bits of a continuation byte.</summary>
    private const int ContinuationMark = 0x80;

    /// <summary>The mask of the number bits of a continuation byte.</summary>
    private const int PayloadMask = 0x3F;

    /// <summary>The number bits of a continuation byte.</summary>
    private const int PayloadBits = 6;

    /// <summary>The block size of code 1.</summary>
    private const int FirstBlockSize = 192;

    /// <summary>The block size that codes 2 to 5 double from.</summary>
    private const int MediumBlockSize = 576;

    /// <summary>The block size that codes 8 to 15 double from.</summary>
    private const int LargeBlockSize = 256;

    /// <summary>The first block size code of the doubling run from 576.</summary>
    private const int MediumFirst = 2;

    /// <summary>The first block size code of the doubling run from 256.</summary>
    private const int LargeFirst = 8;

    /// <summary>The sample rates of codes 1 to 11.</summary>
    private static readonly int[] FixedRates = [0, 88_200, 176_400, 192_000, 8_000, 16_000, 22_050, 24_000, 32_000, 44_100, 48_000, 96_000];

    /// <summary>The sample sizes of codes 0 to 7; 0 means the stream information says, and 3 is reserved.</summary>
    private static readonly int[] SampleSizes = [0, 8, 12, 0, 16, 20, 24, 32];

    /// <summary>Reads a header.</summary>
    /// <param name="data">The bytes from the start of the frame; at least <see cref="MinimumLength"/>.</param>
    /// <param name="info">The stream information, for the fields a header leaves to it.</param>
    /// <param name="header">Receives the header.</param>
    /// <returns><see langword="true"/> when the bytes are a valid header whose check matches.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> data, FlacStreamInfo info, out FlacFrameHeader header)
    {
        header = default;
        if (!ReadCodes(data, out var codes))
        {
            return false;
        }

        var position = FixedFieldsLength;
        if (!ReadNumber(data, ref position, out var number)
            || !ReadBlockSize(data, codes.BlockCode, ref position, out var blockSize)
            || !ReadRate(data, codes.RateCode, info, ref position, out var rate))
        {
            return false;
        }

        if (position >= data.Length || Crc8.Compute(data[..(position + 1)]) != 0)
        {
            return false;
        }

        var bits = SampleSizes[codes.SizeCode] == 0 ? info.BitsPerSample : SampleSizes[codes.SizeCode];
        var channels = codes.Assignment <= LastIndependent ? codes.Assignment + 1 : Two;
        header = new(blockSize, rate, codes.Assignment, channels, bits, (data[1] & 1) != 0, number, position + 1);
        return true;
    }

    /// <summary>Gets the first sample of the frame, from its number and the block size of a fixed block stream.</summary>
    /// <param name="fixedBlockSize">The block size of every frame but the last when the stream has fixed blocks.</param>
    /// <returns>The sample number.</returns>
    internal long FirstSample(int fixedBlockSize) => IsVariableBlock ? Number : Number * fixedBlockSize;

    /// <summary>Checks the sync code and reads the four codes that follow it.</summary>
    /// <param name="data">The header bytes.</param>
    /// <param name="codes">Receives the codes.</param>
    /// <returns><see langword="false"/> when the sync is wrong or a code is one the format reserves.</returns>
    private static bool ReadCodes(ReadOnlySpan<byte> data, out HeaderCodes codes)
    {
        codes = default;
        if (data.Length < MinimumLength || data[0] != SyncFirst || (data[1] & SyncMask) != SyncSecond)
        {
            return false;
        }

        var found = new HeaderCodes(data[2] >> NibbleBits, data[2] & NibbleMask, data[3] >> NibbleBits, (data[3] >> 1) & SizeCodeMask);
        var reserved = found.BlockCode == 0 || found.RateCode == RateInvalid || found.Assignment > LastAssignment || found.SizeCode == SampleSizeReserved || (data[3] & 1) != 0;
        codes = found;
        return !reserved;
    }

    /// <summary>Reads the frame or sample number, which is written like a UTF-8 character but can be up to 36 bits.</summary>
    /// <param name="data">The header bytes.</param>
    /// <param name="position">Where the number starts; moved past it.</param>
    /// <param name="number">Receives the number.</param>
    /// <returns><see langword="false"/> when the number is cut off or badly coded.</returns>
    private static bool ReadNumber(ReadOnlySpan<byte> data, ref int position, out long number)
    {
        number = 0;
        int first = data[position];

        // The ones at the top of the first byte count the bytes, as in UTF-8; a byte of ones then a zero is the longest form.
        var ones = first < SingleByteLimit ? 0 : BitOperations.LeadingZeroCount((uint)(~first & byte.MaxValue) << (WordBits - ByteBits));
        var extra = ones == 0 ? 0 : ones - 1;
        if (ones == 1 || extra > LongestExtra || position + 1 + extra > data.Length)
        {
            return false;
        }

        long value = extra == 0 ? first : first & (FirstByteMask >> (extra + 1));
        for (var index = 1; index <= extra; index++)
        {
            var next = data[position + index];
            if ((next & ContinuationMask) != ContinuationMark)
            {
                return false;
            }

            value = (value << PayloadBits) | (long)(next & PayloadMask);
        }

        number = value;
        position += 1 + extra;
        return true;
    }

    /// <summary>Works out the block size from its code and the bytes after the number.</summary>
    /// <param name="data">The header bytes.</param>
    /// <param name="code">The block size code.</param>
    /// <param name="position">Where the extra bytes start; moved past them.</param>
    /// <param name="blockSize">Receives the block size.</param>
    /// <returns><see langword="false"/> when the header is cut off.</returns>
    private static bool ReadBlockSize(ReadOnlySpan<byte> data, int code, ref int position, out int blockSize)
    {
        blockSize = BlockSizeOf(code);
        if (code == BlockSizeEightBits && position < data.Length)
        {
            blockSize = data[position] + 1;
            position++;
        }
        else if (code == BlockSizeSixteenBits && position + 1 < data.Length)
        {
            blockSize = ((data[position] << ByteBits) | data[position + 1]) + 1;
            position += Two;
        }

        return blockSize > 0;
    }

    /// <summary>Gets the block size a code stands for when the size does not follow.</summary>
    /// <param name="code">The block size code.</param>
    /// <returns>The block size, or 0 when the code means the size follows.</returns>
    private static int BlockSizeOf(int code) =>
        code switch
        {
            1 => FirstBlockSize,
            >= MediumFirst and < BlockSizeEightBits => MediumBlockSize << (code - MediumFirst),
            >= LargeFirst => LargeBlockSize << (code - LargeFirst),
            _ => 0,
        };

    /// <summary>Works out the sample rate from its code and the bytes after the block size.</summary>
    /// <param name="data">The header bytes.</param>
    /// <param name="code">The sample rate code.</param>
    /// <param name="info">The stream information, which a code of 0 refers to.</param>
    /// <param name="position">Where the extra bytes start; moved past them.</param>
    /// <param name="rate">Receives the sample rate.</param>
    /// <returns><see langword="false"/> when the header is cut off.</returns>
    private static bool ReadRate(ReadOnlySpan<byte> data, int code, FlacStreamInfo info, ref int position, out int rate)
    {
        rate = RateOf(code, info);
        if (code == RateKilohertz && position < data.Length)
        {
            rate = data[position] * Thousand;
            position++;
        }
        else if (code is RateHertz or RateTensOfHertz && position + 1 < data.Length)
        {
            var value = (data[position] << ByteBits) | data[position + 1];
            rate = code == RateTensOfHertz ? value * Ten : value;
            position += Two;
        }

        return rate > 0;
    }

    /// <summary>Gets the sample rate a code stands for when the rate does not follow.</summary>
    /// <param name="code">The sample rate code.</param>
    /// <param name="info">The stream information, which a code of 0 refers to.</param>
    /// <returns>The rate, or 0 when the code means the rate follows.</returns>
    private static int RateOf(int code, FlacStreamInfo info)
    {
        if (code == 0)
        {
            return info.SampleRate;
        }

        return code < FixedRates.Length ? FixedRates[code] : 0;
    }

    /// <summary>The four codes at the start of a header.</summary>
    /// <param name="BlockCode">The block size code.</param>
    /// <param name="RateCode">The sample rate code.</param>
    /// <param name="Assignment">The channel assignment.</param>
    /// <param name="SizeCode">The sample size code.</param>
    private readonly record struct HeaderCodes(int BlockCode, int RateCode, int Assignment, int SizeCode);
}
