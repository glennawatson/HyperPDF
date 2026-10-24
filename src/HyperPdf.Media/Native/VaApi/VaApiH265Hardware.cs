// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Codecs;
using HyperPdf.Codecs.H265;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>Reconstructs H.265 pictures on the graphics hardware through VA-API, from the headers, reference lists and picture buffer the C# decoder has already worked out.</summary>
[DebuggerDisplay("VaApiH265Hardware")]
internal sealed unsafe class VaApiH265Hardware : IH265Hardware, IDisposable
{
    /// <summary>The VA-API profile for the main profile: 8 bit 4:2:0.</summary>
    private const int MainProfile = 17;

    /// <summary>The VA-API buffer type of picture parameters.</summary>
    private const int PictureParameterBuffer = 0;

    /// <summary>The VA-API buffer type of slice parameters.</summary>
    private const int SliceParameterBuffer = 4;

    /// <summary>The VA-API buffer type of slice data.</summary>
    private const int SliceDataBuffer = 5;

    /// <summary>The surfaces kept beyond the pictures the stream may keep: the one being decoded and stand-ins for missing references.</summary>
    private const int ExtraSurfaces = 3;

    /// <summary>The flag of an entry that holds no picture.</summary>
    private const uint InvalidFlag = 0x1;

    /// <summary>The initial quantiser is stored relative to this.</summary>
    private const int InitQpBase = 26;

    /// <summary>The parallel merge level is stored relative to this.</summary>
    private const int MergeLevelBase = 2;

    /// <summary>The hardware decoding context.</summary>
    private readonly VaDecoderSession _session;

    /// <summary>The pictures that may be referred to, in the order of the decoded picture buffer field.</summary>
    private readonly List<H265Picture> _references = [];

    /// <summary>The slice parameter buffers, reused from picture to picture.</summary>
    private readonly List<VaBlock> _sliceBlocks = [];

    /// <summary>The picture parameter buffer.</summary>
    private readonly VaBlock _pictureBlock = new(VaH265Layout.PictureSize);

    /// <summary>The layout the tile boundaries were last taken from.</summary>
    private H265PictureLayout? _layout;

    /// <summary>The picture parameter set the layout was made from.</summary>
    private H265Pps? _layoutPps;

    /// <summary>Initializes a new instance of the <see cref="VaApiH265Hardware"/> class.</summary>
    /// <param name="session">The hardware decoding context.</param>
    private VaApiH265Hardware(VaDecoderSession session) => _session = session;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint RentSurface() => _session.Rent();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnSurface(uint surface) => _session.Return(surface);

    /// <inheritdoc/>
    public void Decode(H265HardwarePicture picture, VideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(picture);
        ArgumentNullException.ThrowIfNull(frame);
        CollectReferences(picture);
        WritePictureParameters(picture);
        while (_sliceBlocks.Count < picture.Slices.Count)
        {
            _sliceBlocks.Add(new(VaH265Layout.SliceSize));
        }

        for (var i = 0; i < picture.Slices.Count; i++)
        {
            WriteSliceParameters(_sliceBlocks[i], picture, i);
        }

        Submit(picture);
        var shown = H265FrameConverter.ShownSize(picture.Sps);
        _session.Read(picture.Picture.Surface, frame, shown.X, shown.Y);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _session.Dispose();

    /// <summary>Opens a hardware context for a stream.</summary>
    /// <param name="sps">The first sequence parameter set.</param>
    /// <param name="failure">Receives why no context was made, when none was.</param>
    /// <returns>The hardware, or <see langword="null"/> when this system cannot decode the stream.</returns>
    internal static VaApiH265Hardware? TryOpen(H265Sps sps, out DecoderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(sps);
        if (!VaH265Writer.IsSupported(sps))
        {
            failure = new("The stream is not 8 bit 4:2:0 without scaling lists", 0);
            return null;
        }

        var surfaces = sps.Ordering.MaxDecPicBuffering + ExtraSurfaces;
        var session = VaDecoderSession.TryOpen(MainProfile, sps.Width, sps.Height, surfaces, out failure);
        return session is null ? null : new(session);
    }

    /// <summary>Picks the pictures the current one may refer to from the decoded picture buffer.</summary>
    /// <param name="picture">The picture being decoded.</param>
    /// <exception cref="InvalidDataException">The buffer holds more pictures than VA-API takes.</exception>
    private void CollectReferences(H265HardwarePicture picture)
    {
        _references.Clear();
        foreach (var candidate in picture.Buffer)
        {
            if (candidate.ReferenceState != H265Picture.Unused)
            {
                _references.Add(candidate);
            }
        }

        if (_references.Count > VaH265Layout.ReferenceFrameCount)
        {
            throw new InvalidDataException("The stream keeps more reference pictures than the graphics driver takes.");
        }
    }

    /// <summary>Writes the picture parameters.</summary>
    /// <param name="picture">The picture being decoded.</param>
    private void WritePictureParameters(H265HardwarePicture picture)
    {
        var block = _pictureBlock;
        block.Clear();
        block.SetHevcPicture(VaH265Layout.CurrentPicture, picture.Picture.Surface, picture.Picture.Poc, 0);
        for (var i = 0; i < VaH265Layout.ReferenceFrameCount; i++)
        {
            var offset = VaH265Layout.ReferenceFrames + (i * VaH265Layout.EntrySize);
            if (i < _references.Count)
            {
                var reference = _references[i];
                block.SetHevcPicture(offset, reference.Surface, reference.Poc, VaH265Writer.ReferenceFlags(reference, picture.References));
            }
            else
            {
                block.SetHevcPicture(offset, PictureEntry.NoSurface, 0, InvalidFlag);
            }
        }

        VaH265Writer.WriteSequenceParameters(block, picture.Sps);
        WritePictureLevelParameters(block, picture);
    }

    /// <summary>Writes the parameters that come from the picture parameter set and the first slice.</summary>
    /// <param name="block">The picture parameter buffer.</param>
    /// <param name="picture">The picture being decoded.</param>
    private void WritePictureLevelParameters(VaBlock block, H265HardwarePicture picture)
    {
        var sps = picture.Sps;
        var pps = picture.Pps;
        block.Set(VaH265Layout.PictureFields, VaH265Writer.PictureFields(sps, pps));
        block.Set(VaH265Layout.InitQpMinus26, (sbyte)(pps.Quant.InitQp - InitQpBase));
        block.Set(VaH265Layout.DiffCuQpDeltaDepth, (byte)pps.Quant.DiffCuQpDeltaDepth);
        block.Set(VaH265Layout.CbQpOffset, (sbyte)pps.Quant.CbQpOffset);
        block.Set(VaH265Layout.CrQpOffset, (sbyte)pps.Quant.CrQpOffset);
        block.Set(VaH265Layout.Log2ParallelMergeLevelMinus2, (byte)(pps.Log2ParMrgLevel - MergeLevelBase));
        WriteTiles(block, sps, pps);
        block.Set(VaH265Layout.SliceParsingFields, VaH265Writer.ParsingFields(picture));
        block.Set(VaH265Layout.NumRefIdxL0DefaultMinus1, (byte)Math.Max(pps.NumRefIdxL0Default - 1, 0));
        block.Set(VaH265Layout.NumRefIdxL1DefaultMinus1, (byte)Math.Max(pps.NumRefIdxL1Default - 1, 0));
        block.Set(VaH265Layout.PpsBetaOffsetDiv2, (sbyte)pps.Deblocking.BetaOffsetDiv2);
        block.Set(VaH265Layout.PpsTcOffsetDiv2, (sbyte)pps.Deblocking.TcOffsetDiv2);
        block.Set(VaH265Layout.NumExtraSliceHeaderBits, (byte)pps.ExtraSliceHeaderBits);
        block.Set(VaH265Layout.StRpsBits, (uint)picture.Slices[0].Header.ShortTerm.HeaderBits);
    }

    /// <summary>Writes the tile columns and rows, in coding tree blocks.</summary>
    /// <param name="block">The picture parameter buffer.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    private void WriteTiles(VaBlock block, H265Sps sps, H265Pps pps)
    {
        if (!pps.Tiles.Enabled)
        {
            return;
        }

        if (_layout is null || !ReferenceEquals(_layoutPps, pps))
        {
            _layout = new(sps, pps);
            _layoutPps = pps;
        }

        block.Set(VaH265Layout.TileColumnsMinus1, (byte)(pps.Tiles.Columns - 1));
        block.Set(VaH265Layout.TileRowsMinus1, (byte)(pps.Tiles.Rows - 1));
        VaH265Writer.WriteSizes(block, VaH265Layout.ColumnWidthMinus1, VaH265Layout.ColumnWidthCount, _layout.ColumnBoundaries);
        VaH265Writer.WriteSizes(block, VaH265Layout.RowHeightMinus1, VaH265Layout.RowHeightCount, _layout.RowBoundaries);
    }

    /// <summary>Writes the parameters of one slice segment.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="picture">The picture being decoded.</param>
    /// <param name="index">The index of the slice segment.</param>
    private void WriteSliceParameters(VaBlock block, H265HardwarePicture picture, int index)
    {
        var slice = picture.Slices[index];
        var header = slice.Header;
        var nal = picture.Bytes.Span.Slice(slice.Offset, slice.Length);
        var dataStart = H265Nal.HeaderBytes + header.Segment.DataOffset;
        block.Clear();
        block.Set(VaH265Layout.SliceDataSize, (uint)slice.Length);
        block.Set(VaH265Layout.SliceDataByteOffset, (uint)dataStart);
        block.Set(VaH265Layout.SliceSegmentAddress, (uint)header.Segment.Address);
        WriteReferenceList(block, VaH265Layout.RefPicList, slice.List0);
        WriteReferenceList(block, VaH265Layout.RefPicList + VaH265Layout.ReferenceListCount, slice.List1);
        block.Set(VaH265Layout.LongSliceFlags, VaH265Writer.SliceFlags(header, index == picture.Slices.Count - 1));
        VaH265Writer.WriteSliceSettings(block, header, picture.Pps);
        if (header.Weights is { } weights)
        {
            VaH265Writer.WriteWeights(block, weights);
        }

        block.Set(VaH265Layout.NumEntryPointOffsets, (ushort)header.Segment.EntryPoints.Length);
        block.Set(VaH265Layout.SliceDataNumEmuPrevnBytes, (ushort)VaH265Writer.CountPreventionBytes(nal, dataStart));
    }

    /// <summary>Writes a reference picture list as indexes into the decoded picture buffer field.</summary>
    /// <param name="block">The slice parameter buffer.</param>
    /// <param name="offset">Where the list is.</param>
    /// <param name="list">The pictures of the list.</param>
    private void WriteReferenceList(VaBlock block, int offset, H265Picture[] list)
    {
        for (var i = 0; i < VaH265Layout.ReferenceListCount; i++)
        {
            var index = i < list.Length ? _references.IndexOf(list[i]) : -1;
            block.Set(offset + i, index >= 0 ? (byte)index : VaH265Writer.NoReference);
        }
    }

    /// <summary>Hands the hardware the buffers of the picture and runs it.</summary>
    /// <param name="picture">The picture being decoded.</param>
    private void Submit(H265HardwarePicture picture)
    {
        _session.Begin(picture.Picture.Surface);
        try
        {
            _session.Add(PictureParameterBuffer, _pictureBlock);
            fixed (byte* bytes = picture.Bytes.Span)
            {
                for (var i = 0; i < picture.Slices.Count; i++)
                {
                    _session.Add(SliceParameterBuffer, _sliceBlocks[i]);
                    var slice = picture.Slices[i];
                    _session.Add(SliceDataBuffer, bytes + slice.Offset, (uint)slice.Length);
                }
            }
        }
        finally
        {
            _session.End();
        }
    }
}
