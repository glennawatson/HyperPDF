// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads slice headers.</summary>
internal static class H264SliceHeaderReader
{
    /// <summary>The most memory management control operations or list modifications a header may carry.</summary>
    private const int MaxOperations = 66;

    /// <summary>The slice types are coded 0 to 4 and again 5 to 9; the second range says all slices of the picture have the same type.</summary>
    private const int SliceTypeRange = 5;

    /// <summary>The first slice type number that is out of range.</summary>
    private const int SliceTypeLimit = 10;

    /// <summary>The list modification operation that ends the list.</summary>
    private const int LastModificationIdc = 3;

    /// <summary>The largest list modification operation that carries a number.</summary>
    private const int MaxModificationIdc = 2;

    /// <summary>The largest memory management control operation.</summary>
    private const int MaxMmco = 6;

    /// <summary>The memory management operation that ends the list.</summary>
    private const int EndOfOperations = 0;

    /// <summary>The memory management operation that marks a short-term picture as unused.</summary>
    private const int MmcoShortToUnused = 1;

    /// <summary>The memory management operation that marks a long-term picture as unused.</summary>
    private const int MmcoLongToUnused = 2;

    /// <summary>The memory management operation that turns a short-term picture into a long-term one.</summary>
    private const int MmcoShortToLong = 3;

    /// <summary>The memory management operation that sets the largest long-term frame index.</summary>
    private const int MmcoMaxLongTerm = 4;

    /// <summary>The memory management operation that makes the current picture long-term.</summary>
    private const int MmcoCurrentToLong = 6;

    /// <summary>The largest CABAC initialisation number.</summary>
    private const int MaxCabacInit = 2;

    /// <summary>The largest weight denominator exponent.</summary>
    private const int MaxLog2Denom = 7;

    /// <summary>The most pictures in a reference list.</summary>
    private const int MaxRefIdx = 32;

    /// <summary>The message of an exception for a header that is damaged.</summary>
    private const string Damaged = "An H.264 slice header is damaged.";

    /// <summary>Reads a slice header.</summary>
    /// <param name="rbsp">The payload of the NAL unit, without its header and emulation prevention bytes.</param>
    /// <param name="nalHeader">The NAL unit header byte.</param>
    /// <param name="sets">The parameter sets received so far.</param>
    /// <param name="sps">Receives the sequence parameter set the slice uses.</param>
    /// <param name="pps">Receives the picture parameter set the slice uses.</param>
    /// <param name="dataBit">Receives the bit index where the slice data starts.</param>
    /// <returns>The header.</returns>
    /// <exception cref="InvalidDataException">The header is damaged or names a parameter set that was not sent.</exception>
    /// <exception cref="NotSupportedException">The slice is a switching slice.</exception>
    internal static H264SliceHeader Read(ReadOnlySpan<byte> rbsp, byte nalHeader, H264ParameterSets sets, out H264Sps sps, out H264Pps pps, out long dataBit)
    {
        var reader = new MsbBitReader(rbsp);
        var isIdr = H264Nal.TypeOf(nalHeader) == H264Nal.IdrSlice;
        var firstMb = (int)ExpGolomb.ReadUe(ref reader);
        var type = (int)ExpGolomb.ReadUe(ref reader);
        var ppsId = (int)ExpGolomb.ReadUe(ref reader);
        Resolve(sets, ppsId, out sps, out pps);
        if (type >= SliceTypeLimit || sps.SeparateColourPlanes)
        {
            throw new InvalidDataException(Damaged);
        }

        var sliceType = type % SliceTypeRange;
        var picture = ReadPicture(ref reader, isIdr, sps, pps);
        var refs = ReadReferences(ref reader, sliceType, sps, pps);
        var nalRefIdc = H264Nal.ReferenceIdcOf(nalHeader);
        var marking = nalRefIdc != 0 ? ReadMarking(ref reader, isIdr) : H264Marking.None;
        var tail = ReadTail(ref reader, sliceType, pps);
        if (reader.IsOverrun)
        {
            throw new InvalidDataException("An H.264 slice header is cut short.");
        }

        dataBit = reader.BitPosition;
        return new(nalRefIdc, isIdr, firstMb, sliceType, ppsId, picture, refs, marking, tail);
    }

    /// <summary>Finds the parameter sets a slice names.</summary>
    /// <param name="sets">The parameter sets received so far.</param>
    /// <param name="ppsId">The number of the picture parameter set.</param>
    /// <param name="sps">Receives the sequence parameter set.</param>
    /// <param name="pps">Receives the picture parameter set.</param>
    /// <exception cref="InvalidDataException">A set was not sent.</exception>
    private static void Resolve(H264ParameterSets sets, int ppsId, out H264Sps sps, out H264Pps pps)
    {
        pps = sets.GetPps(ppsId) ?? throw new InvalidDataException("An H.264 slice names a picture parameter set that was not sent.");
        sps = sets.GetSps(pps.SpsId) ?? throw new InvalidDataException("An H.264 slice names a sequence parameter set that was not sent.");
    }

    /// <summary>Reads the part of the header that names the picture: the frame number, structure and picture order count.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="isIdr">Whether the slice is part of an IDR picture.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>What was read.</returns>
    private static H264PictureId ReadPicture(ref MsbBitReader reader, bool isIdr, H264Sps sps, H264Pps pps)
    {
        var frameNum = (int)reader.Read(sps.Log2MaxFrameNum);
        var fieldPic = !sps.FrameMbsOnly && reader.ReadBit();
        var bottomField = fieldPic && reader.ReadBit();
        var idrPicId = isIdr ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        var poc = ReadPocFields(ref reader, sps.Poc, pps.BottomFieldPicOrderPresent && !fieldPic);
        if (pps.RedundantPicCntPresent)
        {
            _ = ExpGolomb.ReadUe(ref reader);
        }

        return new(frameNum, fieldPic, bottomField, idrPicId, poc.Lsb, poc.DeltaBottom, poc.Delta0, poc.Delta1);
    }

    /// <summary>Reads the fields that give a picture its order count.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="poc">How the sequence works out the picture order count.</param>
    /// <param name="hasBottomDelta">Whether the header carries an offset for the bottom field.</param>
    /// <returns>What was read.</returns>
    private static H264PocFields ReadPocFields(ref MsbBitReader reader, H264PocSettings poc, bool hasBottomDelta)
    {
        if (poc.Type == 0)
        {
            var lsb = (int)reader.Read(poc.Log2MaxLsb);
            return new(lsb, hasBottomDelta ? ExpGolomb.ReadSe(ref reader) : 0, 0, 0);
        }

        if (poc.Type != 1 || poc.DeltaAlwaysZero)
        {
            return default;
        }

        var delta0 = ExpGolomb.ReadSe(ref reader);
        return new(0, 0, delta0, hasBottomDelta ? ExpGolomb.ReadSe(ref reader) : 0);
    }

    /// <summary>Reads the part of the header that says which pictures a slice refers to and how it weights them.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sliceType">The slice type.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>What was read.</returns>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    private static H264RefSetup ReadReferences(ref MsbBitReader reader, int sliceType, H264Sps sps, H264Pps pps)
    {
        var isB = sliceType == H264SliceHeader.B;
        var isIntra = sliceType is H264SliceHeader.I or H264SliceHeader.Si;
        var direct = isB && reader.ReadBit();
        var l0 = 0;
        var l1 = 0;
        if (!isIntra)
        {
            (l0, l1) = ReadActiveCounts(ref reader, isB, pps);
        }

        var modificationsL0 = isIntra ? [] : ReadModifications(ref reader);
        var modificationsL1 = isB ? ReadModifications(ref reader) : [];
        var weighted = isB ? pps.WeightedBipredIdc == 1 : pps.WeightedPred && !isIntra;
        var table = weighted ? ReadWeights(ref reader, isB, new(l0, l1), sps.ChromaArrayType) : null;
        return new(direct, l0, l1, modificationsL0, modificationsL1, table);
    }

    /// <summary>Reads the numbers of pictures in the reference lists, which a slice may change from the picture parameter set's.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="isB">Whether the slice is a B slice.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The numbers for list 0 and list 1.</returns>
    /// <exception cref="InvalidDataException">A number is too big.</exception>
    private static ReferenceListCounts ReadActiveCounts(ref MsbBitReader reader, bool isB, H264Pps pps)
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
            throw new InvalidDataException(Damaged);
        }

        return new(l0, l1);
    }

    /// <summary>Reads the reference list modification of one list.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The operations, which are empty when the list is not modified.</returns>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    private static H264RefModification[] ReadModifications(ref MsbBitReader reader)
    {
        if (!reader.ReadBit())
        {
            return [];
        }

        var operations = new List<H264RefModification>();
        while (true)
        {
            var idc = (int)ExpGolomb.ReadUe(ref reader);
            if (idc == LastModificationIdc)
            {
                return [.. operations];
            }

            if (idc > MaxModificationIdc || operations.Count > MaxOperations || reader.IsOverrun)
            {
                throw new InvalidDataException(Damaged);
            }

            operations.Add(new(idc, (int)ExpGolomb.ReadUe(ref reader)));
        }
    }

    /// <summary>Reads the table of explicit prediction weights.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="isB">Whether the slice is a B slice.</param>
    /// <param name="counts">The numbers of pictures in the reference lists.</param>
    /// <param name="chromaArrayType">The type of colour array.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    private static H264WeightTable ReadWeights(ref MsbBitReader reader, bool isB, ReferenceListCounts counts, int chromaArrayType)
    {
        var lumaDenom = (int)ExpGolomb.ReadUe(ref reader);
        var chromaDenom = chromaArrayType != 0 ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        if (lumaDenom > MaxLog2Denom || chromaDenom > MaxLog2Denom)
        {
            throw new InvalidDataException(Damaged);
        }

        var l0 = ReadWeightList(ref reader, counts.L0, lumaDenom, chromaDenom, chromaArrayType);
        var l1 = isB ? ReadWeightList(ref reader, counts.L1, lumaDenom, chromaDenom, chromaArrayType) : [];
        return new(lumaDenom, chromaDenom, l0, l1);
    }

    /// <summary>Reads the weights of one reference list.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="count">The pictures in the list.</param>
    /// <param name="lumaDenom">The log2 of the denominator of the luma weights.</param>
    /// <param name="chromaDenom">The log2 of the denominator of the chroma weights.</param>
    /// <param name="chromaArrayType">The type of colour array.</param>
    /// <returns>The weights.</returns>
    private static H264Weight[] ReadWeightList(ref MsbBitReader reader, int count, int lumaDenom, int chromaDenom, int chromaArrayType)
    {
        var weights = new H264Weight[count];
        for (var index = 0; index < count; index++)
        {
            var hasLuma = reader.ReadBit();
            var lumaWeight = hasLuma ? ExpGolomb.ReadSe(ref reader) : 1 << lumaDenom;
            var lumaOffset = hasLuma ? ExpGolomb.ReadSe(ref reader) : 0;
            var hasChroma = chromaArrayType != 0 && reader.ReadBit();
            var chroma = hasChroma ? ReadChromaWeights(ref reader) : H264ChromaWeights.Default(chromaDenom);
            weights[index] = new(lumaWeight, lumaOffset, chroma.CbWeight, chroma.CbOffset, chroma.CrWeight, chroma.CrOffset, hasLuma, hasChroma);
        }

        return weights;
    }

    /// <summary>Reads the chroma weights and offsets of one reference picture.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>What was read.</returns>
    private static H264ChromaWeights ReadChromaWeights(ref MsbBitReader reader)
    {
        var blueWeight = ExpGolomb.ReadSe(ref reader);
        var blueOffset = ExpGolomb.ReadSe(ref reader);
        var redWeight = ExpGolomb.ReadSe(ref reader);
        var redOffset = ExpGolomb.ReadSe(ref reader);
        return new(blueWeight, blueOffset, redWeight, redOffset);
    }

    /// <summary>Reads the decoded reference picture marking.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="isIdr">Whether the slice is part of an IDR picture.</param>
    /// <returns>The marking.</returns>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    private static H264Marking ReadMarking(ref MsbBitReader reader, bool isIdr)
    {
        if (isIdr)
        {
            return new(reader.ReadBit(), reader.ReadBit(), false, []);
        }

        if (!reader.ReadBit())
        {
            return H264Marking.None;
        }

        var operations = new List<H264Mmco>();
        while (true)
        {
            var operation = (int)ExpGolomb.ReadUe(ref reader);
            if (operation == EndOfOperations)
            {
                return new(false, false, true, [.. operations]);
            }

            if (operation > MaxMmco || operations.Count > MaxOperations || reader.IsOverrun)
            {
                throw new InvalidDataException(Damaged);
            }

            operations.Add(ReadOperation(ref reader, operation));
        }
    }

    /// <summary>Reads the numbers that follow a memory management control operation.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The operation with its numbers.</returns>
    private static H264Mmco ReadOperation(ref MsbBitReader reader, int operation)
    {
        var difference = operation is MmcoShortToUnused or MmcoShortToLong ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        var longTermPicNum = operation == MmcoLongToUnused ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        var longTermFrameIdx = operation is MmcoShortToLong or MmcoMaxLongTerm or MmcoCurrentToLong ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        return new(operation, difference, longTermPicNum, longTermFrameIdx);
    }

    /// <summary>Reads the end of the header: the CABAC initialisation, the quantiser change and the deblocking control.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sliceType">The slice type.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>What was read.</returns>
    /// <exception cref="InvalidDataException">The header is damaged.</exception>
    /// <exception cref="NotSupportedException">The slice is a switching slice.</exception>
    private static H264SliceTail ReadTail(ref MsbBitReader reader, int sliceType, H264Pps pps)
    {
        var isIntra = sliceType is H264SliceHeader.I or H264SliceHeader.Si;
        var cabacInit = pps.Cabac && !isIntra ? (int)ExpGolomb.ReadUe(ref reader) : 0;
        var quantiserDelta = ExpGolomb.ReadSe(ref reader);
        if (cabacInit > MaxCabacInit)
        {
            throw new InvalidDataException(Damaged);
        }

        if (sliceType is H264SliceHeader.Sp or H264SliceHeader.Si)
        {
            throw new NotSupportedException("H.264 switching slices are not supported.");
        }

        if (!pps.DeblockingControlPresent)
        {
            return new(cabacInit, quantiserDelta, 0, 0, 0);
        }

        var disable = (int)ExpGolomb.ReadUe(ref reader);
        var alpha = disable != 1 ? ExpGolomb.ReadSe(ref reader) : 0;
        var beta = disable != 1 ? ExpGolomb.ReadSe(ref reader) : 0;
        return new(cabacInit, quantiserDelta, disable, alpha, beta);
    }
}
