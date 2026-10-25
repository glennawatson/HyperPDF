// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Builds RIFF/WAVE files in memory for the tests.</summary>
internal static class WaveFile
{
    /// <summary>The size of the format chunk of a plain file.</summary>
    private const int FormatSize = 16;

    /// <summary>The size of the header before the chunks: RIFF, a length and WAVE.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of a chunk header.</summary>
    private const int ChunkHeader = 8;

    /// <summary>Where the sound chunk starts.</summary>
    private const int DataAt = HeaderSize + ChunkHeader + FormatSize;

    /// <summary>The offset of a chunk's length.</summary>
    private const int LengthOffset = 4;

    /// <summary>The offset of the form name in the file header.</summary>
    private const int FormOffset = 8;

    /// <summary>The offset of the number of channels in the format chunk.</summary>
    private const int ChannelsOffset = 2;

    /// <summary>The offset of the sample rate in the format chunk.</summary>
    private const int RateOffset = 4;

    /// <summary>The offset of the bytes a second in the format chunk.</summary>
    private const int ByteRateOffset = 8;

    /// <summary>The offset of the bytes in a frame in the format chunk.</summary>
    private const int BlockAlignOffset = 12;

    /// <summary>The offset of the bits in a sample in the format chunk.</summary>
    private const int BitsOffset = 14;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Builds a file.</summary>
    /// <param name="format">The format code: 1 whole numbers, 3 floats, 6 a-law, 7 mu-law.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="rate">The samples each second for each channel.</param>
    /// <param name="bits">The bits in each sample.</param>
    /// <param name="data">The sound.</param>
    /// <returns>The file.</returns>
    internal static byte[] Build(int format, int channels, int rate, int bits, byte[] data) => Build(format, channels, rate, bits, data, data.Length);

    /// <summary>Builds a file whose sound chunk says a length that may not be the real one.</summary>
    /// <param name="format">The format code: 1 whole numbers, 3 floats, 6 a-law, 7 mu-law.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="rate">The samples each second for each channel.</param>
    /// <param name="bits">The bits in each sample.</param>
    /// <param name="data">The sound.</param>
    /// <param name="dataLength">The length written in the sound chunk.</param>
    /// <returns>The file.</returns>
    internal static byte[] Build(int format, int channels, int rate, int bits, byte[] data, long dataLength)
    {
        var file = new byte[HeaderSize + ChunkHeader + FormatSize + ChunkHeader + data.Length];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(LengthOffset), file.Length - ChunkHeader);
        "WAVE"u8.CopyTo(file.AsSpan(FormOffset));
        "fmt "u8.CopyTo(file.AsSpan(HeaderSize));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(HeaderSize + LengthOffset), FormatSize);
        var body = file.AsSpan(HeaderSize + ChunkHeader);
        BinaryPrimitives.WriteInt16LittleEndian(body, (short)format);
        BinaryPrimitives.WriteInt16LittleEndian(body[ChannelsOffset..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(body[RateOffset..], rate);
        BinaryPrimitives.WriteInt32LittleEndian(body[ByteRateOffset..], rate * channels * (bits / BitsPerByte));
        BinaryPrimitives.WriteInt16LittleEndian(body[BlockAlignOffset..], (short)(channels * (bits / BitsPerByte)));
        BinaryPrimitives.WriteInt16LittleEndian(body[BitsOffset..], (short)bits);
        "data"u8.CopyTo(file.AsSpan(DataAt));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(DataAt + LengthOffset), (uint)dataLength);
        data.CopyTo(file.AsSpan(DataAt + ChunkHeader));
        return file;
    }
}
