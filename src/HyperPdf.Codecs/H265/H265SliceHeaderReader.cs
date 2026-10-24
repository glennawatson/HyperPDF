// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads slice segment headers (7.3.6.1).</summary>
internal static class H265SliceHeaderReader
{
    /// <summary>The most reference pictures in a list.</summary>
    private const int MaxRefIdx = 15;

    /// <summary>The number of merge candidates when none are removed.</summary>
    private const int MaxMergeCandidates = 5;

    /// <summary>The most long-term pictures a slice header may name.</summary>
    private const int MaxLongTerm = 32;

    /// <summary>The most entry points a slice header may list.</summary>
    private const int MaxEntryPoints = 4_096;

    /// <summary>Reads a slice segment header.</summary>
    /// <param name="rbsp">The payload of the NAL unit, without its header and emulation prevention bytes.</param>
    /// <param name="nalType">The NAL unit type.</param>
    /// <param name="sets">The parameter sets received so far.</param>
    /// <param name="previous">The header of the slice before this segment, which a dependent segment continues.</param>
    /// <returns>The header.</returns>
    /// <exception cref="InvalidDataException">The header is damaged or names a parameter set that was not sent.</exception>
    internal static H265SliceHeader Read(ReadOnlySpan<byte> rbsp, int nalType, H265ParameterSets sets, H265SliceHeader? previous)
    {
        var reader = new MsbBitReader(rbsp);
        var first = reader.ReadBit();
        var noOutput = H265Nal.IsIrap(nalType) && reader.ReadBit();
        var ppsId = (int)ExpGolomb.ReadUe(ref reader);
        if (!sets.TryFind(ppsId, out var pps, out var sps))
        {
            throw new InvalidDataException("A slice names a parameter set that was not sent.");
        }

        var dependent = false;
        var address = 0;
        if (!first)
        {
            dependent = pps.Flags.DependentSlices && reader.ReadBit();
            address = (int)reader.Read(BitHelpers.CeilLog2(sps.WidthInCtbs * sps.HeightInCtbs));
        }

        var header = dependent ? Continue(previous) : ReadIndependent(ref reader, nalType, pps, sps);
        var entries = ReadEntryPoints(ref reader, pps);
        SkipExtension(ref reader, pps);
        reader.Skip(1);
        reader.Align();
        if (reader.IsOverrun)
        {
            throw new InvalidDataException("The H.265 slice header is damaged.");
        }

        var segment = new H265SliceSegment(first, noOutput, ppsId, dependent, address, (int)(reader.BitPosition >> 3), entries);
        return header with { Segment = segment };
    }

    /// <summary>Takes the settings a dependent slice segment shares with the slice before it.</summary>
    /// <param name="previous">The header of the slice before the segment.</param>
    /// <returns>That header.</returns>
    /// <exception cref="InvalidDataException">There is no slice before the segment.</exception>
    private static H265SliceHeader Continue(H265SliceHeader? previous) =>
        previous ?? throw new InvalidDataException("A dependent slice segment has no slice before it.");

    /// <summary>Reads the part of the header that a dependent segment does not repeat.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="nalType">The NAL unit type.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The header, with an empty segment that the caller fills in.</returns>
    private static H265SliceHeader ReadIndependent(ref MsbBitReader reader, int nalType, H265Pps pps, H265Sps sps)
    {
        BitHelpers.Skip(ref reader, pps.ExtraSliceHeaderBits);
        var sliceType = (int)ExpGolomb.ReadUe(ref reader);
        var picOutput = !pps.Flags.OutputFlagPresent || reader.ReadBit();
        if (sps.SeparatePlanes)
        {
            reader.Skip(H265SliceHeaderBits.ColourPlane);
        }

        var pocLsb = 0;
        var shortTerm = new H265ShortTermRps([], [], [], []);
        var longTerm = Array.Empty<H265LongTermRef>();
        var temporalMvp = false;
        if (!H265Nal.IsIdr(nalType))
        {
            pocLsb = (int)reader.Read(sps.Log2MaxPocLsb);
            shortTerm = ReadShortTerm(ref reader, sps);
            longTerm = ReadLongTerm(ref reader, sps);
            temporalMvp = sps.Tools.TemporalMvpEnabled && reader.ReadBit();
        }

        var saoLuma = sps.Tools.SaoEnabled && reader.ReadBit();
        var saoChroma = sps.Tools.SaoEnabled && sps.ChromaArrayType != ChromaSampling.Monochrome && reader.ReadBit();
        var partial = new H265SliceHeader(
            new(false, false, 0, false, 0, 0, []),
            sliceType,
            picOutput,
            pocLsb,
            shortTerm,
            longTerm,
            temporalMvp,
            saoLuma,
            saoChroma,
            NoRefs(),
            null,
            default,
            default);
        return ReadInterAndQuant(ref reader, partial, pps, sps);
    }

    /// <summary>Reads the reference list settings, weights, quantiser and loop filter settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="header">The header so far.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The completed header.</returns>
    private static H265SliceHeader ReadInterAndQuant(ref MsbBitReader reader, H265SliceHeader header, H265Pps pps, H265Sps sps)
    {
        var refs = header.Refs;
        H265PredWeights? weights = null;
        if (header.SliceType != H265SliceType.I)
        {
            refs = ReadRefs(ref reader, header, pps);
            var weighted = header.SliceType == H265SliceType.P ? pps.Flags.WeightedPred : pps.Flags.WeightedBipred;
            if (weighted)
            {
                weights = H265PredWeightReader.Read(ref reader, sps, new(refs.NumRefIdxL0, header.SliceType == H265SliceType.B ? refs.NumRefIdxL1 : 0));
            }

            refs = refs with { MaxMergeCand = MaxMergeCandidates - (int)ExpGolomb.ReadUe(ref reader) };
        }

        var quant = ReadQuant(ref reader, pps);
        var deblocking = ReadDeblocking(ref reader, pps, header.SaoLuma || header.SaoChroma);
        return header with { Refs = refs, Weights = weights, Quant = quant, Deblocking = deblocking };
    }

    /// <summary>Gets the reference settings of a slice that has none.</summary>
    /// <returns>The settings.</returns>
    private static H265RefSettings NoRefs() => new(0, 0, null, null, false, false, true, 0, 0);

    /// <summary>Reads the short-term reference picture set choice.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The index is out of range.</exception>
    private static H265ShortTermRps ReadShortTerm(ref MsbBitReader reader, H265Sps sps)
    {
        var count = sps.ShortTermSets.Count;
        if (!reader.ReadBit())
        {
            var start = reader.BitPosition;
            var set = H265ShortTermRpsReader.Read(ref reader, count, count, sps.ShortTermSets);
            return set with { HeaderBits = (int)(reader.BitPosition - start) };
        }

        var index = count > 1 ? (int)reader.Read(BitHelpers.CeilLog2(count)) : 0;
        return index < count ? sps.ShortTermSets[index] : throw new InvalidDataException("A slice picks a short-term reference picture set that does not exist.");
    }

    /// <summary>Reads the long-term reference pictures of the slice.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The pictures.</returns>
    /// <exception cref="InvalidDataException">The slice names too many pictures.</exception>
    private static H265LongTermRef[] ReadLongTerm(ref MsbBitReader reader, H265Sps sps)
    {
        if (!sps.LongTerm.Present)
        {
            return [];
        }

        var candidates = sps.LongTerm.PocLsb.Length;
        var fromSps = candidates > 0 ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        var total = fromSps + (int)ExpGolomb.ReadUe(ref reader);
        if (total > MaxLongTerm || fromSps > candidates)
        {
            throw new InvalidDataException("A slice names too many long-term reference pictures.");
        }

        var entries = new H265LongTermRef[total];
        var cycle = 0;
        for (var i = 0; i < total; i++)
        {
            var named = ReadLongTermIdentity(ref reader, sps, i < fromSps);
            var present = reader.ReadBit();
            var delta = present ? (int)ExpGolomb.ReadUe(ref reader) : 0;
            cycle = i == 0 || i == fromSps ? delta : cycle + delta;
            entries[i] = named with { MsbPresent = present, DeltaPocMsbCycle = cycle };
        }

        return entries;
    }

    /// <summary>Reads which long-term picture an entry names.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="fromSps">Whether the entry picks from the candidates in the sequence parameter set.</param>
    /// <returns>The entry with its low order count bits and used flag; the high order count bits are filled in by the caller.</returns>
    private static H265LongTermRef ReadLongTermIdentity(ref MsbBitReader reader, H265Sps sps, bool fromSps)
    {
        if (!fromSps)
        {
            return new((int)reader.Read(sps.Log2MaxPocLsb), reader.ReadBit(), false, 0);
        }

        var candidates = sps.LongTerm.PocLsb.Length;
        var index = candidates > 1 ? (int)reader.Read(BitHelpers.CeilLog2(candidates)) : 0;
        return new(sps.LongTerm.PocLsb[index], sps.LongTerm.Used[index], false, 0);
    }

    /// <summary>Reads the reference list settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="header">The header so far.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The settings, with the merge candidate count still to be read.</returns>
    /// <exception cref="InvalidDataException">A list is too long.</exception>
    private static H265RefSettings ReadRefs(ref MsbBitReader reader, H265SliceHeader header, H265Pps pps)
    {
        var isB = header.SliceType == H265SliceType.B;
        var sizes = ReadListSizes(ref reader, isB, pps);
        var total = header.NumPicTotalCurr;
        int[]? modification0 = null;
        int[]? modification1 = null;
        if (pps.Flags.ListsModificationPresent && total > 1)
        {
            modification0 = ReadModification(ref reader, sizes.L0, total);
            modification1 = isB ? ReadModification(ref reader, sizes.L1, total) : null;
        }

        var mvdL1Zero = isB && reader.ReadBit();
        var cabacInit = pps.Flags.CabacInitPresent && reader.ReadBit();
        var fromL0 = true;
        var collocatedIdx = 0;
        if (header.TemporalMvp)
        {
            fromL0 = !isB || reader.ReadBit();
            collocatedIdx = (fromL0 ? sizes.L0 : sizes.L1) > 1 ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        }

        return new(sizes.L0, sizes.L1, modification0, modification1, mvdL1Zero, cabacInit, fromL0, collocatedIdx, 0);
    }

    /// <summary>Reads the number of pictures in each reference list, which a slice may override.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="isB">Whether the slice is a B slice and so has a second list.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The sizes.</returns>
    /// <exception cref="InvalidDataException">A list is too long.</exception>
    private static ReferenceListCounts ReadListSizes(ref MsbBitReader reader, bool isB, H265Pps pps)
    {
        var l0 = pps.NumRefIdxL0Default;
        var l1 = isB ? pps.NumRefIdxL1Default : 0;
        if (reader.ReadBit())
        {
            l0 = 1 + (int)ExpGolomb.ReadUe(ref reader);
            l1 = isB ? 1 + (int)ExpGolomb.ReadUe(ref reader) : 0;
        }

        if (l0 > MaxRefIdx || l1 > MaxRefIdx)
        {
            throw new InvalidDataException("A reference picture list is too long.");
        }

        return new(l0, l1);
    }

    /// <summary>Reads the modification of one list: a flag, then an entry for each picture.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="count">The pictures in the list.</param>
    /// <param name="total">The pictures the slice refers to.</param>
    /// <returns>The entries, or <see langword="null"/> when the flag is off.</returns>
    private static int[]? ReadModification(ref MsbBitReader reader, int count, int total)
    {
        if (!reader.ReadBit())
        {
            return null;
        }

        var bits = BitHelpers.CeilLog2(total);
        var entries = new int[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] = (int)reader.Read(bits);
        }

        return entries;
    }

    /// <summary>Reads the quantiser settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The settings.</returns>
    private static H265SliceQuant ReadQuant(ref MsbBitReader reader, H265Pps pps)
    {
        var qp = pps.Quant.InitQp + ExpGolomb.ReadSe(ref reader);
        var cb = 0;
        var cr = 0;
        if (pps.Flags.SliceChromaQpOffsetsPresent)
        {
            cb = ExpGolomb.ReadSe(ref reader);
            cr = ExpGolomb.ReadSe(ref reader);
        }

        var chromaOffsetEnabled = pps.Range.ChromaQpOffsetListEnabled && reader.ReadBit();
        return new(qp, cb, cr, chromaOffsetEnabled);
    }

    /// <summary>Reads the loop filter settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="sao">Whether sample adaptive offset is on in the slice.</param>
    /// <returns>The settings.</returns>
    private static H265SliceDeblocking ReadDeblocking(ref MsbBitReader reader, H265Pps pps, bool sao)
    {
        var disabled = pps.Deblocking.Disabled;
        var beta = pps.Deblocking.BetaOffsetDiv2;
        var tc = pps.Deblocking.TcOffsetDiv2;
        if (pps.Deblocking.OverrideEnabled && reader.ReadBit())
        {
            disabled = reader.ReadBit();
            beta = 0;
            tc = 0;
            if (!disabled)
            {
                beta = ExpGolomb.ReadSe(ref reader);
                tc = ExpGolomb.ReadSe(ref reader);
            }
        }

        var across = pps.Flags.LoopFilterAcrossSlices;
        if (across && (sao || !disabled))
        {
            across = reader.ReadBit();
        }

        return new(disabled, beta, tc, across);
    }

    /// <summary>Reads the sizes of the subsets of slice data.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The size in bytes of each subset but the last.</returns>
    /// <exception cref="InvalidDataException">There are too many entry points.</exception>
    private static int[] ReadEntryPoints(ref MsbBitReader reader, H265Pps pps)
    {
        if (!pps.Tiles.Enabled && !pps.Flags.EntropyCodingSync)
        {
            return [];
        }

        var count = (int)ExpGolomb.ReadUe(ref reader);
        if (count > MaxEntryPoints)
        {
            throw new InvalidDataException("An H.265 slice has too many entry points.");
        }

        if (count == 0)
        {
            return [];
        }

        var bits = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var entries = new int[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] = 1 + (int)reader.Read(bits);
        }

        return entries;
    }

    /// <summary>Skips the slice header extension bytes.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="pps">The picture parameter set.</param>
    private static void SkipExtension(ref MsbBitReader reader, H265Pps pps)
    {
        if (!pps.Flags.SliceHeaderExtensionPresent)
        {
            return;
        }

        var length = (int)ExpGolomb.ReadUe(ref reader);
        BitHelpers.Skip(ref reader, length * H265SliceHeaderBits.Byte);
    }
}
