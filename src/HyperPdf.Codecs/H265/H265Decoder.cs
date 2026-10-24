// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Decodes H.265 (HEVC) pictures.</summary>
[DebuggerDisplay("H265Decoder: {Width}x{Height}")]
public sealed class H265Decoder : VideoDecoder
{
    /// <summary>The message for a configuration record that ends early.</summary>
    private const string CutShort = "The H.265 configuration is cut short.";

    /// <summary>The offset in the configuration of the byte that holds the NAL length size.</summary>
    private const int LengthSizeAt = 21;

    /// <summary>The mask of the NAL length size minus 1.</summary>
    private const int LengthSizeMask = 3;

    /// <summary>The offset in the configuration of the number of arrays of parameter sets.</summary>
    private const int ArrayCountAt = 22;

    /// <summary>The offset in the configuration of the first array.</summary>
    private const int ArraysAt = 23;

    /// <summary>The bytes that start an array: its type and the number of units in it.</summary>
    private const int ArrayHeaderBytes = 3;

    /// <summary>The mask of the NAL unit type in the first byte of an array.</summary>
    private const int ArrayTypeMask = 0x3F;

    /// <summary>The bytes of the length of a NAL unit in an array.</summary>
    private const int UnitLengthBytes = 2;

    /// <summary>The parameter sets received so far.</summary>
    private readonly H265ParameterSets _sets = new();

    /// <summary>The bytes of the NAL length that start each NAL unit of a packet.</summary>
    private readonly int _lengthSize;

    /// <summary>The NAL units of the packet being decoded.</summary>
    private readonly List<NalRange> _nals = [];

    /// <summary>The work areas of inter prediction, shared by every picture.</summary>
    private readonly H265PredictionBuffers _prediction = new();

    /// <summary>The pictures made so far, which are decoded into again once the buffer no longer holds them.</summary>
    private readonly List<H265Picture> _pool = [];

    /// <summary>The hardware that reconstructs the pictures, or <see langword="null"/> when they are reconstructed here.</summary>
    private readonly IH265Hardware? _hardware;

    /// <summary>The slice segments of the picture being decoded, for the hardware.</summary>
    private readonly List<H265HardwareSlice> _hardwareSlices = [];

    /// <summary>The NAL units of the slice segments of the picture being decoded, for the hardware.</summary>
    private readonly ArrayBufferWriter<byte> _hardwareBytes = new();

    /// <summary>The pictures that hold a hardware surface.</summary>
    private readonly List<H265Picture> _live = [];

    /// <summary>The decoded picture buffer.</summary>
    private readonly H265Dpb _dpb = new();

    /// <summary>Works out picture order counts.</summary>
    private readonly H265PocState _poc = new();

    /// <summary>The payload of the NAL unit being decoded.</summary>
    private byte[] _rbsp = new byte[1_024];

    /// <summary>The slice context of the picture being decoded, or <see langword="null"/> between pictures.</summary>
    private H265SliceContext? _context;

    /// <summary>The copy of a plane that sample adaptive offset reads, kept from picture to picture.</summary>
    private ushort[] _saoSource = [];

    /// <summary>The set of reference pictures of the picture being decoded.</summary>
    private H265RefPicSet _refSet = H265RefPicSet.Empty;

    /// <summary>The header of the last independent slice segment, which dependent segments continue.</summary>
    private H265SliceHeader? _lastIndependent;

    /// <summary>The layout in use, with the parameter sets it was made from.</summary>
    private H265PictureLayout? _layout;

    /// <summary>The picture parameter set the layout was made from.</summary>
    private H265Pps? _layoutPps;

    /// <summary>The sequence parameter set the layout was made from.</summary>
    private H265Sps? _layoutSps;

    /// <summary>Whether the next IRAP picture starts a new coded video sequence, because the stream just started or an end of sequence came.</summary>
    private bool _expectNewSequence = true;

    /// <summary>Whether leading pictures that need pictures from before the last random access point are skipped.</summary>
    private bool _skipRasl;

    /// <summary>The time of the packet being decoded.</summary>
    private double _time;

    /// <summary>Initializes a new instance of the <see cref="H265Decoder"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="lengthSize">The bytes of the length in front of each NAL unit.</param>
    /// <param name="hardware">The hardware that reconstructs the pictures, or <see langword="null"/> to reconstruct them here.</param>
    private H265Decoder(int width, int height, int lengthSize, IH265Hardware? hardware)
        : base(width, height)
    {
        _lengthSize = lengthSize;
        _hardware = hardware;
    }

    /// <summary>Makes a decoder for an H.265 track.</summary>
    /// <param name="track">The track, whose configuration is the HEVC decoder configuration record.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="NotSupportedException">The stream uses a feature this decoder does not read.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static H265Decoder Create(MediaTrack track) => Make(track, null);

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        var data = packet.Data;
        if (!NalUnits.ListLengthPrefixed(data, _lengthSize, _nals))
        {
            throw new InvalidDataException("An H.265 packet is not valid.");
        }

        _time = packet.Time;
        foreach (var nal in _nals)
        {
            HandleNal(data.Slice(nal.Offset, nal.Length), output);
        }

        FinishPicture(output);
    }

    /// <inheritdoc/>
    public override void Flush(List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        FinishPicture(output);
        BumpAll(output);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _context = null;
        _lastIndependent = null;
        _hardwareSlices.Clear();
        _hardwareBytes.ResetWrittenCount();
        _dpb.Clear();
        _poc.Reset();
        _expectNewSequence = true;
        _skipRasl = false;
    }

    /// <summary>Makes a decoder for an H.265 track that leaves the reconstruction of pictures to hardware.</summary>
    /// <param name="track">The track, whose configuration is the HEVC decoder configuration record.</param>
    /// <param name="hardware">The hardware.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="NotSupportedException">The stream uses a feature this decoder does not read.</exception>
    internal static H265Decoder CreateForHardware(MediaTrack track, IH265Hardware hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        return Make(track, hardware);
    }

    /// <summary>Reads the first sequence parameter set of a track's configuration record.</summary>
    /// <param name="track">The track, whose configuration is the HEVC decoder configuration record.</param>
    /// <returns>The set, or <see langword="null"/> when the configuration cannot be read.</returns>
    internal static H265Sps? ReadFirstSps(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var data = track.Configuration.Span;
        if (data.Length < ArraysAt)
        {
            return null;
        }

        var sets = new H265ParameterSets();
        try
        {
            AddConfiguredSets(data, sets);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        return sets.FirstSps();
    }

    /// <summary>Finds how many pictures a track's sequence lets come before another in decoding order and after it in output order.</summary>
    /// <param name="track">The track, whose configuration is the HEVC decoder configuration record.</param>
    /// <returns>The number of pictures, or -1 when the configuration cannot be read.</returns>
    internal static int ReorderDepth(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var data = track.Configuration.Span;
        if (data.Length < ArraysAt)
        {
            return -1;
        }

        var sets = new H265ParameterSets();
        try
        {
            AddConfiguredSets(data, sets);
        }
        catch (InvalidDataException)
        {
            return -1;
        }

        return sets.FirstSps()?.Ordering.MaxNumReorder ?? -1;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }

    /// <summary>Makes a decoder for an H.265 track.</summary>
    /// <param name="track">The track, whose configuration is the HEVC decoder configuration record.</param>
    /// <param name="hardware">The hardware that reconstructs the pictures, or <see langword="null"/>.</param>
    /// <returns>The decoder.</returns>
    /// <exception cref="NotSupportedException">The stream uses a feature this decoder does not read.</exception>
    private static H265Decoder Make(MediaTrack track, IH265Hardware? hardware)
    {
        ArgumentNullException.ThrowIfNull(track);
        var data = track.Configuration.Span;
        if (data.Length < ArraysAt)
        {
            throw new NotSupportedException("The H.265 track has no configuration record.");
        }

        var sets = new H265ParameterSets();
        try
        {
            AddConfiguredSets(data, sets);
        }
        catch (InvalidDataException exception)
        {
            throw new NotSupportedException("The H.265 configuration is damaged.", exception);
        }

        var sps = sets.FirstSps() ?? throw new NotSupportedException("The H.265 configuration has no sequence parameter set.");
        H265Support.Check(sps);
        var shown = H265FrameConverter.ShownSize(sps);
        var decoder = new H265Decoder(shown.Width, shown.Height, (data[LengthSizeAt] & LengthSizeMask) + 1, hardware);
        AddConfiguredSets(data, decoder._sets);
        return decoder;
    }

    /// <summary>Reads the parameter sets in a decoder configuration record.</summary>
    /// <param name="data">The record.</param>
    /// <param name="sets">Receives the sets.</param>
    /// <exception cref="InvalidDataException">The record is damaged.</exception>
    private static void AddConfiguredSets(ReadOnlySpan<byte> data, H265ParameterSets sets)
    {
        var position = ArraysAt;
        var arrays = data[ArrayCountAt];
        for (var i = 0; i < arrays; i++)
        {
            if (position + ArrayHeaderBytes > data.Length)
            {
                throw new InvalidDataException(CutShort);
            }

            var type = data[position] & ArrayTypeMask;
            var count = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 1)..]);
            position += ArrayHeaderBytes;
            for (var unit = 0; unit < count; unit++)
            {
                var length = ReadUnitLength(data, position);
                AddUnit(sets, type, data.Slice(position + UnitLengthBytes, length));
                position += UnitLengthBytes + length;
            }
        }
    }

    /// <summary>Reads the length of a NAL unit in a configuration array and checks that the unit fits.</summary>
    /// <param name="data">The record.</param>
    /// <param name="position">Where the length is.</param>
    /// <returns>The length.</returns>
    /// <exception cref="InvalidDataException">The unit does not fit.</exception>
    private static int ReadUnitLength(ReadOnlySpan<byte> data, int position)
    {
        if (position + UnitLengthBytes > data.Length)
        {
            throw new InvalidDataException(CutShort);
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
        if (position + UnitLengthBytes + length > data.Length)
        {
            throw new InvalidDataException(CutShort);
        }

        return length;
    }

    /// <summary>Adds a parameter set from a NAL unit.</summary>
    /// <param name="sets">Receives the set.</param>
    /// <param name="type">The NAL unit type.</param>
    /// <param name="nal">The NAL unit including its header.</param>
    private static void AddUnit(H265ParameterSets sets, int type, ReadOnlySpan<byte> nal)
    {
        if (type is not (H265Nal.Sps or H265Nal.Pps))
        {
            return;
        }

        var buffer = new byte[nal.Length];
        var length = NalUnits.Unescape(nal, buffer, H265Nal.HeaderBytes);
        if (type == H265Nal.Sps)
        {
            sets.Add(H265SpsReader.Read(buffer.AsSpan(0, length)));
        }
        else
        {
            sets.Add(H265PpsReader.Read(buffer.AsSpan(0, length)));
        }
    }

    /// <summary>Handles one NAL unit of a packet.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <param name="output">Receives frames that become ready.</param>
    private void HandleNal(ReadOnlySpan<byte> nal, List<VideoFrame> output)
    {
        if (nal.Length <= H265Nal.HeaderBytes || H265Nal.LayerOf(nal) != 0)
        {
            return;
        }

        var type = H265Nal.TypeOf(nal);
        if (type is H265Nal.Sps or H265Nal.Pps)
        {
            FinishPicture(output);
            AddUnit(_sets, type, nal);
        }
        else if (type is H265Nal.EndOfSequence or H265Nal.EndOfBitstream)
        {
            FinishPicture(output);
            _expectNewSequence = true;
        }
        else if (H265Nal.IsSlice(type))
        {
            HandleSlice(nal, type, output);
        }
    }

    /// <summary>Decodes one slice segment, starting a picture first when the segment begins one.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <param name="type">The NAL unit type.</param>
    /// <param name="output">Receives frames that become ready.</param>
    private void HandleSlice(ReadOnlySpan<byte> nal, int type, List<VideoFrame> output)
    {
        if (_rbsp.Length < nal.Length)
        {
            _rbsp = new byte[nal.Length];
        }

        var length = NalUnits.Unescape(nal, _rbsp, H265Nal.HeaderBytes);
        var rbsp = _rbsp.AsSpan(0, length);
        if (!rbsp.IsEmpty && (rbsp[0] & 0x80) != 0)
        {
            FinishPicture(output);
        }

        var header = H265SliceHeaderReader.Read(rbsp, type, _sets, _lastIndependent);
        if (header.Segment.First && !StartPicture(header, type, H265Nal.TemporalIdOf(nal), output))
        {
            return;
        }

        if (_context is null)
        {
            return;
        }

        if (!header.Segment.Dependent)
        {
            _lastIndependent = header;
        }

        _context.Header = header;
        H265RefListBuilder.Build(_context, _refSet);
        _context.SliceIndex = _context.Picture.SliceRefs.Count - 1;
        if (_hardware is null)
        {
            H265SliceDecoder.Decode(rbsp, _context);
            return;
        }

        KeepForHardware(nal, header, _context);
    }

    /// <summary>Keeps a slice segment, with the reference lists built for it, until the picture is complete and goes to the hardware.</summary>
    /// <param name="nal">The NAL unit including its header.</param>
    /// <param name="header">The slice segment header.</param>
    /// <param name="context">The context, whose reference lists belong to this segment.</param>
    private void KeepForHardware(ReadOnlySpan<byte> nal, H265SliceHeader header, H265SliceContext context)
    {
        var offset = _hardwareBytes.WrittenCount;
        _hardwareBytes.Write(nal);
        _hardwareSlices.Add(new(header, [.. context.RefList0], [.. context.RefList1], offset, nal.Length));
    }

    /// <summary>Gives the hardware the slices of the finished picture and keeps the frame it returns until the picture is output.</summary>
    /// <param name="context">The context of the picture.</param>
    private void DecodeOnHardware(H265SliceContext context)
    {
        var picture = context.Picture;
        var shown = H265FrameConverter.ShownSize(context.Sps);
        var frame = RentFrame(shown.Width, shown.Height, picture.Time);
        _hardware!.Decode(new(picture, context.Sps, context.Pps, _refSet, _dpb.Pictures, _hardwareSlices, _hardwareBytes.WrittenMemory), frame);
        picture.HardwareFrame = frame;
        _hardwareSlices.Clear();
        _hardwareBytes.ResetWrittenCount();
    }

    /// <summary>Gives back the surface of every picture that is no longer a reference, because its frame was read when it was decoded.</summary>
    private void ReleaseSurfaces()
    {
        for (var i = _live.Count - 1; i >= 0; i--)
        {
            if (_live[i].ReferenceState != H265Picture.Unused && _dpb.Pictures.Contains(_live[i]))
            {
                continue;
            }

            _hardware!.ReturnSurface(_live[i].Surface);
            _live.RemoveAt(i);
        }
    }

    /// <summary>Starts a picture: works out its order count and reference pictures, and releases pictures from the buffer.</summary>
    /// <param name="header">The header of the picture's first slice segment.</param>
    /// <param name="type">The NAL unit type.</param>
    /// <param name="temporalId">The temporal sub-layer.</param>
    /// <param name="output">Receives frames that become ready.</param>
    /// <returns><see langword="false"/> when the picture is skipped.</returns>
    /// <exception cref="InvalidDataException">The slice names parameter sets that were not sent.</exception>
    private bool StartPicture(H265SliceHeader header, int type, int temporalId, List<VideoFrame> output)
    {
        if (!_sets.TryFind(header.Segment.PpsId, out var pps, out var sps))
        {
            throw new InvalidDataException("The slice names a parameter set that was not sent.");
        }

        H265Support.Check(sps);
        H265Support.Check(pps);
        var irap = H265Nal.IsIrap(type);
        var noRaslOutput = irap && (H265Nal.IsIdr(type) || H265Nal.IsBla(type) || _expectNewSequence);
        if (irap)
        {
            _skipRasl = noRaslOutput;
            _expectNewSequence = false;
        }

        _context = null;
        if (H265Nal.IsRasl(type) && _skipRasl)
        {
            return false;
        }

        var poc = _poc.Compute(sps, header, type, temporalId, noRaslOutput);
        if (irap && noRaslOutput)
        {
            ReleasePrior(header, output);
        }

        _refSet = H265RpsDerivation.Derive(_dpb, sps, header, poc, H265Nal.IsIdr(type), missing => MakeMissing(sps, pps, missing));
        _dpb.RemoveUnused();
        MakeRoom(sps, output);
        BeginPicture(header, new(sps, pps), poc);
        return true;
    }

    /// <summary>Makes the picture about to be decoded and the context to decode it in.</summary>
    /// <param name="header">The header of the picture's first slice segment.</param>
    /// <param name="sets">The parameter sets the picture uses.</param>
    /// <param name="poc">The picture order count.</param>
    private void BeginPicture(H265SliceHeader header, H265ParameterPair sets, int poc)
    {
        var (sps, pps) = sets;
        var layout = LayoutFor(sps, pps);
        var picture = _hardware is null ? RentPicture(sps, layout) : NewHardwarePicture(sps);
        picture.Poc = poc;
        picture.Time = _time;
        picture.Sps = sps;
        picture.NeededForOutput = header.PicOutput;
        _context = MakeContext(sps, pps, layout, picture);
    }

    /// <summary>Makes the context for decoding a picture, giving it the work areas the decoder keeps.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="layout">The layout of the picture.</param>
    /// <param name="picture">The picture.</param>
    /// <returns>The context.</returns>
    private H265SliceContext MakeContext(H265Sps sps, H265Pps pps, H265PictureLayout layout, H265Picture picture)
    {
        if (_saoSource.Length < picture.Luma.Length)
        {
            _saoSource = new ushort[picture.Luma.Length];
        }

        return new(sps, pps, layout, picture, _prediction, _saoSource);
    }

    /// <summary>Finds a picture to decode into: one the buffer no longer holds, made like new, or a new one when every kept picture is still in the buffer.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="layout">The layout of the picture.</param>
    /// <returns>The picture.</returns>
    private H265Picture RentPicture(H265Sps sps, H265PictureLayout layout)
    {
        for (var i = _pool.Count - 1; i >= 0; i--)
        {
            if (!_pool[i].Fits(sps, layout))
            {
                _pool.RemoveAt(i);
            }
        }

        foreach (var candidate in _pool)
        {
            if (_dpb.Pictures.Contains(candidate))
            {
                continue;
            }

            candidate.Recycle();
            return candidate;
        }

        var created = new H265Picture(sps, layout);
        _pool.Add(created);
        return created;
    }

    /// <summary>Makes a picture whose samples live in a hardware surface, first giving back the surfaces nothing refers to any more.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The picture.</returns>
    private H265Picture NewHardwarePicture(H265Sps sps)
    {
        ReleaseSurfaces();
        return CreateHardwarePicture(sps);
    }

    /// <summary>Makes a picture whose samples live in a hardware surface.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The picture.</returns>
    private H265Picture CreateHardwarePicture(H265Sps sps)
    {
        var picture = new H265Picture(sps) { Surface = _hardware!.RentSurface() };
        _live.Add(picture);
        return picture;
    }

    /// <summary>Handles the pictures still in the buffer when a new coded video sequence starts.</summary>
    /// <param name="header">The header of the first slice segment of the new sequence.</param>
    /// <param name="output">Receives frames that become ready.</param>
    private void ReleasePrior(H265SliceHeader header, List<VideoFrame> output)
    {
        if (!header.Segment.NoOutputOfPriorPics)
        {
            BumpAll(output);
        }

        _dpb.Clear();
    }

    /// <summary>Makes room in the buffer by outputting pictures, as the bumping process requires before a picture is decoded (C.5.2.2).</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="output">Receives frames that become ready.</param>
    private void MakeRoom(H265Sps sps, List<VideoFrame> output)
    {
        var ordering = sps.Ordering;
        while (_dpb.CountWaiting() > 0
            && (_dpb.CountWaiting() > ordering.MaxNumReorder || _dpb.LatencyExceeded(ordering) || _dpb.Pictures.Count >= ordering.MaxDecPicBuffering))
        {
            BumpOne(output);
            _dpb.RemoveUnused();
        }
    }

    /// <summary>Outputs the picture that comes first in output order.</summary>
    /// <param name="output">Receives the frame.</param>
    private void BumpOne(List<VideoFrame> output)
    {
        var picture = _dpb.TakeNext();
        if (picture?.Sps is not { } sps)
        {
            return;
        }

        if (picture.HardwareFrame is { } frame)
        {
            picture.HardwareFrame = null;
            output.Add(frame);
            return;
        }

        var shown = H265FrameConverter.ShownSize(sps);
        output.Add(H265FrameConverter.Convert(picture, sps, RentFrame(shown.Width, shown.Height, picture.Time)));
    }

    /// <summary>Outputs every waiting picture in output order.</summary>
    /// <param name="output">Receives the frames.</param>
    private void BumpAll(List<VideoFrame> output)
    {
        while (_dpb.CountWaiting() > 0)
        {
            BumpOne(output);
        }
    }

    /// <summary>Finishes the picture being decoded: filters it, stores it and outputs pictures that are due.</summary>
    /// <param name="output">Receives frames that become ready.</param>
    private void FinishPicture(List<VideoFrame> output)
    {
        var context = _context;
        if (context is null)
        {
            return;
        }

        _context = null;
        if (_hardware is null)
        {
            H265LoopFilter.Apply(context);
        }
        else
        {
            DecodeOnHardware(context);
        }

        var picture = context.Picture;
        picture.ReferenceState = 1;
        picture.LatencyCount = 0;
        if (picture.NeededForOutput)
        {
            _dpb.AgeFollowing(picture.Poc);
        }

        _dpb.Pictures.Add(picture);
        var ordering = context.Sps.Ordering;
        while (_dpb.CountWaiting() > ordering.MaxNumReorder || _dpb.LatencyExceeded(ordering))
        {
            BumpOne(output);
        }
    }

    /// <summary>Gets the picture layout for a pair of parameter sets, reusing the last one when the sets are the same.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <returns>The layout.</returns>
    private H265PictureLayout LayoutFor(H265Sps sps, H265Pps pps)
    {
        if (_layout is null || !ReferenceEquals(_layoutPps, pps) || !ReferenceEquals(_layoutSps, sps))
        {
            _layout = new(sps, pps);
            _layoutPps = pps;
            _layoutSps = sps;
        }

        return _layout;
    }

    /// <summary>Makes a mid-grey stand-in for a reference picture the stream did not provide.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <param name="poc">The picture order count of the missing picture.</param>
    /// <returns>The picture.</returns>
    private H265Picture MakeMissing(H265Sps sps, H265Pps pps, int poc)
    {
        if (_hardware is not null)
        {
            var stand = CreateHardwarePicture(sps);
            stand.Poc = poc;
            stand.Sps = sps;
            stand.ReferenceState = 1;
            stand.IsGenerated = true;
            return stand;
        }

        var picture = new H265Picture(sps, LayoutFor(sps, pps)) { Poc = poc, Sps = sps, ReferenceState = 1, IsGenerated = true };
        Array.Fill(picture.Luma, (ushort)(1 << (sps.BitDepthLuma - 1)));
        Array.Fill(picture.Cb, (ushort)(1 << (sps.BitDepthChroma - 1)));
        Array.Fill(picture.Cr, (ushort)(1 << (sps.BitDepthChroma - 1)));
        return picture;
    }
}
