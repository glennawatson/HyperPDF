// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs;
using HyperPdf.Codecs.H264;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>
/// Decodes H.264 pictures on the graphics hardware through VA-API. The headers, picture order counts, reference marking and output order
/// come from the same code the C# decoder uses; only the reconstruction of the samples is left to the hardware.
/// </summary>
[DebuggerDisplay("VaApiH264Decoder: {Width}x{Height}")]
internal sealed unsafe class VaApiH264Decoder : VideoDecoder
{
    /// <summary>The VA-API profile for the constrained baseline profile.</summary>
    private const int ConstrainedBaselineProfile = 13;

    /// <summary>The VA-API profile for the main profile.</summary>
    private const int MainProfile = 6;

    /// <summary>The VA-API profile for the high profile.</summary>
    private const int HighProfile = 7;

    /// <summary>The profile_idc of the baseline profile.</summary>
    private const int BaselineProfileIdc = 66;

    /// <summary>The profile_idc of the main profile.</summary>
    private const int MainProfileIdc = 77;

    /// <summary>The VA-API buffer type of picture parameters.</summary>
    private const int PictureParameterBuffer = 0;

    /// <summary>The VA-API buffer type of scaling lists.</summary>
    private const int ScalingListBuffer = 1;

    /// <summary>The VA-API buffer type of slice parameters.</summary>
    private const int SliceParameterBuffer = 4;

    /// <summary>The VA-API buffer type of slice data.</summary>
    private const int SliceDataBuffer = 5;

    /// <summary>The surfaces kept beyond the reference frames: the picture being decoded and one spare.</summary>
    private const int ExtraSurfaces = 2;

    /// <summary>The pixels in a crop unit of 4:2:0 video.</summary>
    private const int CropUnit = 2;

    /// <summary>The bits of the NAL unit header, which the slice data bit offset counts.</summary>
    private const int NalHeaderBits = 8;

    /// <summary>The level from which 8x8 bi-prediction blocks are not allowed to be smaller (A.3.3.2).</summary>
    private const int LargeBiPredLevel = 31;

    /// <summary>The value subtracted from the log2 of the largest frame number or order count to store it.</summary>
    private const int Log2Base = 4;

    /// <summary>The offset the initial quantiser is stored relative to.</summary>
    private const int QpBase = 26;

    /// <summary>The 4:2:0 value of the chroma format.</summary>
    private const int Chroma420 = 1;

    /// <summary>The number of 8x8 scaling lists VA-API takes.</summary>
    private const int Scaling8x8Lists = 2;

    /// <summary>The bytes of a 16 bit weight or offset.</summary>
    private const int Int16Bytes = 2;

    /// <summary>The Cb and Cr weights of one reference list entry.</summary>
    private const int ChromaComponents = 2;

    /// <summary>The mask of a field two bits wide.</summary>
    private const int TwoBitMask = 3;

    /// <summary>The parameter sets received so far.</summary>
    private readonly H264ParameterSets _sets;

    /// <summary>The bytes of the NAL length that start each NAL unit of a packet.</summary>
    private readonly int _lengthSize;

    /// <summary>The hardware decoding context.</summary>
    private readonly VaDecoderSession _session;

    /// <summary>The NAL units of the packet being decoded.</summary>
    private readonly List<NalRange> _nals = [];

    /// <summary>The slices of the picture being decoded.</summary>
    private readonly List<SliceRecord> _slices = [];

    /// <summary>The pictures that hold a surface.</summary>
    private readonly List<H264Picture> _live = [];

    /// <summary>The slice parameter buffers, reused from picture to picture.</summary>
    private readonly List<VaBlock> _sliceBlocks = [];

    /// <summary>The picture parameter buffer.</summary>
    private readonly VaBlock _pictureBlock = new(VaH264Layout.PictureSize);

    /// <summary>The scaling list buffer.</summary>
    private readonly VaBlock _matrixBlock = new(VaH264Layout.MatrixSize);

    /// <summary>The frames kept as references for inter prediction.</summary>
    private readonly H264Dpb _dpb = new();

    /// <summary>Works out the picture order count of each frame.</summary>
    private readonly H264PocState _poc = new();

    /// <summary>Holds decoded frames until they can be output in order.</summary>
    private readonly H264OutputQueue _queue = new();

    /// <summary>The columns cropped from the left of the coded picture.</summary>
    private readonly int _cropLeft;

    /// <summary>The rows cropped from the top of the coded picture.</summary>
    private readonly int _cropTop;

    /// <summary>The payload of the NAL unit being read.</summary>
    private byte[] _rbsp = new byte[1_024];

    /// <summary>The number of pictures started so far, which gives each its identity.</summary>
    private int _pictureCount;

    /// <summary>Initializes a new instance of the <see cref="VaApiH264Decoder"/> class.</summary>
    /// <param name="shape">The size and cropping of the pictures.</param>
    /// <param name="sets">The parameter sets.</param>
    /// <param name="lengthSize">The bytes of the length in front of each NAL unit.</param>
    /// <param name="session">The hardware decoding context.</param>
    private VaApiH264Decoder(PictureShape shape, H264ParameterSets sets, int lengthSize, VaDecoderSession session)
        : base(shape.Width, shape.Height)
    {
        _sets = sets;
        _lengthSize = lengthSize;
        _session = session;
        _cropLeft = shape.CropLeft;
        _cropTop = shape.CropTop;
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        var data = packet.Data;
        if (!NalUnits.ListLengthPrefixed(data, _lengthSize, _nals))
        {
            throw new InvalidDataException("An H.264 packet is not valid.");
        }

        _slices.Clear();
        foreach (var nal in _nals)
        {
            ReadNal(data.Slice(nal.Offset, nal.Length), nal);
        }

        if (_slices.Count > 0)
        {
            DecodePicture(data, packet.Time, output);
        }
    }

    /// <inheritdoc/>
    public override void Flush(List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _queue.Flush(output);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _queue.Clear();
        _poc.Reset();
        _dpb.Clear();
        ReleaseUnused();
    }

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The H.264 track.</param>
    /// <param name="failure">Receives why no decoder was made, when none was.</param>
    /// <returns>The decoder, or <see langword="null"/> when this system's graphics hardware cannot decode the track.</returns>
    internal static VaApiH264Decoder? TryCreate(MediaTrack track, out DecoderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(track);
        var sets = H264Decoder.ReadSets(track, out var lengthSize);
        var sps = sets?.FirstSps();
        if (sets is null || sps is null || !IsSupported(sps))
        {
            failure = new("The stream is not 8 bit 4:2:0 progressive H.264", 0);
            return null;
        }

        var shape = new PictureShape(
            (sps.WidthInMbs * H264Picture.LumaSize) - (CropUnit * (sps.Crop.Left + sps.Crop.Right)),
            (sps.HeightInMbs * H264Picture.LumaSize) - (CropUnit * (sps.Crop.Top + sps.Crop.Bottom)),
            CropUnit * sps.Crop.Left,
            CropUnit * sps.Crop.Top);
        var session = OpenSession(sps, out failure);
        return session is null ? null : new(shape, sets, lengthSize, session);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => _session.Dispose();

    /// <summary>Finds whether the hardware path reads a stream: 8 bit 4:2:0 frames, which the C# decoder also reads.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns><see langword="true"/> when the stream is supported.</returns>
    private static bool IsSupported(H264Sps sps) =>
        sps.FrameMbsOnly && sps.ChromaFormat == Chroma420 && sps.BitDepthLuma == H264Layout.SampleBits && sps.BitDepthChroma == H264Layout.SampleBits
        && !sps.SeparateColourPlanes && !sps.TransformBypass;

    /// <summary>Opens a session with the profile that suits the stream, falling back to the high profile, which decodes the others too.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="failure">Receives why no session was opened, when none was.</param>
    /// <returns>The session, or <see langword="null"/>.</returns>
    private static VaDecoderSession? OpenSession(H264Sps sps, out DecoderFailure failure)
    {
        var width = sps.WidthInMbs * H264Picture.LumaSize;
        var height = sps.HeightInMbs * H264Picture.LumaSize;
        var surfaces = Math.Min(Math.Max(sps.MaxRefFrames, 1), VaH264Layout.ReferenceFrameCount) + ExtraSurfaces + 1;
        var preferred = sps.ProfileIdc switch
        {
            BaselineProfileIdc => ConstrainedBaselineProfile,
            MainProfileIdc => MainProfile,
            _ => HighProfile,
        };
        var session = VaDecoderSession.TryOpen(preferred, width, height, surfaces, out failure);
        return session ?? (preferred == HighProfile ? null : VaDecoderSession.TryOpen(HighProfile, width, height, surfaces, out failure));
    }

    /// <summary>Describes a reference frame to VA-API.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The picture entry.</returns>
    private static PictureEntry Reference(H264RefPicture frame) => new(
        frame.Picture.Surface,
        (uint)(frame.IsLongTerm ? frame.LongTermFrameIdx : frame.FrameNum),
        frame.IsLongTerm ? PictureEntry.LongTerm : PictureEntry.ShortTerm,
        frame.Picture.Poc,
        frame.Picture.Poc);

    /// <summary>Writes a run of picture entries, filling the places beyond the pictures with invalid ones.</summary>
    /// <param name="block">The buffer.</param>
    /// <param name="offset">Where the run starts.</param>
    /// <param name="count">The number of places.</param>
    /// <param name="pictures">The pictures.</param>
    private static void WritePictures(VaBlock block, int offset, int count, IReadOnlyList<H264RefPicture?> pictures)
    {
        for (var i = 0; i < count; i++)
        {
            var frame = i < pictures.Count ? pictures[i] : null;
            block.Set(offset + (i * PictureEntry.Size), frame is null ? PictureEntry.None : Reference(frame));
        }
    }

    /// <summary>Packs the sequence fields of the picture parameters.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The word.</returns>
    private static uint SequenceFields(H264Sps sps)
    {
        var poc = sps.Poc;
        return (uint)(
            (sps.ChromaFormat & TwoBitMask)
            | (Bit(sps.GapsAllowed) << 3)
            | (Bit(sps.FrameMbsOnly) << 4)
            | (Bit(sps.Mbaff) << 5)
            | (Bit(sps.Direct8x8Inference) << 6)
            | (Bit(sps.LevelIdc >= LargeBiPredLevel) << 7)
            | ((sps.Log2MaxFrameNum - Log2Base) << 8)
            | ((poc.Type & TwoBitMask) << 12)
            | ((Math.Max(poc.Log2MaxLsb - Log2Base, 0) & 0xF) << 14)
            | (Bit(poc.DeltaAlwaysZero) << 18));
    }

    /// <summary>Packs the picture fields of the picture parameters.</summary>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="header">The header of the picture's first slice.</param>
    /// <returns>The word.</returns>
    private static uint PictureFields(H264Pps pps, H264SliceHeader header) => (uint)(
        Bit(pps.Cabac)
        | (Bit(pps.WeightedPred) << 1)
        | ((pps.WeightedBipredIdc & TwoBitMask) << 2)
        | (Bit(pps.Transform8x8) << 4)
        | (Bit(pps.ConstrainedIntraPred) << 6)
        | (Bit(pps.BottomFieldPicOrderPresent) << 7)
        | (Bit(pps.DeblockingControlPresent) << 8)
        | (Bit(pps.RedundantPicCntPresent) << 9)
        | (Bit(header.NalRefIdc != 0) << 10));

    /// <summary>Turns a flag into a bit.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>1 or 0.</returns>
    private static int Bit(bool flag) => flag ? 1 : 0;

    /// <summary>Writes the scaling lists, which are in zigzag order, in raster order.</summary>
    /// <param name="block">The scaling list buffer.</param>
    /// <param name="scaling">The lists.</param>
    private static void WriteScaling(VaBlock block, H264ScalingLists scaling)
    {
        var zigzag4 = H264Tables.ZigZag4x4;
        var zigzag8 = H264Tables.ZigZag8x8;
        for (var list = 0; list < H264ScalingLists.Count4x4; list++)
        {
            for (var k = 0; k < H264ScalingLists.Size4x4; k++)
            {
                block.Set(VaH264Layout.Lists4x4 + (list * H264ScalingLists.Size4x4) + zigzag4[k], scaling.Lists4x4[list][k]);
            }
        }

        for (var list = 0; list < Scaling8x8Lists; list++)
        {
            for (var k = 0; k < H264ScalingLists.Size8x8; k++)
            {
                block.Set(VaH264Layout.Lists8x8 + (list * H264ScalingLists.Size8x8) + zigzag8[k], scaling.Lists8x8[list][k]);
            }
        }
    }

    /// <summary>Writes the prediction weights of a slice.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="table">The weights the slice sent.</param>
    private static void WriteWeights(VaBlock block, H264WeightTable table)
    {
        block.Set(VaH264Layout.LumaLog2WeightDenom, (byte)table.LumaLog2Denom);
        block.Set(VaH264Layout.ChromaLog2WeightDenom, (byte)table.ChromaLog2Denom);
        WriteWeightGroup(block, VaH264Layout.WeightsL0, table.L0);
        WriteWeightGroup(block, VaH264Layout.WeightsL1, table.L1);
    }

    /// <summary>Writes the weights of one reference list.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="group">Where the group of fields starts.</param>
    /// <param name="weights">The weights of each entry of the list.</param>
    private static void WriteWeightGroup(VaBlock block, int group, H264Weight[] weights)
    {
        block.Set(group + VaH264Layout.LumaFlag, (byte)Bit(Array.Exists(weights, static weight => weight.HasLuma)));
        block.Set(group + VaH264Layout.ChromaFlag, (byte)Bit(Array.Exists(weights, static weight => weight.HasChroma)));
        for (var i = 0; i < Math.Min(weights.Length, VaH264Layout.ReferenceListCount); i++)
        {
            var weight = weights[i];
            block.Set(group + VaH264Layout.LumaWeights + (i * Int16Bytes), (short)weight.LumaWeight);
            block.Set(group + VaH264Layout.LumaOffsets + (i * Int16Bytes), (short)weight.LumaOffset);
            var chroma = i * ChromaComponents * Int16Bytes;
            block.Set(group + VaH264Layout.ChromaWeights + chroma, (short)weight.CbWeight);
            block.Set(group + VaH264Layout.ChromaWeights + chroma + Int16Bytes, (short)weight.CrWeight);
            block.Set(group + VaH264Layout.ChromaOffsets + chroma, (short)weight.CbOffset);
            block.Set(group + VaH264Layout.ChromaOffsets + chroma + Int16Bytes, (short)weight.CrOffset);
        }
    }

    /// <summary>Handles one NAL unit of a packet: parameter sets are kept, slices are read.</summary>
    /// <param name="nal">The NAL unit.</param>
    /// <param name="range">Where the NAL unit is in the packet.</param>
    private void ReadNal(ReadOnlySpan<byte> nal, NalRange range)
    {
        var type = H264Nal.TypeOf(nal[0]);
        if (type is H264Nal.Slice or H264Nal.IdrSlice)
        {
            ReadSlice(nal, range);
            return;
        }

        if (type is not (H264Nal.Sps or H264Nal.Pps))
        {
            return;
        }

        var payload = new byte[nal.Length];
        var length = NalUnits.Unescape(nal, payload, 1);
        if (type == H264Nal.Sps)
        {
            _sets.AddSps(payload.AsSpan(0, length));
        }
        else
        {
            _sets.AddPps(payload.AsSpan(0, length));
        }
    }

    /// <summary>Reads the header of a slice and keeps it for the picture.</summary>
    /// <param name="nal">The NAL unit.</param>
    /// <param name="range">Where the NAL unit is in the packet.</param>
    private void ReadSlice(ReadOnlySpan<byte> nal, NalRange range)
    {
        if (_rbsp.Length < nal.Length)
        {
            _rbsp = new byte[nal.Length];
        }

        var length = NalUnits.Unescape(nal, _rbsp, 1);
        var header = H264SliceHeaderReader.Read(_rbsp.AsSpan(0, length), nal[0], _sets, out var sps, out var pps, out var dataBit);
        _slices.Add(new(header, sps, pps, dataBit, range));
    }

    /// <summary>Decodes the slices gathered for a picture on the hardware and queues the result.</summary>
    /// <param name="data">The packet's bytes.</param>
    /// <param name="time">When the picture is shown, in seconds.</param>
    /// <param name="output">Receives the pictures that become ready.</param>
    private void DecodePicture(ReadOnlySpan<byte> data, double time, List<VideoFrame> output)
    {
        var first = _slices[0];
        _pictureCount++;
        var picture = new H264Picture(0, 0) { Uid = _pictureCount, Poc = _poc.Next(first.Header, first.Sps) };
        var surface = _session.Rent();
        picture.Surface = surface;
        _live.Add(picture);
        PrepareBuffers(picture, first);
        _session.Begin(surface);
        try
        {
            AddBuffers(data, first.Pps.Scaling);
        }
        finally
        {
            _session.End();
        }

        var frame = RentFrame(Width, Height, time);
        _session.Read(surface, frame, _cropLeft, _cropTop);
        var sps = first.Sps;
        _queue.Add(new(picture.Poc, frame), first.Header.IsIdr, sps.MaxNumReorderFrames >= 0 ? sps.MaxNumReorderFrames : sps.DpbFrames, output);
        _dpb.Mark(new(picture, first.Header.Picture.FrameNum), first.Header, sps);
        ReleaseUnused();
    }

    /// <summary>Fills the picture and slice parameter buffers for the picture being decoded.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="first">The first slice.</param>
    private void PrepareBuffers(H264Picture picture, in SliceRecord first)
    {
        WritePictureParameters(picture, first);
        while (_sliceBlocks.Count < _slices.Count)
        {
            _sliceBlocks.Add(new(VaH264Layout.SliceSize));
        }

        for (var i = 0; i < _slices.Count; i++)
        {
            WriteSliceParameters(_sliceBlocks[i], _slices[i], picture);
        }
    }

    /// <summary>Hands the hardware the parameters and data of the picture being decoded.</summary>
    /// <param name="data">The packet's bytes.</param>
    /// <param name="scaling">The scaling lists.</param>
    private void AddBuffers(ReadOnlySpan<byte> data, H264ScalingLists scaling)
    {
        _matrixBlock.Clear();
        WriteScaling(_matrixBlock, scaling);
        _session.Add(PictureParameterBuffer, _pictureBlock);
        _session.Add(ScalingListBuffer, _matrixBlock);
        fixed (byte* bytes = data)
        {
            for (var i = 0; i < _slices.Count; i++)
            {
                _session.Add(SliceParameterBuffer, _sliceBlocks[i]);
                var nal = _slices[i].Nal;
                _session.Add(SliceDataBuffer, bytes + nal.Offset, (uint)nal.Length);
            }
        }
    }

    /// <summary>Writes the picture parameters from the picture's first slice and the frames kept as references.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="first">The first slice.</param>
    private void WritePictureParameters(H264Picture picture, in SliceRecord first)
    {
        var (header, sps, pps, _, _) = first;
        var block = _pictureBlock;
        block.Clear();
        var flags = header.NalRefIdc != 0 ? PictureEntry.ShortTerm : 0;
        block.Set(VaH264Layout.CurrentPicture, new PictureEntry(picture.Surface, (uint)header.Picture.FrameNum, flags, picture.Poc, picture.Poc));
        WritePictures(block, VaH264Layout.ReferenceFrames, VaH264Layout.ReferenceFrameCount, _dpb.Frames);
        block.Set(VaH264Layout.WidthInMbsMinus1, (ushort)(sps.WidthInMbs - 1));
        block.Set(VaH264Layout.HeightInMbsMinus1, (ushort)(sps.HeightInMbs - 1));
        block.Set(VaH264Layout.NumRefFrames, (byte)sps.MaxRefFrames);
        block.Set(VaH264Layout.SequenceFields, SequenceFields(sps));
        block.Set(VaH264Layout.PicInitQpMinus26, (sbyte)(pps.PicInitQp - QpBase));
        block.Set(VaH264Layout.ChromaQpIndexOffset, (sbyte)pps.ChromaQpOffset);
        block.Set(VaH264Layout.SecondChromaQpIndexOffset, (sbyte)pps.SecondChromaQpOffset);
        block.Set(VaH264Layout.PictureFields, PictureFields(pps, header));
        block.Set(VaH264Layout.FrameNum, (ushort)header.Picture.FrameNum);
    }

    /// <summary>Writes the parameters of one slice, with its reference lists.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="record">The slice.</param>
    /// <param name="picture">The picture the slice belongs to.</param>
    private void WriteSliceParameters(VaBlock block, in SliceRecord record, H264Picture picture)
    {
        var header = record.Header;
        block.Clear();
        block.Set(VaH264Layout.SliceDataSize, (uint)record.Nal.Length);
        block.Set(VaH264Layout.SliceDataBitOffset, (ushort)(record.DataBit + NalHeaderBits));
        block.Set(VaH264Layout.FirstMbInSlice, (ushort)header.FirstMb);
        block.Set(VaH264Layout.SliceType, (byte)header.SliceType);
        block.Set(VaH264Layout.DirectSpatialMvPred, (byte)Bit(header.Refs.DirectSpatialMvPred));
        block.Set(VaH264Layout.NumRefIdxL0ActiveMinus1, (byte)Math.Max(header.Refs.NumRefIdxL0 - 1, 0));
        block.Set(VaH264Layout.NumRefIdxL1ActiveMinus1, (byte)Math.Max(header.Refs.NumRefIdxL1 - 1, 0));
        block.Set(VaH264Layout.CabacInitIdc, (byte)header.Tail.CabacInitIdc);
        block.Set(VaH264Layout.SliceQpDelta, (sbyte)header.Tail.QpDelta);
        block.Set(VaH264Layout.DisableDeblockingFilterIdc, (byte)header.Tail.DisableDeblockingFilterIdc);
        block.Set(VaH264Layout.SliceAlphaC0OffsetDiv2, (sbyte)header.Tail.AlphaC0OffsetDiv2);
        block.Set(VaH264Layout.SliceBetaOffsetDiv2, (sbyte)header.Tail.BetaOffsetDiv2);
        WriteReferenceLists(block, header, record.Sps, picture);
        if (header.Refs.Weights is { } weights)
        {
            WriteWeights(block, weights);
        }
    }

    /// <summary>Builds the reference lists of a slice and writes them.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="header">The slice header.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="picture">The picture being decoded.</param>
    private void WriteReferenceLists(VaBlock block, H264SliceHeader header, H264Sps sps, H264Picture picture)
    {
        var lists = header.IsB ? _dpb.BuildListsB(header, sps, picture.Poc) : new H264RefLists(header.IsP ? _dpb.BuildList0(header, sps) : [], []);
        WritePictures(block, VaH264Layout.RefPicList0, VaH264Layout.ReferenceListCount, lists.L0);
        WritePictures(block, VaH264Layout.RefPicList1, VaH264Layout.ReferenceListCount, lists.L1);
    }

    /// <summary>Gives back the surface of every picture that is no longer a reference.</summary>
    private void ReleaseUnused()
    {
        for (var i = _live.Count - 1; i >= 0; i--)
        {
            if (_dpb.Holds(_live[i]))
            {
                continue;
            }

            _session.Return(_live[i].Surface);
            _live.RemoveAt(i);
        }
    }

    /// <summary>A slice read from the packet: its header, the parameter sets it uses, where its data starts and where it is in the packet.</summary>
    /// <param name="Header">The slice header.</param>
    /// <param name="Sps">The sequence parameter set.</param>
    /// <param name="Pps">The picture parameter set.</param>
    /// <param name="DataBit">The bit where the slice data starts, counted from the end of the NAL unit header.</param>
    /// <param name="Nal">Where the NAL unit is in the packet.</param>
    private readonly record struct SliceRecord(H264SliceHeader Header, H264Sps Sps, H264Pps Pps, long DataBit, NalRange Nal);

    /// <summary>The size of the pictures and how much of the coded picture is cropped away.</summary>
    /// <param name="Width">The shown width in pixels.</param>
    /// <param name="Height">The shown height in pixels.</param>
    /// <param name="CropLeft">The columns cropped from the left.</param>
    /// <param name="CropTop">The rows cropped from the top.</param>
    private readonly record struct PictureShape(int Width, int Height, int CropLeft, int CropTop);
}
