// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HyperPdf.Codecs.Audio;

/// <summary>Reads uncompressed sound: whole number samples of 8, 16, 24 or 32 bits, floating point samples of 32 or 64 bits, and a-law or mu-law bytes.</summary>
[DebuggerDisplay("PcmDecoder: {_codec} {_bytesPerSample} bytes a sample")]
internal sealed class PcmDecoder : AudioDecoder
{
    /// <summary>The scale from a 16 bit sample to a float between -1 and 1.</summary>
    private const float Scale16 = 1F / 32_768F;

    /// <summary>The scale from an 8 bit sample, once its offset is removed.</summary>
    private const float Scale8 = 1F / 128F;

    /// <summary>The scale from a 32 bit sample, which a 24 bit sample is moved up to.</summary>
    private const double Scale32 = 1D / 2_147_483_648D;

    /// <summary>The offset of an unsigned 8 bit sample.</summary>
    private const int Offset8 = 128;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The bytes in a 16 bit sample.</summary>
    private const int Bytes16 = 2;

    /// <summary>The bytes in a 24 bit sample.</summary>
    private const int Bytes24 = 3;

    /// <summary>The bytes in a 32 bit sample.</summary>
    private const int Bytes32 = 4;

    /// <summary>The bytes in a 64 bit sample.</summary>
    private const int Bytes64 = 8;

    /// <summary>The offset of the second byte of a 24 bit sample.</summary>
    private const int SecondByte = 1;

    /// <summary>The offset of the third byte of a 24 bit sample.</summary>
    private const int ThirdByte = 2;

    /// <summary>The shift that moves a byte to the second place of a 32 bit number.</summary>
    private const int ShiftSecond = 16;

    /// <summary>The shift that moves a byte to the top of a 32 bit number.</summary>
    private const int ShiftTop = 24;

    /// <summary>The number of values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>The mask of a byte.</summary>
    private const int ByteMask = 0xFF;

    /// <summary>The mask of the mantissa of a companded byte.</summary>
    private const int MantissaMask = 0x0F;

    /// <summary>The mask of the segment of a companded byte.</summary>
    private const int SegmentMask = 0x70;

    /// <summary>The mask of the sign of a companded byte.</summary>
    private const int SignMask = 0x80;

    /// <summary>The shift of the segment of a companded byte down to a number.</summary>
    private const int SegmentShift = 4;

    /// <summary>The shift of an a-law mantissa up to the size of a sample.</summary>
    private const int ALawMantissaShift = 4;

    /// <summary>The shift of a mu-law mantissa up to the size of a sample.</summary>
    private const int MuLawMantissaShift = 3;

    /// <summary>The value every a-law byte is XORed with before it is read.</summary>
    private const int ALawMask = 0x55;

    /// <summary>The value added to an a-law mantissa in the first segment.</summary>
    private const int ALawFirst = 8;

    /// <summary>The value added to an a-law mantissa in the other segments.</summary>
    private const int ALawBias = 0x108;

    /// <summary>The bias of mu-law sound.</summary>
    private const int MuLawBias = 0x84;

    /// <summary>The float values of the 256 a-law bytes.</summary>
    private static readonly float[] ALawTable = BuildALaw();

    /// <summary>The float values of the 256 mu-law bytes.</summary>
    private static readonly float[] MuLawTable = BuildMuLaw();

    /// <summary>How the sound is coded.</summary>
    private readonly MediaCodec _codec;

    /// <summary>The bytes in one sample of one channel.</summary>
    private readonly int _bytesPerSample;

    /// <summary>Initializes a new instance of the <see cref="PcmDecoder"/> class.</summary>
    /// <param name="track">The sound track.</param>
    /// <exception cref="NotSupportedException">The sample size is not one this decoder reads.</exception>
    internal PcmDecoder(MediaTrack track)
        : base(track.SampleRate, track.Channels)
    {
        _codec = track.Codec;
        _bytesPerSample = track.Codec is MediaCodec.ALaw or MediaCodec.MuLaw ? 1 : track.BitsPerSample / BitsPerByte;
        if (!IsSupported(track.Codec, _bytesPerSample))
        {
            throw new NotSupportedException($"{track.BitsPerSample} bit {track.Codec} sound is not supported.");
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        // Nothing is kept from one packet to the next.
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, PcmBuffer output)
    {
        var data = packet.Data;
        var samples = output.Append(data.Length / _bytesPerSample);
        switch (_codec)
        {
            case MediaCodec.ALaw:
            {
                Lookup(data, ALawTable, samples);
                break;
            }

            case MediaCodec.MuLaw:
            {
                Lookup(data, MuLawTable, samples);
                break;
            }

            case MediaCodec.PcmFloat:
            {
                DecodeFloat(data, samples);
                break;
            }

            default:
            {
                DecodeInteger(data, samples);
                break;
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Nothing is held.
    }

    /// <summary>Checks whether a coding and sample size are ones this decoder reads.</summary>
    /// <param name="codec">The codec.</param>
    /// <param name="bytes">The bytes in a sample.</param>
    /// <returns>Whether it reads them.</returns>
    private static bool IsSupported(MediaCodec codec, int bytes) =>
        codec switch
        {
            MediaCodec.Pcm => bytes is 1 or Bytes16 or Bytes24 or Bytes32,
            MediaCodec.PcmFloat => bytes is Bytes32 or Bytes64,
            MediaCodec.ALaw or MediaCodec.MuLaw => true,
            _ => false,
        };

    /// <summary>Makes the table of a-law values.</summary>
    /// <returns>The 256 values.</returns>
    private static float[] BuildALaw()
    {
        var table = new float[ByteValues];
        for (var code = 0; code < table.Length; code++)
        {
            var value = code ^ ALawMask;
            var magnitude = (value & MantissaMask) << ALawMantissaShift;
            var segment = (value & SegmentMask) >> SegmentShift;
            magnitude = segment == 0 ? magnitude + ALawFirst : (magnitude + ALawBias) << (segment - 1);
            table[code] = ((value & SignMask) != 0 ? magnitude : -magnitude) * Scale16;
        }

        return table;
    }

    /// <summary>Makes the table of mu-law values.</summary>
    /// <returns>The 256 values.</returns>
    private static float[] BuildMuLaw()
    {
        var table = new float[ByteValues];
        for (var code = 0; code < table.Length; code++)
        {
            var value = ~code & ByteMask;
            var magnitude = (((value & MantissaMask) << MuLawMantissaShift) + MuLawBias) << ((value & SegmentMask) >> SegmentShift);
            table[code] = ((value & SignMask) != 0 ? MuLawBias - magnitude : magnitude - MuLawBias) * Scale16;
        }

        return table;
    }

    /// <summary>Converts bytes through a table.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="table">The 256 values.</param>
    /// <param name="samples">Receives one sample for each byte.</param>
    private static void Lookup(ReadOnlySpan<byte> data, float[] table, Span<float> samples)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = table[data[index]];
        }
    }

    /// <summary>Converts 16 bit samples a vector at a time.</summary>
    /// <param name="data">The little endian samples.</param>
    /// <param name="samples">Receives the floats.</param>
    private static void Convert16(ReadOnlySpan<byte> data, Span<float> samples)
    {
        var index = 0;
        if (BitConverter.IsLittleEndian && Vector.IsHardwareAccelerated)
        {
            var shorts = MemoryMarshal.Cast<byte, short>(data);
            var scale = new Vector<float>(Scale16);
            var half = Vector<float>.Count;
            var step = Vector<short>.Count;
            for (; index <= samples.Length - step; index += step)
            {
                Vector.Widen(new Vector<short>(shorts.Slice(index, step)), out var low, out var high);
                (Vector.ConvertToSingle(low) * scale).CopyTo(samples.Slice(index, half));
                (Vector.ConvertToSingle(high) * scale).CopyTo(samples.Slice(index + half, half));
            }
        }

        for (; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(data[(index * Bytes16)..]) * Scale16;
        }
    }

    /// <summary>Converts whole number samples.</summary>
    /// <param name="data">The little endian samples.</param>
    /// <param name="samples">Receives the floats.</param>
    private void DecodeInteger(ReadOnlySpan<byte> data, Span<float> samples)
    {
        switch (_bytesPerSample)
        {
            case 1:
            {
                for (var index = 0; index < samples.Length; index++)
                {
                    samples[index] = (data[index] - Offset8) * Scale8;
                }

                break;
            }

            case Bytes16:
            {
                Convert16(data, samples);
                break;
            }

            case Bytes24:
            {
                for (var index = 0; index < samples.Length; index++)
                {
                    // The three bytes go to the top of a 32 bit number, which keeps the sign.
                    var at = index * Bytes24;
                    var top = (data[at] << BitsPerByte) | (data[at + SecondByte] << ShiftSecond) | (data[at + ThirdByte] << ShiftTop);
                    samples[index] = (float)(top * Scale32);
                }

                break;
            }

            default:
            {
                for (var index = 0; index < samples.Length; index++)
                {
                    samples[index] = (float)(BinaryPrimitives.ReadInt32LittleEndian(data[(index * Bytes32)..]) * Scale32);
                }

                break;
            }
        }
    }

    /// <summary>Converts floating point samples.</summary>
    /// <param name="data">The little endian samples.</param>
    /// <param name="samples">Receives the floats.</param>
    private void DecodeFloat(ReadOnlySpan<byte> data, Span<float> samples)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = _bytesPerSample == Bytes32
                ? BinaryPrimitives.ReadSingleLittleEndian(data[(index * Bytes32)..])
                : (float)BinaryPrimitives.ReadDoubleLittleEndian(data[(index * Bytes64)..]);
        }
    }
}
