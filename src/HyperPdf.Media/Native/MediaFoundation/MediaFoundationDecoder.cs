// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using HyperPdf.Codecs;
using HyperPdf.Codecs.Video;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>
/// Decodes H.264 and H.265 pictures with the decoder Windows provides, through Media Foundation. The pictures are given as the
/// byte stream with start codes that the Windows decoders take, with the parameter sets in front of each key picture, and
/// come back in the order they are shown.
/// </summary>
[DebuggerDisplay("MediaFoundationDecoder: {Width}x{Height}")]
internal sealed unsafe class MediaFoundationDecoder : VideoDecoder
{
    /// <summary>The number of 100 nanosecond units in a second, which is how Media Foundation counts time.</summary>
    private const double TicksPerSecond = 10_000_000;

    /// <summary>The bytes of a sample of 4:2:0 pictures for each pixel, as a share: three halves.</summary>
    private const int ChromaShare = 3;

    /// <summary>The divisor of <see cref="ChromaShare"/>.</summary>
    private const int ChromaDivisor = 2;

    /// <summary>The HRESULT for running out of memory.</summary>
    private const int OutOfMemory = unchecked((int)0x8007000E);

    /// <summary>The HRESULT for an unspecified failure.</summary>
    private const int Failed = unchecked((int)0x80004005);

    /// <summary>The transform that decodes.</summary>
    private readonly VideoTransform _transform;

    /// <summary>The bytes of each NAL unit's length in front of it in the packets.</summary>
    private readonly int _lengthSize;

    /// <summary>The parameter sets of the track's configuration, as a byte stream.</summary>
    private readonly byte[] _parameterSets;

    /// <summary>The byte stream of the picture being given to the decoder.</summary>
    private readonly ArrayBufferWriter<byte> _stream = new();

    /// <summary>The sample the decoder fills with a picture.</summary>
    private ComHandle? _outputSample;

    /// <summary>The buffer of the output sample.</summary>
    private ComHandle? _outputBuffer;

    /// <summary>Whether the parameter sets must go in front of the next picture.</summary>
    private bool _needSets = true;

    /// <summary>Initializes a new instance of the <see cref="MediaFoundationDecoder"/> class.</summary>
    /// <param name="track">The track.</param>
    /// <param name="transform">The transform that decodes.</param>
    /// <param name="lengthSize">The bytes of each NAL unit's length.</param>
    /// <param name="parameterSets">The parameter sets as a byte stream.</param>
    private MediaFoundationDecoder(MediaTrack track, VideoTransform transform, int lengthSize, byte[] parameterSets)
        : base(track.Width, track.Height)
    {
        _transform = transform;
        _lengthSize = lengthSize;
        _parameterSets = parameterSets;
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        _stream.ResetWrittenCount();
        if (_needSets || packet.IsKey)
        {
            _stream.Write(_parameterSets);
            _needSets = false;
        }

        if (!AnnexB.WriteUnits(packet.Data, _lengthSize, _stream))
        {
            throw new InvalidDataException("A packet of pictures is not valid.");
        }

        var result = Submit(packet.Time, packet.Duration);
        if (result == VideoTransform.NotAccepting)
        {
            Drain(output);
            result = Submit(packet.Time, packet.Duration);
        }

        if (result < 0)
        {
            throw new InvalidDataException($"Windows did not accept a picture (0x{result:X8}).");
        }

        Drain(output);
    }

    /// <inheritdoc/>
    public override void Flush(List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _transform.Message(VideoTransform.MessageDrain);
        Drain(output);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _transform.Message(VideoTransform.MessageFlush);
        _needSets = true;
    }

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The track.</param>
    /// <param name="failure">Receives why no decoder was made, when none was.</param>
    /// <returns>The decoder, or <see langword="null"/> when this is not Windows, the coding is not H.264 or H.265, or Windows has no decoder for it.</returns>
    internal static MediaFoundationDecoder? TryCreate(MediaTrack track, out DecoderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(track);
        var subtype = SubtypeOf(track);
        var sets = new ArrayBufferWriter<byte>();
        var lengthSize = AnnexB.LengthSize(track.Codec, track.Configuration.Span);
        if (subtype == Guid.Empty || lengthSize == 0 || !AnnexB.WriteParameterSets(track.Codec, track.Configuration.Span, sets))
        {
            failure = new("The track is not one Media Foundation can be given", 0);
            return null;
        }

        var transform = VideoTransform.Open(subtype, track.Width, track.Height, out failure);
        if (transform is null)
        {
            return null;
        }

        var decoder = new MediaFoundationDecoder(track, transform, lengthSize, sets.WrittenSpan.ToArray());
        if (decoder.PrepareOutput())
        {
            return decoder;
        }

        failure = new("The output sample could not be made", 0);
        decoder.Dispose();
        return null;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _outputSample?.Dispose();
        _outputBuffer?.Dispose();
        _transform.Dispose();
    }

    /// <summary>Finds the Media Foundation subtype of a track's coding.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The subtype, or <see cref="Guid.Empty"/> when Media Foundation cannot be used for the track.</returns>
    private static Guid SubtypeOf(MediaTrack track) => !OperatingSystem.IsWindows() || track.Kind != MediaTrackKind.Video || track.Width <= 0 || track.Height <= 0
        ? Guid.Empty
        : track.Codec switch
        {
            MediaCodec.H264 => MediaFoundationIds.H264Subtype,
            MediaCodec.H265 => MediaFoundationIds.HevcSubtype,
            _ => Guid.Empty,
        };

    /// <summary>Makes the sample the decoder fills, sized for the pictures it gives.</summary>
    /// <returns><see langword="true"/> when the sample was made.</returns>
    private bool PrepareOutput()
    {
        _outputSample?.Dispose();
        _outputBuffer?.Dispose();
        _outputSample = null;
        _outputBuffer = null;
        var size = _transform.OutputSize > 0 ? _transform.OutputSize : _transform.OutputStride * _transform.OutputHeight * ChromaShare / ChromaDivisor;
        void* buffer;
        void* sample;
        if (NativeMethods.MFCreateMemoryBuffer((uint)size, &buffer) < 0)
        {
            return false;
        }

        var bufferHandle = new ComHandle(buffer);
        if (NativeMethods.MFCreateSample(&sample) < 0)
        {
            bufferHandle.Dispose();
            return false;
        }

        var sampleHandle = new ComHandle(sample);
        if (Com.AddBuffer(sample, buffer) < 0)
        {
            sampleHandle.Dispose();
            bufferHandle.Dispose();
            return false;
        }

        _outputBuffer = bufferHandle;
        _outputSample = sampleHandle;
        return true;
    }

    /// <summary>Gives the decoder the byte stream built for the picture.</summary>
    /// <param name="time">When the picture is shown, in seconds.</param>
    /// <param name="duration">How long the picture is shown, in seconds, or 0.</param>
    /// <returns>An HRESULT.</returns>
    private int Submit(double time, double duration)
    {
        var data = _stream.WrittenSpan;
        void* buffer;
        if (NativeMethods.MFCreateMemoryBuffer((uint)data.Length, &buffer) < 0)
        {
            return OutOfMemory;
        }

        using ComHandle bufferHandle = new(buffer);
        byte* target;
        var locked = Com.LockBuffer(buffer, &target, null);
        if (locked < 0)
        {
            return locked;
        }

        data.CopyTo(new(target, data.Length));
        Com.UnlockBuffer(buffer);
        Com.SetLength(buffer, (uint)data.Length);
        void* sample;
        var created = NativeMethods.MFCreateSample(&sample);
        if (created < 0)
        {
            return created;
        }

        using ComHandle sampleHandle = new(sample);
        if (Com.AddBuffer(sample, buffer) < 0)
        {
            return Failed;
        }

        Com.SetTimes(sample, (long)(time * TicksPerSecond), (long)(duration * TicksPerSecond));
        return _transform.Input(sample);
    }

    /// <summary>Collects every picture the decoder has ready.</summary>
    /// <param name="output">Receives the pictures.</param>
    /// <exception cref="InvalidDataException">The decoder failed.</exception>
    private void Drain(List<VideoFrame> output)
    {
        while (true)
        {
            Com.SetLength(_outputBuffer!.Pointer, 0);
            var buffer = new OutputDataBuffer(_outputSample!);
            var result = _transform.Output(ref buffer);
            buffer.ReleaseEvents();
            if (result == VideoTransform.NeedMoreInput)
            {
                return;
            }

            if (result == VideoTransform.StreamChange)
            {
                if (!_transform.ChooseOutput() || !PrepareOutput())
                {
                    throw new InvalidDataException("Windows changed the pictures it gives to a form that cannot be read.");
                }

                continue;
            }

            if (result < 0)
            {
                throw new InvalidDataException($"Windows could not decode a picture (0x{result:X8}).");
            }

            output.Add(ReadPicture());
        }
    }

    /// <summary>Copies the picture the decoder just filled into planes.</summary>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The picture cannot be read.</exception>
    private VideoFrame ReadPicture()
    {
        var sample = _outputSample!.Pointer;
        void* contiguous;
        if (Com.ConvertToContiguous(sample, &contiguous) < 0)
        {
            throw new InvalidDataException("Windows gave a picture that cannot be read.");
        }

        using ComHandle contiguousHandle = new(contiguous);
        byte* data;
        uint length;
        if (Com.LockBuffer(contiguous, &data, &length) < 0)
        {
            throw new InvalidDataException("Windows gave a picture that cannot be read.");
        }

        try
        {
            var frame = RentFrame(Width, Height, Com.GetTime(sample) / TicksPerSecond);
            Nv12Planes.Copy(new(data, (int)length), _transform.OutputStride, _transform.OutputHeight, frame);
            return frame;
        }
        finally
        {
            Com.UnlockBuffer(contiguous);
        }
    }
}
