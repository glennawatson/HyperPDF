// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the macroblocks of B slices coded with CAVLC and has them reconstructed.</summary>
[DebuggerDisplay("H264BSliceDecoder")]
internal sealed class H264BSliceDecoder
{
    /// <summary>The 4x4 blocks along a macroblock side.</summary>
    private const int Side = 4;

    /// <summary>The 4x4 blocks along the side of an 8x8 partition.</summary>
    private const int Half = 2;

    /// <summary>The partitions of a 16x8 or 8x16 macroblock.</summary>
    private const int TwoPartitions = 2;

    /// <summary>The 8x8 partitions of a macroblock.</summary>
    private const int Groups = 4;

    /// <summary>The reference lists B slices use.</summary>
    private const int Lists = 2;

    /// <summary>The weighted_bipred_idc value that sends weights.</summary>
    private const int ExplicitWeights = 1;

    /// <summary>The weighted_bipred_idc value that works weights out from picture order counts.</summary>
    private const int ImplicitWeights = 2;

    /// <summary>The shape code of a sub-macroblock split into two 8x4 blocks.</summary>
    private const int Shape8x4 = 1;

    /// <summary>The shape code of a sub-macroblock split into two 4x8 blocks.</summary>
    private const int Shape4x8 = 2;

    /// <summary>The shape code of a sub-macroblock split into four 4x4 blocks.</summary>
    private const int Shape4x4 = 3;

    /// <summary>The largest sub_mb_type.</summary>
    private const int MaxSubType = 12;

    /// <summary>The largest mb_type of a B slice, counting the intra types.</summary>
    private const int MaxMbType = 48;

    /// <summary>The picture being decoded.</summary>
    private readonly H264Picture _picture;

    /// <summary>The two reference lists.</summary>
    private readonly H264RefLists _lists;

    /// <summary>Reads the intra macroblocks of the slice.</summary>
    private readonly H264IntraSliceDecoder _intra;

    /// <summary>Reads the residual of inter macroblocks.</summary>
    private readonly H264CavlcInterResidual _residual;

    /// <summary>The number of the slice being decoded.</summary>
    private readonly int _slice;

    /// <summary>The macroblock being decoded.</summary>
    private readonly H264CurrentMb _mb;

    /// <summary>Keeps the motion of the macroblock being decoded.</summary>
    private readonly H264BMotion _motion;

    /// <summary>Whether direct prediction works on 8x8 blocks.</summary>
    private readonly bool _inference;

    /// <summary>Whether every partition of the macroblock being decoded is at least 8x8, so it may use the 8x8 transform.</summary>
    private bool _allow8x8;

    /// <summary>Initializes a new instance of the <see cref="H264BSliceDecoder"/> class.</summary>
    /// <param name="picture">The picture to decode into.</param>
    /// <param name="sets">The sequence and picture parameter sets.</param>
    /// <param name="scale">The level scale factors.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="header">The slice header, which gives the starting quantiser, the weights and the direct mode.</param>
    /// <param name="lists">The two reference lists.</param>
    /// <param name="work">The decoder's working memory.</param>
    internal H264BSliceDecoder(H264Picture picture, H264SliceSets sets, H264LevelScale scale, int slice, H264SliceHeader header, H264RefLists lists, H264SliceWork work)
    {
        var (sps, pps) = sets;
        var startQp = pps.PicInitQp + header.Tail.QpDelta;
        _mb = work.Mb;
        _picture = picture;
        _lists = lists;
        _inference = sps.Direct8x8Inference;
        var reconstructor = new H264IntraReconstructor(picture, pps, scale);
        _intra = new(picture, pps, scale, slice, startQp, work.IntraMb);
        _residual = new(picture, _mb, slice, reconstructor, pps.Transform8x8) { Qp = startQp };
        _slice = slice;
        var mode = pps.WeightedBipredIdc switch
        {
            ExplicitWeights => H264InterPredictor.Explicit,
            ImplicitWeights => H264InterPredictor.Implicit,
            _ => H264InterPredictor.Unweighted,
        };
        _motion = new(picture, _mb, slice, lists, new(picture, mode, header.Refs.Weights, work), new(header.Refs.DirectSpatialMvPred, sps.Direct8x8Inference));
    }

    /// <summary>Decodes the macroblocks of a slice.</summary>
    /// <param name="rbsp">The payload of the slice's NAL unit.</param>
    /// <param name="dataBit">The bit index where the slice data starts.</param>
    /// <param name="firstMb">The address of the slice's first macroblock.</param>
    /// <exception cref="InvalidDataException">The slice data is damaged.</exception>
    internal void Decode(ReadOnlySpan<byte> rbsp, long dataBit, int firstMb)
    {
        var reader = new MsbBitReader(rbsp);
        reader.Seek(dataBit);
        var stop = ExpGolomb.StopBit(rbsp);
        var total = _picture.MbWidth * _picture.MbHeight;
        var address = firstMb;
        while (true)
        {
            var run = (int)ExpGolomb.ReadUe(ref reader);
            for (var skipped = 0; skipped < run; skipped++)
            {
                CheckAddress(address, total);
                SkipMacroblock(address);
                address++;
            }

            if ((run > 0 && !ExpGolomb.MoreData(reader.BitPosition, stop)) || reader.IsOverrun)
            {
                break;
            }

            CheckAddress(address, total);
            DecodeMacroblock(ref reader, address);
            address++;
            if (!ExpGolomb.MoreData(reader.BitPosition, stop) || reader.IsOverrun)
            {
                break;
            }
        }

        if (reader.IsOverrun)
        {
            throw new InvalidDataException("An H.264 slice is cut short.");
        }
    }

    /// <summary>Checks that a macroblock address is inside the picture.</summary>
    /// <param name="address">The address.</param>
    /// <param name="total">The macroblocks in the picture.</param>
    /// <exception cref="InvalidDataException">The address is past the last macroblock.</exception>
    private static void CheckAddress(int address, int total)
    {
        if (address >= total)
        {
            throw new InvalidDataException("An H.264 slice has more macroblocks than the picture.");
        }
    }

    /// <summary>Gets a partition of a macroblock with one or two partitions.</summary>
    /// <param name="type">The mb_type, 1 to 21.</param>
    /// <param name="index">The partition's number.</param>
    /// <returns>The partition.</returns>
    private static H264Partition PartitionOf(int type, int index)
    {
        var shape = H264BTypes.ShapeOf(type);
        if (shape == H264BTypes.Shape16x16)
        {
            return new(0, 0, Side, Side, H264MotionPrediction.Plain);
        }

        if (shape == H264BTypes.Shape16x8)
        {
            return new(0, index * Half, Side, Half, index == 0 ? H264MotionPrediction.Upper16x8 : H264MotionPrediction.Lower16x8);
        }

        return new(index * Half, 0, Half, Side, index == 0 ? H264MotionPrediction.Left8x16 : H264MotionPrediction.Right8x16);
    }

    /// <summary>Gets the position of a sub-partition inside its 8x8 partition, in 4x4 blocks.</summary>
    /// <param name="shape">The shape: 0 for 8x8, 1 for 8x4, 2 for 4x8, 3 for 4x4.</param>
    /// <param name="index">The sub-partition's number.</param>
    /// <returns>The sub-partition.</returns>
    private static H264Partition SubPartition(int shape, int index) => shape switch
    {
        Shape8x4 => new(0, index, Half, 1, H264MotionPrediction.Plain),
        Shape4x8 => new(index, 0, 1, Half, H264MotionPrediction.Plain),
        Shape4x4 => new(index & 1, index >> 1, 1, 1, H264MotionPrediction.Plain),
        _ => new(0, 0, Half, Half, H264MotionPrediction.Plain),
    };

    /// <summary>Gets how many sub-partitions a shape has.</summary>
    /// <param name="shape">The shape: 0 for 8x8, 1 for 8x4, 2 for 4x8, 3 for 4x4.</param>
    /// <returns>1, 2 or 4.</returns>
    private static int SubPartitionCount(int shape) => shape switch
    {
        Shape8x4 or Shape4x8 => TwoPartitions,
        Shape4x4 => Groups,
        _ => 1,
    };

    /// <summary>Gets the partition of an 8x8 group.</summary>
    /// <param name="group">The group, 0 to 3.</param>
    /// <returns>The partition.</returns>
    private static H264Partition GroupPartition(int group) => new((group & 1) * Half, (group >> 1) * Half, Half, Half, H264MotionPrediction.Plain);

    /// <summary>Sets up the macroblock being decoded as an inter macroblock with no motion yet.</summary>
    /// <param name="address">The macroblock's address.</param>
    private void BeginInter(int address)
    {
        var mb = _mb;
        mb.Address = address;
        mb.X = address % _picture.MbWidth;
        mb.Y = address / _picture.MbWidth;
        mb.Kind = H264MbKind.Inter;
        mb.CbpLuma = 0;
        mb.CbpChroma = 0;
        mb.ClearCoefficients();
        _picture.SliceOfMb[address] = _slice;
        _picture.Kinds[address] = H264MbKind.Inter;
        Array.Clear(_picture.Counts, address * H264Picture.CountsPerMb, H264Picture.CountsPerMb);
        _picture.ClearMotion(address);
        _allow8x8 = true;
        _motion.Begin();
    }

    /// <summary>Reconstructs a B_Skip macroblock: direct prediction with no residual.</summary>
    /// <param name="address">The macroblock's address.</param>
    private void SkipMacroblock(int address)
    {
        BeginInter(address);
        _picture.Skipped[address] = true;
        PredictDirect();
        _picture.Qp[address] = (sbyte)_residual.Qp;
    }

    /// <summary>Derives and predicts all four 8x8 groups of the macroblock directly.</summary>
    private void PredictDirect()
    {
        for (var group = 0; group < Groups; group++)
        {
            _motion.Direct(group);
            _motion.MarkAbsent(0, GroupPartition(group));
            _motion.MarkAbsent(1, GroupPartition(group));
            CompensateDirect(group);
        }
    }

    /// <summary>Predicts the samples of a direct 8x8 group, in 8x8 blocks or 4x4 blocks according to the sequence.</summary>
    /// <param name="group">The group, 0 to 3.</param>
    private void CompensateDirect(int group)
    {
        var whole = GroupPartition(group);
        if (_inference)
        {
            _motion.Compensate(whole);
            return;
        }

        for (var part = 0; part < Groups; part++)
        {
            _motion.Compensate(new(whole.Column + (part & 1), whole.Row + (part >> 1), 1, 1, H264MotionPrediction.Plain));
        }
    }

    /// <summary>Reads and reconstructs one macroblock.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="address">The macroblock's address.</param>
    /// <exception cref="InvalidDataException">The macroblock is damaged.</exception>
    private void DecodeMacroblock(ref MsbBitReader reader, int address)
    {
        var type = (int)ExpGolomb.ReadUe(ref reader);
        if (type > MaxMbType)
        {
            throw new InvalidDataException("An H.264 macroblock type is not valid for a B slice.");
        }

        if (type >= H264BTypes.IntraOffset)
        {
            _intra.Qp = _residual.Qp;
            _intra.DecodeMacroblock(ref reader, address, type - H264BTypes.IntraOffset);
            _residual.Qp = _intra.Qp;
            return;
        }

        BeginInter(address);
        if (type == H264BTypes.Direct16x16)
        {
            _allow8x8 = _inference;
            PredictDirect();
        }
        else if (type == H264BTypes.Partitions8x8)
        {
            ReadSubMacroblocks(ref reader);
        }
        else
        {
            ReadPartitions(ref reader, type);
        }

        _residual.Read(ref reader, _allow8x8);
    }

    /// <summary>Reads the reference indices and motion vectors of a macroblock with one or two partitions.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="type">The mb_type, 1 to 21.</param>
    private void ReadPartitions(ref MsbBitReader reader, int type)
    {
        var count = H264BTypes.HasTwoPartitions(type) ? TwoPartitions : 1;
        Span<int> refIdx = stackalloc int[Lists * TwoPartitions];
        refIdx.Fill(-1);
        for (var list = 0; list < Lists; list++)
        {
            for (var index = 0; index < count; index++)
            {
                if ((H264BTypes.ModeOf(type, index) & (1 << list)) != 0)
                {
                    refIdx[(list * TwoPartitions) + index] = ReadRefIdx(ref reader, list);
                }
            }
        }

        for (var list = 0; list < Lists; list++)
        {
            for (var index = 0; index < count; index++)
            {
                var partition = PartitionOf(type, index);
                var reference = refIdx[(list * TwoPartitions) + index];
                if (reference >= 0)
                {
                    ReadVector(ref reader, list, partition, reference);
                }
                else
                {
                    _motion.MarkAbsent(list, partition);
                }
            }
        }

        for (var index = 0; index < count; index++)
        {
            _motion.Compensate(PartitionOf(type, index));
        }
    }

    /// <summary>Reads the reference indices and motion vectors of a macroblock of four 8x8 partitions.</summary>
    /// <param name="reader">The bits.</param>
    /// <exception cref="InvalidDataException">A sub_mb_type is not valid.</exception>
    private void ReadSubMacroblocks(ref MsbBitReader reader)
    {
        Span<int> subTypes = stackalloc int[Groups];
        ReadSubTypes(ref reader, subTypes);
        Span<int> refIdx = stackalloc int[Lists * Groups];
        refIdx.Fill(-1);
        for (var list = 0; list < Lists; list++)
        {
            for (var group = 0; group < Groups; group++)
            {
                if ((H264BTypes.SubModeOf(subTypes[group]) & (1 << list)) != 0)
                {
                    refIdx[(list * Groups) + group] = ReadRefIdx(ref reader, list);
                }
            }
        }

        for (var list = 0; list < Lists; list++)
        {
            for (var group = 0; group < Groups; group++)
            {
                ReadGroupVectors(ref reader, list, group, subTypes[group], refIdx[(list * Groups) + group]);
            }
        }

        for (var group = 0; group < Groups; group++)
        {
            CompensateGroup(group, subTypes[group]);
        }
    }

    /// <summary>Reads the four sub_mb_types, works out whether the 8x8 transform is still allowed, and derives the motion of the direct groups.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="subTypes">Receives the four sub_mb_types.</param>
    /// <exception cref="InvalidDataException">A sub_mb_type is not valid.</exception>
    private void ReadSubTypes(ref MsbBitReader reader, scoped Span<int> subTypes)
    {
        for (var group = 0; group < Groups; group++)
        {
            var subType = (int)ExpGolomb.ReadUe(ref reader);
            if (subType > MaxSubType)
            {
                throw new InvalidDataException("An H.264 sub-macroblock type is not valid.");
            }

            subTypes[group] = subType;
            _allow8x8 &= subType == H264BTypes.SubDirect ? _inference : H264BTypes.SubShapeOf(subType) == 0;
            if (subType == H264BTypes.SubDirect)
            {
                _motion.Direct(group);
            }
        }
    }

    /// <summary>Reads the motion vectors of the sub-partitions of an 8x8 group for one list.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="list">0 or 1.</param>
    /// <param name="group">The group, 0 to 3.</param>
    /// <param name="subType">The group's sub_mb_type.</param>
    /// <param name="reference">The group's reference index for the list, or -1 when it does not use the list.</param>
    private void ReadGroupVectors(ref MsbBitReader reader, int list, int group, int subType, int reference)
    {
        var whole = GroupPartition(group);
        if (reference < 0)
        {
            _motion.MarkAbsent(list, whole);
            return;
        }

        var shape = H264BTypes.SubShapeOf(subType);
        for (var part = 0; part < SubPartitionCount(shape); part++)
        {
            var sub = SubPartition(shape, part);
            ReadVector(ref reader, list, sub with { Column = whole.Column + sub.Column, Row = whole.Row + sub.Row }, reference);
        }
    }

    /// <summary>Predicts the samples of an 8x8 group after its motion has been read.</summary>
    /// <param name="group">The group, 0 to 3.</param>
    /// <param name="subType">The group's sub_mb_type.</param>
    private void CompensateGroup(int group, int subType)
    {
        if (subType == H264BTypes.SubDirect)
        {
            CompensateDirect(group);
            return;
        }

        var whole = GroupPartition(group);
        var shape = H264BTypes.SubShapeOf(subType);
        for (var part = 0; part < SubPartitionCount(shape); part++)
        {
            var sub = SubPartition(shape, part);
            _motion.Compensate(sub with { Column = whole.Column + sub.Column, Row = whole.Row + sub.Row });
        }
    }

    /// <summary>Reads ref_idx for a list.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="list">0 or 1.</param>
    /// <returns>The reference index.</returns>
    /// <exception cref="InvalidDataException">The index names a frame that is not in the list.</exception>
    private int ReadRefIdx(ref MsbBitReader reader, int list)
    {
        var count = list == 0 ? _lists.L0.Length : _lists.L1.Length;
        if (count == 1)
        {
            return 0;
        }

        var index = ExpGolomb.ReadTe(ref reader, count - 1);
        return index < count ? index : throw new InvalidDataException("An H.264 reference index is not valid.");
    }

    /// <summary>Reads a motion vector difference for a list, adds it to the prediction, and records the result.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="list">0 or 1.</param>
    /// <param name="partition">The partition.</param>
    /// <param name="reference">The partition's reference index for the list.</param>
    private void ReadVector(ref MsbBitReader reader, int list, H264Partition partition, int reference)
    {
        var predicted = _motion.Predict(list, partition, reference);
        var difference = new MotionVector(ExpGolomb.ReadSe(ref reader), ExpGolomb.ReadSe(ref reader));
        _motion.Store(list, partition, reference, new(new(predicted.X + difference.X, predicted.Y + difference.Y), difference));
    }
}
