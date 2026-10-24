// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>
/// Everything the first and third header packets of a Vorbis stream say: the layout of the sound, and the codebooks, floors,
/// residues, mappings and modes the audio packets are coded with.
/// </summary>
[DebuggerDisplay("VorbisSetup: {Channels} channels, {SampleRate} Hz, blocks {ShortBlock} and {LongBlock}")]
internal sealed class VorbisSetup
{
    /// <summary>The bytes of the packet type and the word "vorbis" that start every header packet.</summary>
    private const int MarkerLength = 7;

    /// <summary>The packet type of the identification header.</summary>
    private const byte IdentificationType = 1;

    /// <summary>The packet type of the setup header.</summary>
    private const byte SetupType = 5;

    /// <summary>The bytes of the identification header.</summary>
    private const int IdentificationLength = 30;

    /// <summary>The index of the channel count in the identification header.</summary>
    private const int ChannelIndex = 11;

    /// <summary>The index of the sample rate in the identification header.</summary>
    private const int RateIndex = 12;

    /// <summary>The index of the block sizes in the identification header.</summary>
    private const int BlockSizeIndex = 28;

    /// <summary>The bits of the number of floors, residues, mappings and modes, which are stored as that number less one.</summary>
    private const int CountBits = 6;

    /// <summary>The bits of the number of codebooks, which are stored as that number less one.</summary>
    private const int BookCountBits = 8;

    /// <summary>The bits of a type number.</summary>
    private const int TypeBits = 16;

    /// <summary>The bits of the low nibble that holds the short block size.</summary>
    private const int NibbleBits = 4;

    /// <summary>The mask of a nibble.</summary>
    private const int NibbleMask = 0x0F;

    /// <summary>The smallest exponent of a block size the format allows.</summary>
    private const int SmallestExponent = 6;

    /// <summary>The largest exponent of a block size the format allows.</summary>
    private const int LargestExponent = 13;

    /// <summary>The largest number of channels this decoder handles.</summary>
    private const int MostChannels = 8;

    /// <summary>The residue type that is the largest the format defines.</summary>
    private const int LastResidueType = 2;

    /// <summary>The floor type this decoder reads.</summary>
    private const int ReadFloorType = 1;

    /// <summary>Gets the number of channels.</summary>
    internal int Channels { get; private set; }

    /// <summary>Gets the samples a second for each channel.</summary>
    internal int SampleRate { get; private set; }

    /// <summary>Gets the size of a short block.</summary>
    internal int ShortBlock { get; private set; }

    /// <summary>Gets the size of a long block.</summary>
    internal int LongBlock { get; private set; }

    /// <summary>Gets the codebooks.</summary>
    internal VorbisCodebook[] Codebooks { get; private set; } = [];

    /// <summary>Gets the floors.</summary>
    internal VorbisFloor[] Floors { get; private set; } = [];

    /// <summary>Gets the residues.</summary>
    internal VorbisResidue[] Residues { get; private set; } = [];

    /// <summary>Gets the mappings.</summary>
    internal VorbisMapping[] Mappings { get; private set; } = [];

    /// <summary>Gets the modes.</summary>
    internal VorbisMode[] Modes { get; private set; } = [];

    /// <summary>Reads the identification and setup headers.</summary>
    /// <param name="identification">The identification packet.</param>
    /// <param name="setup">The setup packet.</param>
    /// <returns>The setup.</returns>
    /// <exception cref="InvalidDataException">A header is damaged.</exception>
    /// <exception cref="NotSupportedException">The stream uses a floor type this decoder does not read.</exception>
    internal static VorbisSetup Parse(ReadOnlySpan<byte> identification, ReadOnlySpan<byte> setup)
    {
        var result = new VorbisSetup();
        result.ReadIdentification(identification);
        result.ReadSetup(setup);
        return result;
    }

    /// <summary>Tells whether a packet starts with a type byte and the word "vorbis".</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="type">The type byte the header must have.</param>
    /// <returns>Whether the marker is there.</returns>
    private static bool HasMarker(ReadOnlySpan<byte> packet, byte type) =>
        packet.Length >= MarkerLength && packet[0] == type && packet.Slice(1, MarkerLength - 1).SequenceEqual("vorbis"u8);

    /// <summary>Reads the identification header.</summary>
    /// <param name="packet">The packet.</param>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    private void ReadIdentification(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < IdentificationLength || !HasMarker(packet, IdentificationType))
        {
            throw new InvalidDataException("The Vorbis identification header is not valid.");
        }

        Channels = packet[ChannelIndex];
        SampleRate = BinaryPrimitives.ReadInt32LittleEndian(packet[RateIndex..]);
        var shortExponent = packet[BlockSizeIndex] & NibbleMask;
        var longExponent = packet[BlockSizeIndex] >> NibbleBits;
        ShortBlock = 1 << shortExponent;
        LongBlock = 1 << longExponent;
        if (!IsValidLayout(shortExponent, longExponent))
        {
            throw new InvalidDataException("The Vorbis identification header holds values that cannot be right.");
        }
    }

    /// <summary>Tells whether the channel count, rate and block sizes are ones the format allows.</summary>
    /// <param name="shortExponent">The exponent of the short block size.</param>
    /// <param name="longExponent">The exponent of the long block size.</param>
    /// <returns>Whether they are valid.</returns>
    private bool IsValidLayout(int shortExponent, int longExponent) =>
        Channels is >= 1 and <= MostChannels
        && SampleRate > 0
        && shortExponent is >= SmallestExponent and <= LargestExponent
        && longExponent is >= SmallestExponent and <= LargestExponent
        && shortExponent <= longExponent;

    /// <summary>Reads the setup header.</summary>
    /// <param name="packet">The packet.</param>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    private void ReadSetup(ReadOnlySpan<byte> packet)
    {
        if (packet.Length <= MarkerLength || !HasMarker(packet, SetupType))
        {
            throw new InvalidDataException("The Vorbis setup header is not valid.");
        }

        var reader = new LsbBitReader(packet[MarkerLength..]);
        Codebooks = new VorbisCodebook[(int)reader.Read(BookCountBits) + 1];
        for (var index = 0; index < Codebooks.Length; index++)
        {
            Codebooks[index] = VorbisCodebook.Read(ref reader);
        }

        var times = (int)reader.Read(CountBits) + 1;
        for (var index = 0; index < times; index++)
        {
            _ = reader.Read(TypeBits);
        }

        ReadFloors(ref reader);
        ReadResidues(ref reader);
        ReadMappings(ref reader);
        ReadModes(ref reader);
        if (reader.IsOverrun)
        {
            throw new InvalidDataException("The Vorbis setup header ends early.");
        }
    }

    /// <summary>Reads the floors.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="NotSupportedException">A floor is of type 0.</exception>
    private void ReadFloors(ref LsbBitReader reader)
    {
        Floors = new VorbisFloor[(int)reader.Read(CountBits) + 1];
        for (var index = 0; index < Floors.Length; index++)
        {
            if (reader.Read(TypeBits) != ReadFloorType)
            {
                throw new NotSupportedException("Only type 1 floors are read.");
            }

            Floors[index] = VorbisFloor.Read(ref reader, Codebooks.Length);
        }
    }

    /// <summary>Reads the residues.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="InvalidDataException">A residue has an unknown type.</exception>
    private void ReadResidues(ref LsbBitReader reader)
    {
        Residues = new VorbisResidue[(int)reader.Read(CountBits) + 1];
        for (var index = 0; index < Residues.Length; index++)
        {
            var type = (int)reader.Read(TypeBits);
            if (type > LastResidueType)
            {
                throw new InvalidDataException("A Vorbis residue has an unknown type.");
            }

            Residues[index] = VorbisResidue.Read(ref reader, type, Codebooks.Length);
        }
    }

    /// <summary>Reads the mappings.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="InvalidDataException">A mapping has an unknown type.</exception>
    private void ReadMappings(ref LsbBitReader reader)
    {
        Mappings = new VorbisMapping[(int)reader.Read(CountBits) + 1];
        for (var index = 0; index < Mappings.Length; index++)
        {
            if (reader.Read(TypeBits) != 0)
            {
                throw new InvalidDataException("A Vorbis mapping has an unknown type.");
            }

            Mappings[index] = VorbisMapping.Read(ref reader, Channels, Floors.Length, Residues.Length);
        }
    }

    /// <summary>Reads the modes.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="InvalidDataException">A mode is not valid.</exception>
    private void ReadModes(ref LsbBitReader reader)
    {
        Modes = new VorbisMode[(int)reader.Read(CountBits) + 1];
        for (var index = 0; index < Modes.Length; index++)
        {
            var longBlock = reader.ReadBit();
            var window = reader.Read(TypeBits);
            var transform = reader.Read(TypeBits);
            var mapping = (int)reader.Read(BookCountBits);
            if (window != 0 || transform != 0 || mapping >= Mappings.Length)
            {
                throw new InvalidDataException("A Vorbis mode is not valid.");
            }

            Modes[index] = new(longBlock, mapping);
        }
    }
}
