// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdf.Codecs.Bits;
using HyperPdf.Codecs.Dsp;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>
/// Decodes Vorbis audio packets. Each packet is one block; its sound overlaps the block before and after, so the decoder keeps the
/// second half of the last block and a packet's sound is what is left once the two halves are added. The first packet after a start
/// or a seek therefore gives no sound.
/// </summary>
[DebuggerDisplay("VorbisDecoder: {SampleRate} Hz, {Channels} channels")]
public sealed class VorbisDecoder : AudioDecoder
{
    /// <summary>The bytes of the length that starts each header in the track's configuration.</summary>
    private const int LengthBytes = 4;

    /// <summary>The shift that takes a quarter of a block size.</summary>
    private const int QuarterShift = 2;

    /// <summary>The setup of the stream.</summary>
    private readonly VorbisSetup _setup;

    /// <summary>The transform for short blocks.</summary>
    private readonly InverseMdct _shortTransform;

    /// <summary>The transform for long blocks.</summary>
    private readonly InverseMdct _longTransform;

    /// <summary>The rising slope of the window for a short block's half.</summary>
    private readonly float[] _shortSlope;

    /// <summary>The rising slope of the window for a long block's half.</summary>
    private readonly float[] _longSlope;

    /// <summary>The second half of the last block, for each channel.</summary>
    private readonly float[][] _tails;

    /// <summary>The coefficients of each channel: the residue, then the spectrum.</summary>
    private readonly float[][] _spectrum;

    /// <summary>The floor curve of each channel, as heights.</summary>
    private readonly int[][] _curves;

    /// <summary>Whether each channel has a floor in the packet.</summary>
    private readonly bool[] _nonzero;

    /// <summary>Whether each channel carries no residue in the packet.</summary>
    private readonly bool[] _skip;

    /// <summary>The samples of one transformed block.</summary>
    private readonly float[] _block;

    /// <summary>The vectors of the channels of one submap.</summary>
    private readonly float[][] _submapVectors;

    /// <summary>The one vector of a type 2 residue, which holds the channels interleaved.</summary>
    private readonly float[] _combined;

    /// <summary>The classes of the residue partitions.</summary>
    private readonly int[] _classes;

    /// <summary>Working room for the floors.</summary>
    private readonly VorbisFloorScratch _floorScratch = new();

    /// <summary>The sample at which the sound ends, which can be before the end of the last block.</summary>
    private readonly long _total;

    /// <summary>The size of the last block, or 0 before any.</summary>
    private int _previousBlock;

    /// <summary>The position of the next sample to be given, counted from the start of the stream.</summary>
    private long _position;

    /// <summary>Initializes a new instance of the <see cref="VorbisDecoder"/> class.</summary>
    /// <param name="setup">The setup.</param>
    /// <param name="total">The sample at which the sound ends.</param>
    private VorbisDecoder(VorbisSetup setup, long total)
        : base(setup.SampleRate, setup.Channels)
    {
        _setup = setup;
        _total = total;
        var longHalf = setup.LongBlock >> 1;
        _shortTransform = new(setup.ShortBlock >> 1);
        _longTransform = new(longHalf);
        _shortSlope = VorbisWindow.BuildSlope(setup.ShortBlock >> 1);
        _longSlope = VorbisWindow.BuildSlope(longHalf);
        _tails = NewPlanes(setup.Channels, longHalf);
        _spectrum = NewPlanes(setup.Channels, longHalf);
        _curves = new int[setup.Channels][];
        for (var channel = 0; channel < setup.Channels; channel++)
        {
            _curves[channel] = new int[longHalf];
        }

        _nonzero = new bool[setup.Channels];
        _skip = new bool[setup.Channels];
        _block = new float[setup.LongBlock];
        _submapVectors = new float[setup.Channels][];
        _combined = new float[setup.Channels * longHalf];
        _classes = new int[(setup.Channels + 1) * longHalf];
    }

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The Vorbis track, whose configuration holds the identification and setup headers, each after a little endian 32 bit length.</param>
    /// <returns>The decoder, or <see langword="null"/> when the headers are damaged or use something this decoder does not read.</returns>
    public static VorbisDecoder? Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        try
        {
            var configuration = track.Configuration.Span;
            var identificationLength = BinaryPrimitives.ReadInt32LittleEndian(configuration);
            var identification = configuration.Slice(LengthBytes, identificationLength);
            var rest = configuration[(LengthBytes + identificationLength)..];
            var setupLength = BinaryPrimitives.ReadInt32LittleEndian(rest);
            var afterSetup = rest[(LengthBytes + setupLength)..];
            var total = afterSetup.Length >= sizeof(long) ? BinaryPrimitives.ReadInt64LittleEndian(afterSetup) : -1;
            return new(VorbisSetup.Parse(identification, rest.Slice(LengthBytes, setupLength)), total >= 0 ? total : long.MaxValue);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _previousBlock = 0;
        foreach (var tail in _tails)
        {
            Array.Clear(tail);
        }
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, PcmBuffer output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        var reader = new LsbBitReader(packet.Data);
        if (reader.ReadBit())
        {
            return;
        }

        var mode = _setup.Modes[(int)reader.Read(VorbisCodebook.Ilog(_setup.Modes.Length - 1))];
        var block = ReadBlockShape(ref reader, mode);
        _position = _previousBlock == 0 ? (long)Math.Round(packet.Time * SampleRate) : _position;
        var half = block.Size >> 1;
        var mapping = _setup.Mappings[mode.Mapping];
        DecodeFloors(ref reader, mapping, half);
        DecodeResidues(ref reader, mapping, half);
        for (var step = mapping.CouplingSteps - 1; step >= 0; step--)
        {
            VorbisCoupling.Undo(_spectrum[mapping.Magnitudes[step]], _spectrum[mapping.Angles[step]], half);
        }

        MultiplyFloors(half);
        Transform(block, output);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Nothing is held that needs to be freed.
    }

    /// <summary>Makes a plane of samples for each channel.</summary>
    /// <param name="channels">The number of channels.</param>
    /// <param name="length">The length of each plane.</param>
    /// <returns>The planes.</returns>
    private static float[][] NewPlanes(int channels, int length)
    {
        var planes = new float[channels][];
        for (var channel = 0; channel < channels; channel++)
        {
            planes[channel] = new float[length];
        }

        return planes;
    }

    /// <summary>Reads how a long block's window meets its neighbours, which a packet of a long block says.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="mode">The mode of the packet.</param>
    /// <returns>The shape of the block.</returns>
    private BlockShape ReadBlockShape(ref LsbBitReader reader, VorbisMode mode)
    {
        var previousLong = true;
        var nextLong = true;
        if (mode.IsLongBlock)
        {
            previousLong = reader.ReadBit();
            nextLong = reader.ReadBit();
        }

        return new(mode.IsLongBlock, mode.IsLongBlock ? _setup.LongBlock : _setup.ShortBlock, previousLong, nextLong, _setup.ShortBlock);
    }

    /// <summary>Reads the floor of every channel.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="mapping">The mapping of the packet.</param>
    /// <param name="half">The number of coefficients in a channel.</param>
    private void DecodeFloors(ref LsbBitReader reader, VorbisMapping mapping, int half)
    {
        for (var channel = 0; channel < Channels; channel++)
        {
            var floor = _setup.Floors[mapping.SubmapFloors[mapping.ChannelSubmaps[channel]]];
            _nonzero[channel] = !reader.IsOverrun && floor.Decode(ref reader, _setup.Codebooks, half, _curves[channel], _floorScratch);
            Array.Clear(_spectrum[channel], 0, half);
        }

        for (var step = 0; step < mapping.CouplingSteps; step++)
        {
            var eitherUsed = _nonzero[mapping.Magnitudes[step]] || _nonzero[mapping.Angles[step]];
            _nonzero[mapping.Magnitudes[step]] = eitherUsed;
            _nonzero[mapping.Angles[step]] = eitherUsed;
        }
    }

    /// <summary>Reads the residue of every submap into the channels' vectors.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="mapping">The mapping of the packet.</param>
    /// <param name="half">The number of coefficients in a channel.</param>
    private void DecodeResidues(ref LsbBitReader reader, VorbisMapping mapping, int half)
    {
        for (var submap = 0; submap < mapping.Submaps && !reader.IsOverrun; submap++)
        {
            var residue = _setup.Residues[mapping.SubmapResidues[submap]];
            var count = GatherSubmap(mapping, submap);
            if (count == 0)
            {
                continue;
            }

            if (residue.Type == VorbisResidue.InterleavedAcrossChannels)
            {
                DecodeInterleaved(ref reader, residue, mapping, submap, count, half);
            }
            else
            {
                residue.Decode(ref reader, _setup.Codebooks, _submapVectors.AsSpan(0, count), _skip.AsSpan(0, count), half, _classes);
            }
        }
    }

    /// <summary>Collects the vectors and skip flags of the channels of a submap.</summary>
    /// <param name="mapping">The mapping of the packet.</param>
    /// <param name="submap">The submap.</param>
    /// <returns>The number of channels in the submap.</returns>
    private int GatherSubmap(VorbisMapping mapping, int submap)
    {
        var count = 0;
        for (var channel = 0; channel < Channels; channel++)
        {
            if (mapping.ChannelSubmaps[channel] != submap)
            {
                continue;
            }

            _submapVectors[count] = _spectrum[channel];
            _skip[count] = !_nonzero[channel];
            count++;
        }

        return count;
    }

    /// <summary>Reads a type 2 residue, whose channels are coded as one interleaved vector, and shares it out.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="residue">The residue.</param>
    /// <param name="mapping">The mapping of the packet.</param>
    /// <param name="submap">The submap.</param>
    /// <param name="count">The channels in the submap.</param>
    /// <param name="half">The number of coefficients in a channel.</param>
    private void DecodeInterleaved(ref LsbBitReader reader, VorbisResidue residue, VorbisMapping mapping, int submap, int count, int half)
    {
        if (_skip.AsSpan(0, count).IndexOf(false) < 0)
        {
            return;
        }

        var total = count * half;
        Array.Clear(_combined, 0, total);
        Span<float[]> vectors = [_combined];
        ReadOnlySpan<bool> noSkip = [false];
        residue.Decode(ref reader, _setup.Codebooks, vectors, noSkip, total, _classes);
        var member = 0;
        for (var channel = 0; channel < Channels; channel++)
        {
            if (mapping.ChannelSubmaps[channel] != submap)
            {
                continue;
            }

            for (var index = 0; index < half; index++)
            {
                _spectrum[channel][index] = _combined[(index * count) + member];
            }

            member++;
        }
    }

    /// <summary>Multiplies each channel's residue by its floor.</summary>
    /// <param name="half">The number of coefficients in a channel.</param>
    private void MultiplyFloors(int half)
    {
        var table = VorbisTableData.InverseDecibel;
        for (var channel = 0; channel < Channels; channel++)
        {
            var spectrum = _spectrum[channel];
            if (!_nonzero[channel])
            {
                Array.Clear(spectrum, 0, half);
                continue;
            }

            var curve = _curves[channel];
            for (var index = 0; index < half; index++)
            {
                spectrum[index] *= BitConverter.UInt32BitsToSingle(table[Math.Clamp(curve[index], 0, table.Length - 1)]);
            }
        }
    }

    /// <summary>Transforms the channels, windows them, adds the overlap and appends the finished sound.</summary>
    /// <param name="block">The shape of the block.</param>
    /// <param name="output">Receives the sound.</param>
    private void Transform(BlockShape block, PcmBuffer output)
    {
        var count = _previousBlock == 0 ? 0 : (_previousBlock >> QuarterShift) + (block.Size >> QuarterShift);
        count = (int)Math.Clamp(_total - _position, 0, count);
        _position += count;
        var samples = count == 0 ? default : output.Append(count * Channels);
        var transform = block.IsLong ? _longTransform : _shortTransform;
        var half = block.Size >> 1;
        for (var channel = 0; channel < Channels; channel++)
        {
            transform.Inverse(_spectrum[channel].AsSpan(0, half), _block);
            ApplyWindow(block);
            Overlap(channel, block, samples, count);
            _block.AsSpan(half, half).CopyTo(_tails[channel]);
        }

        _previousBlock = block.Size;
    }

    /// <summary>Multiplies the transformed block by its window.</summary>
    /// <param name="block">The shape of the block.</param>
    private void ApplyWindow(BlockShape block)
    {
        var half = block.Size >> 1;
        var left = block.IsLong && block.PreviousLong ? _longSlope : _shortSlope;
        var right = block.IsLong && block.NextLong ? _longSlope : _shortSlope;
        VorbisWindow.Apply(_block.AsSpan(0, half), left, block.LeftStart, false);
        VorbisWindow.Apply(_block.AsSpan(half, half), right, block.RightStart, true);
    }

    /// <summary>Adds the last block's tail to the start of this block and writes the finished sound for a channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="block">The shape of the block.</param>
    /// <param name="samples">Receives the interleaved samples.</param>
    /// <param name="count">The number of samples to write for the channel.</param>
    private void Overlap(int channel, BlockShape block, Span<float> samples, int count)
    {
        if (count == 0)
        {
            return;
        }

        var tail = _tails[channel];
        var shift = (_previousBlock >> QuarterShift) - (block.Size >> QuarterShift);
        var slot = VorbisChannelOrder.OutputSlot(Channels, channel);
        for (var index = 0; index < count; index++)
        {
            var value = index < _previousBlock >> 1 ? tail[index] : 0F;
            var inBlock = index - shift;
            if (inBlock >= 0 && inBlock < block.Size >> 1)
            {
                value += _block[inBlock];
            }

            samples[(index * Channels) + slot] = value;
        }
    }

    /// <summary>The shape of one block: its size and how its window meets the blocks either side.</summary>
    /// <param name="IsLong">Whether the block is a long one.</param>
    /// <param name="Size">The size of the block.</param>
    /// <param name="PreviousLong">Whether the block before is long, which a long block's window needs to know.</param>
    /// <param name="NextLong">Whether the block after is long.</param>
    /// <param name="ShortSize">The size of a short block.</param>
    private readonly record struct BlockShape(bool IsLong, int Size, bool PreviousLong, bool NextLong, int ShortSize)
    {
        /// <summary>Gets where the rising slope of the window starts in the left half.</summary>
        internal int LeftStart => IsLong && !PreviousLong ? (Size >> QuarterShift) - (ShortSize >> QuarterShift) : 0;

        /// <summary>Gets where the falling slope of the window starts, counted in from the outer edge of the right half.</summary>
        internal int RightStart => IsLong && !NextLong ? (Size >> QuarterShift) - (ShortSize >> QuarterShift) : 0;
    }
}
