// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>Keeps the motion of the macroblock being decoded in a B slice for both reference lists, predicts vectors, derives direct motion, and predicts the samples.</summary>
[DebuggerDisplay("H264BMotion: spatial {_spatial}")]
internal sealed class H264BMotion
{
    /// <summary>The 4x4 blocks along a macroblock side.</summary>
    private const int Side = 4;

    /// <summary>The 4x4 blocks along the side of an 8x8 group.</summary>
    private const int Half = 2;

    /// <summary>The index of the last row or column of 4x4 blocks.</summary>
    private const int Last = 3;

    /// <summary>The largest size of a motion vector difference kept for the contexts.</summary>
    private const int MaxStoredMvd = 255;

    /// <summary>The range of a motion vector component, in quarter samples, that counts as no motion for direct prediction.</summary>
    private const int ColZeroRange = 1;

    /// <summary>The rounding of the temporal direct scale.</summary>
    private const int ScaleRound = 128;

    /// <summary>The shift of the temporal direct scale.</summary>
    private const int ScaleShift = 8;

    /// <summary>The picture being decoded.</summary>
    private readonly H264Picture _picture;

    /// <summary>The macroblock being decoded.</summary>
    private readonly H264CurrentMb _mb;

    /// <summary>The two reference lists.</summary>
    private readonly H264RefLists _lists;

    /// <summary>Whether direct prediction works from neighbours (spatial) rather than from the co-located picture (temporal).</summary>
    private readonly bool _spatial;

    /// <summary>Whether direct prediction works on 8x8 blocks, taking the motion of the macroblock's corners.</summary>
    private readonly bool _inference;

    /// <summary>Predicts vectors for list 0.</summary>
    private readonly H264MotionPrediction _predict0;

    /// <summary>Predicts vectors for list 1.</summary>
    private readonly H264MotionPrediction _predict1;

    /// <summary>Predicts samples.</summary>
    private readonly H264InterPredictor _predictor;

    /// <summary>The reference indices and vectors spatial direct prediction found for the macroblock.</summary>
    private H264DirectBlock _spatialMotion;

    /// <summary>Whether <see cref="_spatialMotion"/> has been worked out for the macroblock.</summary>
    private bool _directReady;

    /// <summary>Initializes a new instance of the <see cref="H264BMotion"/> class.</summary>
    /// <param name="picture">The picture being decoded.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="lists">The two reference lists.</param>
    /// <param name="predictor">Predicts samples.</param>
    /// <param name="flags">Whether direct prediction is spatial and whether it works on 8x8 blocks.</param>
    internal H264BMotion(H264Picture picture, H264CurrentMb mb, int slice, H264RefLists lists, H264InterPredictor predictor, H264DirectFlags flags)
    {
        _picture = picture;
        _mb = mb;
        _lists = lists;
        _predictor = predictor;
        _spatial = flags.Spatial;
        _inference = flags.Inference;
        _predict0 = new(picture, mb, slice, picture.Field0);
        _predict1 = new(picture, mb, slice, picture.Field1);
    }

    /// <summary>Starts a macroblock: none of its blocks has motion yet.</summary>
    internal void Begin()
    {
        _predict0.Begin();
        _predict1.Begin();
        _directReady = false;
    }

    /// <summary>Predicts the vector of a partition for a list.</summary>
    /// <param name="list">0 or 1.</param>
    /// <param name="partition">The partition.</param>
    /// <param name="refIdx">The partition's reference index in that list.</param>
    /// <returns>The predicted vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MotionVector Predict(int list, H264Partition partition, int refIdx) => (list == 0 ? _predict0 : _predict1).Predict(partition, refIdx);

    /// <summary>Gets the motion field of a list.</summary>
    /// <param name="list">0 or 1.</param>
    /// <returns>The field.</returns>
    internal H264MotionField FieldOf(int list) => list == 0 ? _picture.Field0 : _picture.Field1;

    /// <summary>Gets the sum of the sizes of the motion vector differences of the blocks to the left of and above a partition, for the contexts of CABAC.</summary>
    /// <param name="list">0 or 1.</param>
    /// <param name="vertical">Whether to sum the vertical components.</param>
    /// <param name="partition">The partition.</param>
    /// <returns>The sum.</returns>
    internal int NeighbourMvd(int list, bool vertical, H264Partition partition)
    {
        var field = FieldOf(list);
        var differences = vertical ? field.MvdY : field.MvdX;
        return MvdAt(differences, partition.Column - 1, partition.Row) + MvdAt(differences, partition.Column, partition.Row - 1);
    }

    /// <summary>Gets the reference index of the block at a position for a list.</summary>
    /// <param name="list">0 or 1.</param>
    /// <param name="column">The block's column relative to the current macroblock.</param>
    /// <param name="row">The block's row relative to the current macroblock.</param>
    /// <returns>The reference index, or -1 when the block is missing, intra coded or does not use the list.</returns>
    internal int NeighbourRef(int list, int column, int row)
    {
        var address = (list == 0 ? _predict0 : _predict1).MacroblockAt(column, row);
        var group = ((((row + Side) & Last) >> 1) * Half) + (((column + Side) & Last) >> 1);
        var skip = address < 0 || ((_picture.DirectGroups[address] >> group) & 1) != 0;
        return skip ? -1 : FieldOf(list).RefIdx[(address * H264Picture.GroupsPerMb) + group];
    }

    /// <summary>Records the motion of a partition for a list.</summary>
    /// <param name="list">0 or 1.</param>
    /// <param name="partition">The partition.</param>
    /// <param name="refIdx">The reference index.</param>
    /// <param name="motion">The vector and the vector difference that was read.</param>
    internal void Store(int list, H264Partition partition, int refIdx, H264StoredVector motion)
    {
        var field = FieldOf(list);
        var references = list == 0 ? _lists.L0 : _lists.L1;
        var address = _mb.Address;
        for (var r = partition.Row; r < partition.Row + partition.Height; r++)
        {
            for (var c = partition.Column; c < partition.Column + partition.Width; c++)
            {
                var index = (address * H264Picture.BlocksPerMb) + (r * Side) + c;
                field.MvX[index] = (short)motion.Vector.X;
                field.MvY[index] = (short)motion.Vector.Y;
                field.MvdX[index] = (byte)Math.Min(Math.Abs(motion.Difference.X), MaxStoredMvd);
                field.MvdY[index] = (byte)Math.Min(Math.Abs(motion.Difference.Y), MaxStoredMvd);
                var group = (address * H264Picture.GroupsPerMb) + ((r >> 1) * Half) + (c >> 1);
                field.RefIdx[group] = (sbyte)refIdx;
                field.RefUid[group] = references[refIdx].Picture.Uid;
            }
        }

        (list == 0 ? _predict0 : _predict1).MarkDecoded(partition);
    }

    /// <summary>Records the reference index of a partition for a list as soon as it is read, because the indices of later partitions use it as context.</summary>
    /// <param name="list">0 or 1.</param>
    /// <param name="partition">The partition.</param>
    /// <param name="refIdx">The reference index.</param>
    internal void StoreRefIdx(int list, H264Partition partition, int refIdx)
    {
        var field = FieldOf(list);
        for (var r = partition.Row; r < partition.Row + partition.Height; r++)
        {
            for (var c = partition.Column; c < partition.Column + partition.Width; c++)
            {
                field.RefIdx[(_mb.Address * H264Picture.GroupsPerMb) + ((r >> 1) * Half) + (c >> 1)] = (sbyte)refIdx;
            }
        }
    }

    /// <summary>Records that a partition does not use a list, so the partitions after it find it decoded but without motion.</summary>
    /// <param name="list">0 or 1.</param>
    /// <param name="partition">The partition.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkAbsent(int list, H264Partition partition) => (list == 0 ? _predict0 : _predict1).MarkDecoded(partition);

    /// <summary>Derives and records the motion of an 8x8 group that uses direct prediction; the group counts as decoded once <see cref="MarkAbsent"/> is called for it.</summary>
    /// <param name="group">The group, 0 to 3.</param>
    internal void Direct(int group)
    {
        if (!_directReady)
        {
            _directReady = true;
            if (_spatial)
            {
                _spatialMotion = SpatialMotion();
            }
        }

        var firstColumn = (group & 1) * Half;
        var firstRow = (group >> 1) * Half;
        var block = default(H264DirectBlock);
        for (var r = 0; r < Half; r++)
        {
            for (var c = 0; c < Half; c++)
            {
                var column = _inference ? (group & 1) * Last : firstColumn + c;
                var row = _inference ? (group >> 1) * Last : firstRow + r;
                var col = Colocated(column, row);
                block = _spatial ? SpatialBlock(col) : TemporalBlock(col);
                WriteDirect(firstColumn + c, firstRow + r, block);
            }
        }

        WriteGroupReferences(group, block);
        _picture.DirectGroups[_mb.Address] |= (byte)(1 << group);
    }

    /// <summary>Predicts the samples of a partition from the motion recorded for it.</summary>
    /// <param name="partition">The partition; the motion must be the same all over it.</param>
    internal void Compensate(H264Partition partition)
    {
        var address = _mb.Address;
        var block = (address * H264Picture.BlocksPerMb) + (partition.Row * Side) + partition.Column;
        var group = (address * H264Picture.GroupsPerMb) + ((partition.Row >> 1) * Half) + (partition.Column >> 1);
        var ref0 = _picture.Field0.RefIdx[group];
        var ref1 = _picture.Field1.RefIdx[group];
        var first = ref0 >= 0 ? new H264ListMotion(_lists.L0[ref0], ref0, new(_picture.Field0.MvX[block], _picture.Field0.MvY[block])) : default;
        var second = ref1 >= 0 ? new H264ListMotion(_lists.L1[ref1], ref1, new(_picture.Field1.MvX[block], _picture.Field1.MvY[block])) : default;
        _predictor.Compensate(_mb, partition, new(first, second));
    }

    /// <summary>Takes the smaller of two reference indices when both are valid, otherwise the other one.</summary>
    /// <param name="a">One index, -1 when missing.</param>
    /// <param name="b">The other index.</param>
    /// <returns>The index to use, -1 when both are missing.</returns>
    private static int MinPositive(int a, int b) => a >= 0 && b >= 0 ? Math.Min(a, b) : Math.Max(a, b);

    /// <summary>Takes a vector component away from zero when it is out of the range that direct prediction treats as still.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns><see langword="true"/> when both components are within one quarter sample of zero.</returns>
    private static bool IsStill(MotionVector vector) => Math.Abs(vector.X) <= ColZeroRange && Math.Abs(vector.Y) <= ColZeroRange;

    /// <summary>Gets the stored size of the motion vector difference of the block at a position.</summary>
    /// <param name="differences">The stored sizes of one component.</param>
    /// <param name="column">The block's column relative to the current macroblock.</param>
    /// <param name="row">The block's row relative to the current macroblock.</param>
    /// <returns>The size, 0 when the block is not available.</returns>
    private int MvdAt(byte[] differences, int column, int row)
    {
        var address = _predict0.MacroblockAt(column, row);
        return address < 0 ? 0 : differences[(address * H264Picture.BlocksPerMb) + (((row + Side) & Last) * Side) + ((column + Side) & Last)];
    }

    /// <summary>Works out the reference indices and vectors spatial direct prediction gives the whole macroblock (clause 8.4.1.2.2).</summary>
    /// <returns>The motion; blocks later zero the vectors where the co-located block is still.</returns>
    private H264DirectBlock SpatialMotion()
    {
        var around0 = _predict0.NeighbourRefs();
        var around1 = _predict1.NeighbourRefs();
        var ref0 = MinPositive(around0.A, MinPositive(around0.B, around0.C));
        var ref1 = MinPositive(around1.A, MinPositive(around1.B, around1.C));
        if (ref0 < 0 && ref1 < 0)
        {
            return new(0, 0, default, default);
        }

        var whole = new H264Partition(0, 0, Side, Side, H264MotionPrediction.Plain);
        var vector0 = ref0 >= 0 ? _predict0.Predict(whole, ref0) : default;
        var vector1 = ref1 >= 0 ? _predict1.Predict(whole, ref1) : default;
        return new(ref0, ref1, vector0, vector1);
    }

    /// <summary>Gives the motion of a block under spatial direct prediction.</summary>
    /// <param name="col">The co-located block.</param>
    /// <returns>The motion.</returns>
    private H264DirectBlock SpatialBlock(H264ColBlock col)
    {
        var still = !_lists.L1[0].IsLongTerm && !col.Intra && col.RefIdx == 0 && IsStill(col.Vector);
        var motion = _spatialMotion;
        var vector0 = motion.Ref0 < 0 || (motion.Ref0 == 0 && still) ? default : motion.Mv0;
        var vector1 = motion.Ref1 < 0 || (motion.Ref1 == 0 && still) ? default : motion.Mv1;
        return new(motion.Ref0, motion.Ref1, vector0, vector1);
    }

    /// <summary>Gives the motion of a block under temporal direct prediction (clause 8.4.1.2.3).</summary>
    /// <param name="col">The co-located block.</param>
    /// <returns>The motion.</returns>
    private H264DirectBlock TemporalBlock(H264ColBlock col)
    {
        var ref0 = col.Intra ? 0 : MapToList0(col.RefUid);
        var vector = col.Intra ? default : col.Vector;
        var first = _lists.L0[ref0];
        var second = _lists.L1[0];
        if (first.IsLongTerm || second.Picture.Poc == first.Picture.Poc)
        {
            return new(ref0, 0, vector, default);
        }

        var factor = H264InterPredictor.DistScaleFactor(_picture.Poc, first.Picture.Poc, second.Picture.Poc);
        var scaled = new MotionVector(((factor * vector.X) + ScaleRound) >> ScaleShift, ((factor * vector.Y) + ScaleRound) >> ScaleShift);
        return new(ref0, 0, scaled, new(scaled.X - vector.X, scaled.Y - vector.Y));
    }

    /// <summary>Finds the lowest index in list 0 of the frame a co-located block referred to.</summary>
    /// <param name="uid">The frame's identity.</param>
    /// <returns>The index; 0 when the frame is not in the list.</returns>
    private int MapToList0(int uid)
    {
        for (var index = 0; index < _lists.L0.Length; index++)
        {
            if (_lists.L0[index].Picture.Uid == uid)
            {
                return index;
            }
        }

        return 0;
    }

    /// <summary>Looks up the motion of the co-located block in the first picture of list 1.</summary>
    /// <param name="column">The block's column in the macroblock.</param>
    /// <param name="row">The block's row in the macroblock.</param>
    /// <returns>The co-located block's motion.</returns>
    private H264ColBlock Colocated(int column, int row)
    {
        var col = _lists.L1[0].Picture;
        var address = _mb.Address;
        var group = (address * H264Picture.GroupsPerMb) + ((row >> 1) * Half) + (column >> 1);
        var block = (address * H264Picture.BlocksPerMb) + (row * Side) + column;
        if (col.Kinds[address] != H264MbKind.Inter)
        {
            return new(true, -1, -1, default);
        }

        if (col.Field0.RefIdx[group] >= 0)
        {
            return new(false, col.Field0.RefIdx[group], col.Field0.RefUid[group], new(col.Field0.MvX[block], col.Field0.MvY[block]));
        }

        return col.Field1.RefIdx[group] >= 0
            ? new(false, col.Field1.RefIdx[group], col.Field1.RefUid[group], new(col.Field1.MvX[block], col.Field1.MvY[block]))
            : new(true, -1, -1, default);
    }

    /// <summary>Records the vectors direct prediction gave a 4x4 block.</summary>
    /// <param name="column">The block's column in the macroblock.</param>
    /// <param name="row">The block's row in the macroblock.</param>
    /// <param name="block">The motion.</param>
    private void WriteDirect(int column, int row, H264DirectBlock block)
    {
        var index = (_mb.Address * H264Picture.BlocksPerMb) + (row * Side) + column;
        _picture.Field0.MvX[index] = (short)block.Mv0.X;
        _picture.Field0.MvY[index] = (short)block.Mv0.Y;
        _picture.Field1.MvX[index] = (short)block.Mv1.X;
        _picture.Field1.MvY[index] = (short)block.Mv1.Y;
    }

    /// <summary>Records the reference indices direct prediction gave an 8x8 group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="block">The motion of the group's last block, which has the group's references.</param>
    private void WriteGroupReferences(int group, H264DirectBlock block)
    {
        var index = (_mb.Address * H264Picture.GroupsPerMb) + group;
        _picture.Field0.RefIdx[index] = (sbyte)block.Ref0;
        _picture.Field0.RefUid[index] = block.Ref0 >= 0 ? _lists.L0[block.Ref0].Picture.Uid : H264Picture.NoReference;
        _picture.Field1.RefIdx[index] = (sbyte)block.Ref1;
        _picture.Field1.RefUid[index] = block.Ref1 >= 0 ? _lists.L1[block.Ref1].Picture.Uid : H264Picture.NoReference;
    }
}
