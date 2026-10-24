// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>Works out the picture order count of frames (clause 8.2.1 of ITU-T H.264), keeping what it needs from the picture before. Frames only.</summary>
[DebuggerDisplay("H264PocState: msb {_prevMsb}, frame offset {_prevFrameNumOffset}")]
internal sealed class H264PocState
{
    /// <summary>The picture order count type that sends the least significant bits.</summary>
    private const int TypeLsb = 0;

    /// <summary>The picture order count type that follows a cycle of offsets.</summary>
    private const int TypeCycle = 1;

    /// <summary>The prediction of the picture order count of a picture that is not a reference, relative to the frame number, for type 2.</summary>
    private const int NonReferenceAdjust = 1;

    /// <summary>The most significant part of the picture order count of the reference picture before.</summary>
    private int _prevMsb;

    /// <summary>The least significant part of the picture order count of the reference picture before.</summary>
    private int _prevLsb;

    /// <summary>The frame number offset of the picture before.</summary>
    private int _prevFrameNumOffset;

    /// <summary>The frame number of the picture before.</summary>
    private int _prevFrameNum;

    /// <summary>Forgets what the pictures before gave.</summary>
    internal void Reset()
    {
        _prevMsb = 0;
        _prevLsb = 0;
        _prevFrameNumOffset = 0;
        _prevFrameNum = 0;
    }

    /// <summary>Works out the picture order count of a frame and remembers what the next picture needs.</summary>
    /// <param name="header">The header of the frame's first slice.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The frame's picture order count: the smaller of its top and bottom field counts.</returns>
    internal int Next(H264SliceHeader header, H264Sps sps)
    {
        var settings = sps.Poc;
        var picture = header.Picture;
        var maxFrameNum = 1 << sps.Log2MaxFrameNum;
        int top;
        int bottom;
        if (settings.Type == TypeLsb)
        {
            top = LsbCount(header, settings, out var msb);
            bottom = top + picture.DeltaPicOrderCntBottom;
            if (header.NalRefIdc != 0)
            {
                _prevMsb = msb;
                _prevLsb = picture.PicOrderCntLsb;
            }
        }
        else
        {
            var offset = FrameNumOffset(header, maxFrameNum);
            var counts = settings.Type == TypeCycle ? CycleCount(header, settings, offset) : DirectCount(header, offset);
            top = counts.Top;
            bottom = counts.Bottom;
            _prevFrameNumOffset = offset;
        }

        _prevFrameNum = picture.FrameNum;
        return Math.Min(top, bottom);
    }

    /// <summary>Works out the field counts for type 1 (8.2.1.2).</summary>
    /// <param name="header">The slice header.</param>
    /// <param name="settings">The picture order count settings.</param>
    /// <param name="offset">The frame number offset.</param>
    /// <returns>The top and bottom field counts.</returns>
    private static H264FieldCounts CycleCount(H264SliceHeader header, H264PocSettings settings, int offset)
    {
        var cycle = settings.OffsetForRefFrame.Length;
        var absolute = cycle != 0 ? offset + header.Picture.FrameNum : 0;
        if (header.NalRefIdc == 0 && absolute > 0)
        {
            absolute--;
        }

        var expected = 0;
        if (absolute > 0)
        {
            var cycleCount = (absolute - 1) / cycle;
            var inCycle = (absolute - 1) % cycle;
            var perCycle = 0;
            foreach (var step in settings.OffsetForRefFrame)
            {
                perCycle += step;
            }

            expected = cycleCount * perCycle;
            for (var index = 0; index <= inCycle; index++)
            {
                expected += settings.OffsetForRefFrame[index];
            }
        }

        if (header.NalRefIdc == 0)
        {
            expected += settings.OffsetForNonRef;
        }

        var top = expected + header.Picture.DeltaPicOrderCnt0;
        return new(top, top + settings.OffsetForTopToBottom + header.Picture.DeltaPicOrderCnt1);
    }

    /// <summary>Works out the field counts for type 2 (8.2.1.3).</summary>
    /// <param name="header">The slice header.</param>
    /// <param name="offset">The frame number offset.</param>
    /// <returns>The top and bottom field counts, which are equal.</returns>
    private static H264FieldCounts DirectCount(H264SliceHeader header, int offset)
    {
        var count = 0;
        if (!header.IsIdr)
        {
            count = (offset + header.Picture.FrameNum) << 1;
            if (header.NalRefIdc == 0)
            {
                count -= NonReferenceAdjust;
            }
        }

        return new(count, count);
    }

    /// <summary>Works out the frame number offset for the types that count frames (8.2.1.2 and 8.2.1.3).</summary>
    /// <param name="header">The slice header.</param>
    /// <param name="maxFrameNum">The range of frame numbers.</param>
    /// <returns>The offset.</returns>
    private int FrameNumOffset(H264SliceHeader header, int maxFrameNum)
    {
        if (header.IsIdr)
        {
            return 0;
        }

        return _prevFrameNum > header.Picture.FrameNum ? _prevFrameNumOffset + maxFrameNum : _prevFrameNumOffset;
    }

    /// <summary>Works out the top field count for type 0 (8.2.1.1).</summary>
    /// <param name="header">The slice header.</param>
    /// <param name="settings">The picture order count settings.</param>
    /// <param name="msb">Receives the most significant part.</param>
    /// <returns>The top field count.</returns>
    private int LsbCount(H264SliceHeader header, H264PocSettings settings, out int msb)
    {
        if (header.IsIdr)
        {
            _prevMsb = 0;
            _prevLsb = 0;
        }

        var range = 1 << settings.Log2MaxLsb;
        var lsb = header.Picture.PicOrderCntLsb;
        msb = _prevMsb;
        var half = range >> 1;
        if (lsb < _prevLsb && _prevLsb - lsb >= half)
        {
            msb = _prevMsb + range;
        }
        else if (lsb > _prevLsb && lsb - _prevLsb > half)
        {
            msb = _prevMsb - range;
        }

        return msb + lsb;
    }
}
