// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>Decodes H.264 pictures. This version reads intra coded pictures; inter prediction follows.</summary>
[DebuggerDisplay("H264Decoder: {Width}x{Height}")]
public sealed class H264Decoder : VideoDecoder
{
    /// <summary>The offset in the configuration of the byte that holds the NAL length size.</summary>
    private const int LengthSizeAt = 4;

    /// <summary>The mask of the NAL length size minus 1.</summary>
    private const int LengthSizeMask = 3;

    /// <summary>The offset in the configuration of the byte that holds the number of sequence parameter sets.</summary>
    private const int SpsCountAt = 5;

    /// <summary>The mask of the number of sequence parameter sets.</summary>
    private const int SpsCountMask = 0x1F;

    /// <summary>The first byte of the parameter sets in the configuration.</summary>
    private const int SetsAt = 6;

    /// <summary>The bytes of the length of a parameter set in the configuration.</summary>
    private const int SetLengthBytes = 2;

    /// <summary>The pixels in a crop unit of 4:2:0 video.</summary>
    private const int CropUnit = 2;

    /// <summary>The parameter sets received so far.</summary>
    private readonly H264ParameterSets _sets = new();

    /// <summary>The bytes of the NAL length that start each NAL unit of a packet.</summary>
    private readonly int _lengthSize;

    /// <summary>The NAL units of the packet being decoded.</summary>
    private readonly List<NalRange> _nals = [];

    /// <summary>The pictures made so far, which are decoded into again once no later picture can refer to them.</summary>
    private readonly List<H264Picture> _pool = [];

    /// <summary>The working memory every slice uses, kept so slices do not allocate it.</summary>
    private readonly H264SliceWork _work = new();

    /// <summary>The frames kept as references for inter prediction.</summary>
    private readonly H264Dpb _dpb = new();

    /// <summary>Works out the picture order count of each frame.</summary>
    private readonly H264PocState _poc = new();

    /// <summary>Holds decoded frames until they can be output in order.</summary>
    private readonly H264OutputQueue _queue = new();

    /// <summary>The payload of the NAL unit being decoded.</summary>
    private byte[] _rbsp = new byte[1_024];

    /// <summary>The picture being decoded, or <see langword="null"/> between pictures.</summary>
    private H264Picture? _picture;

    /// <summary>The header of the first slice of the picture being decoded.</summary>
    private H264SliceHeader? _firstHeader;

    /// <summary>The level scale factors of the picture parameter set last used.</summary>
    private H264LevelScale? _scale;

    /// <summary>The scaling lists the cached level scale factors were made from.</summary>
    private H264ScalingLists? _scaleSource;

    /// <summary>The number of slices decoded in the picture.</summary>
    private int _slices;

    /// <summary>The number of pictures started so far, which gives each its identity.</summary>
    private int _pictureCount;

    /// <summary>Initializes a new instance of the <see cref="H264Decoder"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="lengthSize">The bytes of the length in front of each NAL unit.</param>
    private H264Decoder(int width, int height, int lengthSize)
        : base(width, height) => _lengthSize = lengthSize;

    /// <summary>Makes a decoder for an H.264 track.</summary>
    /// <param name="track">The track, whose configuration is the AVC decoder configuration record.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="NotSupportedException">The stream uses a feature this decoder does not read.</exception>
    public static H264Decoder Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var data = track.Configuration.Span;
        if (data.Length < SetsAt)
        {
            throw new NotSupportedException("The H.264 track has no configuration record.");
        }

        var sets = new H264ParameterSets();
        try
        {
            AddConfiguredSets(data, sets);
        }
        catch (InvalidDataException exception)
        {
            throw new NotSupportedException("The H.264 configuration is damaged.", exception);
        }

        var sps = sets.FirstSps() ?? throw new NotSupportedException("The H.264 configuration has no sequence parameter set.");
        var width = (sps.WidthInMbs * H264Picture.LumaSize) - (CropUnit * (sps.Crop.Left + sps.Crop.Right));
        var height = (sps.HeightInMbs * H264Picture.LumaSize) - (CropUnit * (sps.Crop.Top + sps.Crop.Bottom));
        if (!sps.FrameMbsOnly)
        {
            throw new NotSupportedException("Interlaced H.264 is not supported yet.");
        }

        var decoder = new H264Decoder(width, height, (data[LengthSizeAt] & LengthSizeMask) + 1);
        AddConfiguredSets(data, decoder._sets);
        return decoder;
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

        _picture = null;
        _slices = 0;
        H264Sps? sps = null;
        foreach (var nal in _nals)
        {
            sps = HandleNal(data.Slice(nal.Offset, nal.Length)) ?? sps;
        }

        if (_picture is not null && sps is not null && _firstHeader is not null)
        {
            H264Deblocking.Apply(_picture);
            var limit = sps.MaxNumReorderFrames >= 0 ? sps.MaxNumReorderFrames : sps.DpbFrames;
            _queue.Add(new(_picture.Poc, MakeFrame(_picture, sps, packet.Time)), _firstHeader.IsIdr, limit, output);
            _dpb.Mark(new(_picture, _firstHeader.Picture.FrameNum), _firstHeader, sps);
        }

        _picture = null;
        _firstHeader = null;
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
        _picture = null;
        _firstHeader = null;
        _dpb.Clear();
    }

    /// <summary>Finds how many pictures a track's sequence lets come before another in decoding order and after it in output order.</summary>
    /// <param name="track">The track, whose configuration is the AVC decoder configuration record.</param>
    /// <returns>The number of pictures, or -1 when the configuration cannot be read.</returns>
    internal static int ReorderDepth(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var data = track.Configuration.Span;
        if (data.Length < SetsAt)
        {
            return -1;
        }

        var sets = new H264ParameterSets();
        try
        {
            AddConfiguredSets(data, sets);
        }
        catch (InvalidDataException)
        {
            return -1;
        }

        var sps = sets.FirstSps();
        if (sps is null)
        {
            return -1;
        }

        return sps.MaxNumReorderFrames >= 0 ? sps.MaxNumReorderFrames : sps.DpbFrames;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Nothing here needs to be let go.
    }

    /// <summary>Adds the parameter sets of a decoder configuration record.</summary>
    /// <param name="data">The record.</param>
    /// <param name="sets">Receives the sets.</param>
    /// <exception cref="InvalidDataException">A set is damaged or the record is cut short.</exception>
    private static void AddConfiguredSets(ReadOnlySpan<byte> data, H264ParameterSets sets)
    {
        var position = SetsAt;
        var spsCount = data[SpsCountAt] & SpsCountMask;
        for (var index = 0; index < spsCount; index++)
        {
            var set = NextSet(data, ref position);
            sets.AddSps(Unescape(set));
        }

        if (position >= data.Length)
        {
            return;
        }

        var ppsCount = data[position];
        position++;
        for (var index = 0; index < ppsCount; index++)
        {
            var set = NextSet(data, ref position);
            sets.AddPps(Unescape(set));
        }
    }

    /// <summary>Reads one length-prefixed parameter set from a configuration record.</summary>
    /// <param name="data">The record.</param>
    /// <param name="position">The position of the length, which moves past the set.</param>
    /// <returns>The set's NAL unit.</returns>
    /// <exception cref="InvalidDataException">The record is cut short.</exception>
    private static ReadOnlySpan<byte> NextSet(ReadOnlySpan<byte> data, ref int position)
    {
        if (position + SetLengthBytes > data.Length)
        {
            throw new InvalidDataException("The H.264 configuration is cut short.");
        }

        var length = (data[position] << 8) | data[position + 1];
        position += SetLengthBytes;
        if (position + length > data.Length)
        {
            throw new InvalidDataException("The H.264 configuration is cut short.");
        }

        var set = data.Slice(position, length);
        position += length;
        return set;
    }

    /// <summary>Gets the payload of a NAL unit without its header or emulation prevention bytes.</summary>
    /// <param name="nal">The NAL unit.</param>
    /// <returns>The payload.</returns>
    private static byte[] Unescape(ReadOnlySpan<byte> nal)
    {
        var buffer = new byte[nal.Length];
        var length = NalUnits.Unescape(nal, buffer, 1);
        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>Handles one NAL unit of a packet.</summary>
    /// <param name="nal">The NAL unit.</param>
    /// <returns>The sequence parameter set a slice used, or <see langword="null"/>.</returns>
    private H264Sps? HandleNal(ReadOnlySpan<byte> nal)
    {
        var type = H264Nal.TypeOf(nal[0]);
        if (type is H264Nal.Slice or H264Nal.IdrSlice)
        {
            return DecodeSlice(nal);
        }

        if (type == H264Nal.Sps)
        {
            _sets.AddSps(Unescape(nal));
        }
        else if (type == H264Nal.Pps)
        {
            _sets.AddPps(Unescape(nal));
        }

        return null;
    }

    /// <summary>Finds a picture to decode into: one no later picture can refer to, made like new, or a new one when every kept picture is still a reference.</summary>
    /// <param name="columns">The width in macroblocks.</param>
    /// <param name="rows">The height in macroblocks.</param>
    /// <returns>The picture.</returns>
    private H264Picture RentPicture(int columns, int rows)
    {
        for (var i = _pool.Count - 1; i >= 0; i--)
        {
            if (_pool[i].MbWidth != columns || _pool[i].MbHeight != rows)
            {
                _pool.RemoveAt(i);
            }
        }

        foreach (var candidate in _pool)
        {
            if (_dpb.Holds(candidate))
            {
                continue;
            }

            candidate.Recycle();
            return candidate;
        }

        var created = new H264Picture(columns, rows);
        _pool.Add(created);
        return created;
    }

    /// <summary>Decodes a slice NAL unit into the current picture.</summary>
    /// <param name="nal">The NAL unit.</param>
    /// <returns>The sequence parameter set the slice uses.</returns>
    /// <exception cref="NotSupportedException">The slice is not an intra slice.</exception>
    private H264Sps DecodeSlice(ReadOnlySpan<byte> nal)
    {
        if (_rbsp.Length < nal.Length)
        {
            _rbsp = new byte[nal.Length];
        }

        var length = NalUnits.Unescape(nal, _rbsp, 1);
        var rbsp = _rbsp.AsSpan(0, length);
        var header = H264SliceHeaderReader.Read(rbsp, nal[0], _sets, out var sps, out var pps, out var dataBit);
        if (_picture is null)
        {
            _pictureCount++;
            _picture = RentPicture(sps.WidthInMbs, sps.HeightInMbs);
            _picture.Uid = _pictureCount;
            _picture.ChromaQpOffsetCb = pps.ChromaQpOffset;
            _picture.ChromaQpOffsetCr = pps.SecondChromaQpOffset;
            _picture.Poc = _poc.Next(header, sps);
            _firstHeader = header;
        }

        _picture.SliceFilters.Add(new(header.Tail.DisableDeblockingFilterIdc, header.Tail.AlphaC0OffsetDiv2 << 1, header.Tail.BetaOffsetDiv2 << 1));

        if (_scale is null || !ReferenceEquals(_scaleSource, pps.Scaling))
        {
            _scale = new(pps.Scaling);
            _scaleSource = pps.Scaling;
        }

        _slices++;
        var startQp = pps.PicInitQp + header.Tail.QpDelta;
        if (header.IsP || header.IsB)
        {
            DecodeInterSlice(rbsp, dataBit, header, new(sps, pps));
        }
        else if (pps.Cabac)
        {
            new H264CabacIntraSliceDecoder(_picture, pps, _scale, _slices, startQp, _work).Decode(rbsp, dataBit, header.FirstMb);
        }
        else
        {
            new H264IntraSliceDecoder(_picture, pps, _scale, _slices, startQp, _work.Mb).Decode(rbsp, dataBit, header.FirstMb);
        }

        return sps;
    }

    /// <summary>Decodes a P or B slice into the current picture.</summary>
    /// <param name="rbsp">The payload of the slice's NAL unit.</param>
    /// <param name="dataBit">The bit index where the slice data starts.</param>
    /// <param name="header">The slice header.</param>
    /// <param name="sets">The parameter sets the slice uses.</param>
    /// <exception cref="NotSupportedException">The slice uses a feature this decoder does not read.</exception>
    private void DecodeInterSlice(ReadOnlySpan<byte> rbsp, long dataBit, H264SliceHeader header, H264SliceSets sets)
    {
        var picture = _picture!;
        if (header.IsB)
        {
            var lists = _dpb.BuildListsB(header, sets.Sps, picture.Poc);
            if (sets.Pps.Cabac)
            {
                new H264CabacBSliceDecoder(picture, sets, _scale!, _slices, header, lists, _work).Decode(rbsp, dataBit, header.FirstMb);
                return;
            }

            new H264BSliceDecoder(picture, sets, _scale!, _slices, header, lists, _work).Decode(rbsp, dataBit, header.FirstMb);
            return;
        }

        var list = _dpb.BuildList0(header, sets.Sps);
        if (sets.Pps.Cabac)
        {
            new H264CabacPSliceDecoder(picture, sets.Pps, _scale!, _slices, header, list, _work).Decode(rbsp, dataBit, header.FirstMb);
            return;
        }

        new H264PSliceDecoder(picture, sets.Pps, _scale!, _slices, header, list, _work).Decode(rbsp, dataBit, header.FirstMb);
    }

    /// <summary>Copies the visible part of a decoded picture into a frame.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="sps">The sequence parameter set, which gives the cropping.</param>
    /// <param name="time">When the picture is shown, in seconds.</param>
    /// <returns>The frame.</returns>
    private VideoFrame MakeFrame(H264Picture picture, H264Sps sps, double time)
    {
        var frame = RentFrame(Width, Height, time);
        var left = CropUnit * sps.Crop.Left;
        var top = CropUnit * sps.Crop.Top;
        for (var row = 0; row < Height; row++)
        {
            Array.Copy(picture.Luma, ((top + row) * picture.LumaStride) + left, frame.Y, row * Width, Width);
        }

        for (var row = 0; row < frame.ChromaHeight; row++)
        {
            var source = (((top >> 1) + row) * picture.ChromaStride) + (left >> 1);
            Array.Copy(picture.Cb, source, frame.U, row * frame.ChromaWidth, frame.ChromaWidth);
            Array.Copy(picture.Cr, source, frame.V, row * frame.ChromaWidth, frame.ChromaWidth);
        }

        return frame;
    }
}
