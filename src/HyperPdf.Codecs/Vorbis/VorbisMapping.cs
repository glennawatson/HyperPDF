// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>A mapping: which channel pairs are coupled, and which floor and residue each group of channels uses.</summary>
[DebuggerDisplay("VorbisMapping: {Submaps} submaps, {CouplingSteps} coupling steps")]
internal sealed class VorbisMapping
{
    /// <summary>The bits of the number of submaps, stored as that number less one.</summary>
    private const int SubmapBits = 4;

    /// <summary>The bits of the number of coupling steps, stored as that number less one.</summary>
    private const int StepBits = 8;

    /// <summary>The reserved bits after the coupling steps.</summary>
    private const int ReservedBits = 2;

    /// <summary>The bits of the submap of a channel.</summary>
    private const int ChannelSubmapBits = 4;

    /// <summary>The bits of the number of a floor or residue.</summary>
    private const int IndexBits = 8;

    /// <summary>Gets the number of submaps.</summary>
    internal required int Submaps { get; init; }

    /// <summary>Gets the number of coupling steps.</summary>
    internal int CouplingSteps => Magnitudes.Length;

    /// <summary>Gets the magnitude channel of each coupling step.</summary>
    internal required int[] Magnitudes { get; init; }

    /// <summary>Gets the angle channel of each coupling step.</summary>
    internal required int[] Angles { get; init; }

    /// <summary>Gets the submap of each channel.</summary>
    internal required int[] ChannelSubmaps { get; init; }

    /// <summary>Gets the floor of each submap.</summary>
    internal required int[] SubmapFloors { get; init; }

    /// <summary>Gets the residue of each submap.</summary>
    internal required int[] SubmapResidues { get; init; }

    /// <summary>Reads a mapping from the setup header.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="floors">The number of floors.</param>
    /// <param name="residues">The number of residues.</param>
    /// <returns>The mapping.</returns>
    /// <exception cref="InvalidDataException">The mapping is damaged.</exception>
    internal static VorbisMapping Read(ref LsbBitReader reader, int channels, int floors, int residues)
    {
        var submaps = reader.ReadBit() ? (int)reader.Read(SubmapBits) + 1 : 1;
        var steps = reader.ReadBit() ? (int)reader.Read(StepBits) + 1 : 0;
        var magnitudes = new int[steps];
        var angles = new int[steps];
        ReadCoupling(ref reader, channels, magnitudes, angles);
        if (reader.Read(ReservedBits) != 0)
        {
            throw new InvalidDataException("A Vorbis mapping has reserved bits set.");
        }

        var channelSubmaps = new int[channels];
        for (var channel = 0; channel < channels && submaps > 1; channel++)
        {
            channelSubmaps[channel] = (int)reader.Read(ChannelSubmapBits);
        }

        var submapFloors = new int[submaps];
        var submapResidues = new int[submaps];
        ReadSubmaps(ref reader, floors, residues, submapFloors, submapResidues);
        return new() { Submaps = submaps, Magnitudes = magnitudes, Angles = angles, ChannelSubmaps = channelSubmaps, SubmapFloors = submapFloors, SubmapResidues = submapResidues };
    }

    /// <summary>Reads the coupled channel pairs.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="magnitudes">Receives the magnitude channel of each step.</param>
    /// <param name="angles">Receives the angle channel of each step.</param>
    /// <exception cref="InvalidDataException">A pair cannot be coupled.</exception>
    private static void ReadCoupling(ref LsbBitReader reader, int channels, int[] magnitudes, int[] angles)
    {
        var bits = VorbisCodebook.Ilog(channels - 1);
        for (var step = 0; step < magnitudes.Length; step++)
        {
            magnitudes[step] = (int)reader.Read(bits);
            angles[step] = (int)reader.Read(bits);
            if (magnitudes[step] == angles[step] || magnitudes[step] >= channels || angles[step] >= channels)
            {
                throw new InvalidDataException("A Vorbis mapping couples channels that cannot be coupled.");
            }
        }
    }

    /// <summary>Reads the floor and residue of each submap.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="floors">The number of floors.</param>
    /// <param name="residues">The number of residues.</param>
    /// <param name="submapFloors">Receives the floor of each submap.</param>
    /// <param name="submapResidues">Receives the residue of each submap.</param>
    /// <exception cref="InvalidDataException">A submap names a floor or residue that does not exist.</exception>
    private static void ReadSubmaps(ref LsbBitReader reader, int floors, int residues, int[] submapFloors, int[] submapResidues)
    {
        for (var submap = 0; submap < submapFloors.Length; submap++)
        {
            _ = reader.Read(IndexBits);
            submapFloors[submap] = (int)reader.Read(IndexBits);
            submapResidues[submap] = (int)reader.Read(IndexBits);
            if (submapFloors[submap] >= floors || submapResidues[submap] >= residues)
            {
                throw new InvalidDataException("A Vorbis mapping names a floor or residue that does not exist.");
            }
        }
    }
}
