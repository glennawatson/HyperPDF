// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Media;

namespace HyperPdf.Engine;

/// <summary>Turns the samples of a PDF sound object into a WAVE file the system player can open.</summary>
internal static class HyperPdfSoundWave
{
    /// <summary>The bytes before the samples of a WAVE file: the RIFF header and the format and data chunk headers.</summary>
    private const int HeaderLength = 44;

    /// <summary>The bytes of the format chunk.</summary>
    private const int FormatLength = 16;

    /// <summary>The bytes of the RIFF header that are not counted in its length: the tag and the length field.</summary>
    private const int RiffPrefix = 8;

    /// <summary>The WAVE format tag of whole number samples.</summary>
    private const short PcmTag = 1;

    /// <summary>The WAVE format tag of A-law samples.</summary>
    private const short ALawTag = 6;

    /// <summary>The WAVE format tag of mu-law samples.</summary>
    private const short MuLawTag = 7;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The bits of a 16 bit sample.</summary>
    private const int SixteenBits = 16;

    /// <summary>The value that turns a signed byte into an unsigned one.</summary>
    private const byte SignFlip8 = 0x80;

    /// <summary>The value that turns a signed 16 bit value into an unsigned one.</summary>
    private const ushort SignFlip16 = 0x8000;

    /// <summary>The offset of the RIFF length.</summary>
    private const int RiffLengthOffset = 4;

    /// <summary>The offset of the format chunk's length.</summary>
    private const int FormatLengthOffset = 16;

    /// <summary>The offset of the format tag.</summary>
    private const int TagOffset = 20;

    /// <summary>The offset of the channel count.</summary>
    private const int ChannelsOffset = 22;

    /// <summary>The offset of the sampling rate.</summary>
    private const int RateOffset = 24;

    /// <summary>The offset of the bytes per second.</summary>
    private const int ByteRateOffset = 28;

    /// <summary>The offset of the bytes per frame.</summary>
    private const int BlockAlignOffset = 32;

    /// <summary>The offset of the bits per sample.</summary>
    private const int BitsOffset = 34;

    /// <summary>The offset of the data chunk's tag.</summary>
    private const int DataTagOffset = 36;

    /// <summary>The offset of the data chunk's length.</summary>
    private const int DataLengthOffset = 40;

    /// <summary>The bytes of a 16 bit sample.</summary>
    private const int SampleBytes = 2;

    /// <summary>Determines whether a sound's samples can be written as a WAVE file.</summary>
    /// <param name="sound">The sound.</param>
    /// <returns><see langword="true"/> when the encoding, compression and sample size are ones this class can write.</returns>
    internal static bool CanWrite(PdfSound sound) =>
        string.IsNullOrEmpty(sound.Compression) && sound.Channels > 0 && TagOf(sound) != 0 && sound.Rate > 0;

    /// <summary>Writes a sound as a WAVE file: the header, then the samples in the byte order and sign WAVE expects.</summary>
    /// <param name="sound">The sound.</param>
    /// <param name="samples">The decoded samples, which are changed in place and belong to the caller.</param>
    /// <param name="destination">Receives the file.</param>
    internal static void Write(PdfSound sound, Span<byte> samples, Stream destination)
    {
        Span<byte> wave = stackalloc byte[HeaderLength];
        var tag = TagOf(sound);
        var bytesPerSample = Math.Max(sound.BitsPerSample / BitsPerByte, 1);
        var rate = (int)sound.Rate;
        var blockAlign = (short)(sound.Channels * bytesPerSample);
        "RIFF"u8.CopyTo(wave);
        BinaryPrimitives.WriteInt32LittleEndian(wave[RiffLengthOffset..], HeaderLength + samples.Length - RiffPrefix);
        "WAVEfmt "u8.CopyTo(wave[RiffPrefix..]);
        BinaryPrimitives.WriteInt32LittleEndian(wave[FormatLengthOffset..], FormatLength);
        BinaryPrimitives.WriteInt16LittleEndian(wave[TagOffset..], tag);
        BinaryPrimitives.WriteInt16LittleEndian(wave[ChannelsOffset..], (short)sound.Channels);
        BinaryPrimitives.WriteInt32LittleEndian(wave[RateOffset..], rate);
        BinaryPrimitives.WriteInt32LittleEndian(wave[ByteRateOffset..], rate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wave[BlockAlignOffset..], blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wave[BitsOffset..], (short)sound.BitsPerSample);
        "data"u8.CopyTo(wave[DataTagOffset..]);
        BinaryPrimitives.WriteInt32LittleEndian(wave[DataLengthOffset..], samples.Length);
        Convert(sound, samples);
        destination.Write(wave);
        destination.Write(samples);
    }

    /// <summary>Gets the WAVE format tag of a sound, or 0 when it cannot be written.</summary>
    /// <param name="sound">The sound.</param>
    /// <returns>The tag.</returns>
    private static short TagOf(PdfSound sound) => (sound.Encoding, sound.BitsPerSample) switch
    {
        ("Raw" or "Signed", BitsPerByte or SixteenBits) => PcmTag,
        ("muLaw", BitsPerByte) => MuLawTag,
        ("ALaw", BitsPerByte) => ALawTag,
        _ => 0,
    };

    /// <summary>Writes samples in the byte order and sign WAVE expects.</summary>
    /// <param name="sound">The sound.</param>
    /// <param name="samples">The samples as the PDF stores them: unsigned or signed, most significant byte first. They are changed in place.</param>
    private static void Convert(PdfSound sound, Span<byte> samples)
    {
        if (sound.BitsPerSample == BitsPerByte)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = sound.Encoding == "Signed" ? (byte)(samples[i] ^ SignFlip8) : samples[i];
            }

            return;
        }

        // 16 bit samples: WAVE holds them as signed values with the least significant byte first.
        for (var i = 0; i + 1 < samples.Length; i += SampleBytes)
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(samples[i..]);
            BinaryPrimitives.WriteUInt16LittleEndian(samples[i..], sound.Encoding == "Raw" ? (ushort)(value ^ SignFlip16) : value);
        }
    }
}
