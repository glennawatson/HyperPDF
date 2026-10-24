// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads picture parameter sets.</summary>
internal static class H264PpsReader
{
    /// <summary>The largest picture parameter set number.</summary>
    private const int MaxId = 255;

    /// <summary>The largest sequence parameter set number.</summary>
    private const int MaxSpsId = 31;

    /// <summary>The most reference pictures in a list.</summary>
    private const int MaxRefIdx = 32;

    /// <summary>The 8x8 lists that follow the six 4x4 lists in the syntax when the transform is on and the colour planes are not all full size.</summary>
    private const int Lists8x8Narrow = 2;

    /// <summary>The 8x8 lists for 4:4:4 video.</summary>
    private const int Lists8x8Wide = 6;

    /// <summary>The chroma format of 4:4:4 video.</summary>
    private const int ChromaFormat444 = 3;

    /// <summary>The offset of the quantiser the picture starts from.</summary>
    private const int QpBase = 26;

    /// <summary>The bits of the weighted bi-prediction mode.</summary>
    private const int WeightedBits = 2;

    /// <summary>Reads a picture parameter set.</summary>
    /// <param name="rbsp">The payload of the NAL unit, without its header and emulation prevention bytes.</param>
    /// <param name="sequence">The sequence parameter set the picture parameter set names.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    internal static H264Pps Read(ReadOnlySpan<byte> rbsp, H264Sps sequence)
    {
        var reader = new MsbBitReader(rbsp);
        var id = (int)ExpGolomb.ReadUe(ref reader);
        var spsId = (int)ExpGolomb.ReadUe(ref reader);
        var cabac = reader.ReadBit();
        var bottomPoc = reader.ReadBit();
        if (ExpGolomb.ReadUe(ref reader) != 0)
        {
            throw new NotSupportedException("H.264 slice groups are not supported.");
        }

        var l0 = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var l1 = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var weighted = reader.ReadBit();
        var bipred = (int)reader.Read(WeightedBits);
        var initQp = QpBase + ExpGolomb.ReadSe(ref reader);
        _ = ExpGolomb.ReadSe(ref reader);
        var chromaOffset = ExpGolomb.ReadSe(ref reader);
        var deblocking = reader.ReadBit();
        var constrained = reader.ReadBit();
        var redundant = reader.ReadBit();
        var extension = ExpGolomb.MoreData(reader.BitPosition, ExpGolomb.StopBit(rbsp)) ? ReadExtension(ref reader, sequence) : new H264PpsExtension(false, sequence.Scaling, chromaOffset);
        if (reader.IsOverrun || id > MaxId || spsId > MaxSpsId || l0 > MaxRefIdx || l1 > MaxRefIdx)
        {
            throw new InvalidDataException("An H.264 picture parameter set is damaged.");
        }

        return new(
            id,
            spsId,
            cabac,
            bottomPoc,
            l0,
            l1,
            weighted,
            bipred,
            initQp,
            chromaOffset,
            extension.SecondChromaOffset,
            deblocking,
            constrained,
            redundant,
            extension.Transform8x8,
            extension.Scaling);
    }

    /// <summary>Reads the number of the sequence parameter set that a picture parameter set names, so the right one can be found to read it with.</summary>
    /// <param name="rbsp">The payload of the NAL unit.</param>
    /// <returns>The sequence parameter set number.</returns>
    internal static int ReadSequenceId(ReadOnlySpan<byte> rbsp)
    {
        var reader = new MsbBitReader(rbsp);
        _ = ExpGolomb.ReadUe(ref reader);
        return (int)ExpGolomb.ReadUe(ref reader);
    }

    /// <summary>Reads the part that high profile streams add: the 8x8 transform, scaling lists and the second chroma offset.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sequence">The sequence parameter set.</param>
    /// <returns>What was read.</returns>
    private static H264PpsExtension ReadExtension(ref MsbBitReader reader, H264Sps sequence)
    {
        var transform8x8 = reader.ReadBit();
        var scaling = sequence.Scaling;
        if (reader.ReadBit())
        {
            var wide = sequence.ChromaFormat == ChromaFormat444;
            var lists8x8 = 0;
            if (transform8x8)
            {
                lists8x8 = wide ? Lists8x8Wide : Lists8x8Narrow;
            }

            scaling = H264ScalingLists.Read(ref reader, H264ScalingLists.Count4x4 + lists8x8, sequence.SequenceScaling);
        }

        return new(transform8x8, scaling, ExpGolomb.ReadSe(ref reader));
    }
}
