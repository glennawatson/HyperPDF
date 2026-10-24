// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Codecs;
using HyperPdf.Codecs.Video;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>
/// Decodes H.264 and H.265 pictures with the decoder macOS provides, through VideoToolbox. The pictures keep the form the file
/// stores them in, with the length of each NAL unit in front of it, because the format description says how long those lengths are.
/// VideoToolbox gives the pictures in the order they were coded, so they are held back and sorted by the time they are shown.
/// </summary>
[DebuggerDisplay("VideoToolboxDecoder: {Width}x{Height}")]
internal sealed unsafe class VideoToolboxDecoder : VideoDecoder
{
    /// <summary>The most pictures that H.264 and H.265 let come before another in decoding order and after it in output order, used when the track does not say.</summary>
    private const int LargestReorderDepth = 16;

    /// <summary>The flag that asks CoreMedia to allocate the memory of a block buffer at once (<c>kCMBlockBufferAssureMemoryNowFlag</c>).</summary>
    private const uint AssureMemoryNow = 1;

    /// <summary>The lock flag that says memory is only read (<c>kCVPixelBufferLock_ReadOnly</c>).</summary>
    private const ulong ReadOnlyLock = 1;

    /// <summary>The words in a <c>VTDecompressionOutputCallbackRecord</c>: the function, then the value passed to it.</summary>
    private const int CallbackWords = 2;

    /// <summary>The plane of a pixel buffer that holds luma.</summary>
    private const int LumaPlane = 0;

    /// <summary>The plane of a pixel buffer that holds interleaved Cb and Cr.</summary>
    private const int ColourPlane = 1;

    /// <summary>The coded pictures' format.</summary>
    private readonly CoreFoundationHandle _description;

    /// <summary>The session that decodes.</summary>
    private readonly DecompressionSessionHandle _session = new();

    /// <summary>How many decoded pictures are held back before the earliest is given out.</summary>
    private readonly int _depth;

    /// <summary>The decoded pictures not yet given out.</summary>
    private readonly List<VideoFrame> _pending = [];

    /// <summary>Guards <see cref="_pending"/> and <see cref="_failure"/>, which the VideoToolbox callback may write from another thread.</summary>
    private readonly Lock _gate = new();

    /// <summary>Keeps this decoder reachable for the callback while the session lives.</summary>
    private GCHandle _self;

    /// <summary>Why a picture could not be decoded, or <see langword="null"/>.</summary>
    private string? _failure;

    /// <summary>Initializes a new instance of the <see cref="VideoToolboxDecoder"/> class.</summary>
    /// <param name="track">The track.</param>
    /// <param name="description">The coded pictures' format.</param>
    private VideoToolboxDecoder(MediaTrack track, CoreFoundationHandle description)
        : base(track.Width, track.Height)
    {
        _description = description;
        var depth = VideoDecoders.ReorderDepth(track);
        _depth = depth >= 0 ? depth : LargestReorderDepth;
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(output);
        Submit(packet);
        ThrowIfFailed();
        Release(output, _depth);
    }

    /// <inheritdoc/>
    public override void Flush(List<VideoFrame> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Require(NativeMethods.VTDecompressionSessionWaitForAsynchronousFrames(_session.Pointer), "finish decoding pictures");
        ThrowIfFailed();
        Release(output, 0);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _ = NativeMethods.VTDecompressionSessionWaitForAsynchronousFrames(_session.Pointer);
        lock (_gate)
        {
            _pending.Clear();
            _failure = null;
        }
    }

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The track.</param>
    /// <param name="failure">Receives why no decoder was made, when none was.</param>
    /// <returns>The decoder, or <see langword="null"/> when this is not macOS, the coding is not H.264 or H.265, or macOS has no decoder for it.</returns>
    internal static VideoToolboxDecoder? TryCreate(MediaTrack track, out DecoderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (!OperatingSystem.IsMacOS() || track.Kind != MediaTrackKind.Video || track.Width <= 0 || track.Height <= 0 || track.Codec is not (MediaCodec.H264 or MediaCodec.H265))
        {
            failure = new("The track is not one VideoToolbox can be given", 0);
            return null;
        }

        var description = FormatDescription.Create(track, out failure);
        if (description is null)
        {
            return null;
        }

        var decoder = new VideoToolboxDecoder(track, description);
        if (decoder.Start(out failure))
        {
            return decoder;
        }

        decoder.Dispose();
        return null;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _session.Dispose();
        _description.Dispose();
        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }

    /// <summary>Called by VideoToolbox with each decoded picture.</summary>
    /// <param name="reference">The handle of the decoder.</param>
    /// <param name="frameReference">The value given with the coded picture, which is not used.</param>
    /// <param name="status">An OSStatus; 0 when the picture decoded.</param>
    /// <param name="flags">Facts about the decode, which are not used.</param>
    /// <param name="image">The decoded picture.</param>
    /// <param name="time">When the picture is shown.</param>
    /// <param name="duration">How long the picture is shown, which is not used.</param>
    [UnmanagedCallersOnly]
    private static void OnPicture(void* reference, void* frameReference, int status, uint flags, void* image, CoreMediaTime time, CoreMediaTime duration)
    {
        if (GCHandle.FromIntPtr((nint)reference).Target is VideoToolboxDecoder decoder)
        {
            decoder.Accept(status, image, time);
        }
    }

    /// <summary>Copies a decoded picture into a frame.</summary>
    /// <param name="image">The decoded picture.</param>
    /// <param name="frame">Receives the picture.</param>
    /// <returns>Why the picture could not be read, or <see langword="null"/> when it was copied.</returns>
    private static string? TryCopy(void* image, VideoFrame frame)
    {
        var layout = NativeMethods.CVPixelBufferGetPixelFormatType(image);
        if (layout is not (PixelBufferRequest.VideoRangeNv12 or PixelBufferRequest.FullRangeNv12))
        {
            return $"VideoToolbox gave pictures in a layout that is not read (0x{layout:X8}).";
        }

        if (NativeMethods.CVPixelBufferLockBaseAddress(image, ReadOnlyLock) != 0)
        {
            return "VideoToolbox gave a picture that cannot be read.";
        }

        try
        {
            var luma = NativeMethods.CVPixelBufferGetBaseAddressOfPlane(image, LumaPlane);
            var colour = NativeMethods.CVPixelBufferGetBaseAddressOfPlane(image, ColourPlane);
            var lumaStride = (int)NativeMethods.CVPixelBufferGetBytesPerRowOfPlane(image, LumaPlane);
            var colourStride = (int)NativeMethods.CVPixelBufferGetBytesPerRowOfPlane(image, ColourPlane);
            var lumaRows = (int)NativeMethods.CVPixelBufferGetHeightOfPlane(image, LumaPlane);
            var colourRows = (int)NativeMethods.CVPixelBufferGetHeightOfPlane(image, ColourPlane);
            Nv12Planes.Copy(new(luma, lumaStride * lumaRows), lumaStride, new(colour, colourStride * colourRows), colourStride, frame);
            return null;
        }
        catch (ArgumentException)
        {
            return "VideoToolbox gave a picture smaller than the track says.";
        }
        finally
        {
            _ = NativeMethods.CVPixelBufferUnlockBaseAddress(image, ReadOnlyLock);
        }
    }

    /// <summary>Throws when a CoreMedia or VideoToolbox call failed.</summary>
    /// <param name="status">The OSStatus.</param>
    /// <param name="step">What was being done.</param>
    /// <exception cref="InvalidDataException">The call failed.</exception>
    private static void Require(int status, string step)
    {
        if (status != 0)
        {
            throw new InvalidDataException($"VideoToolbox could not {step} (0x{status:X8}).");
        }
    }

    /// <summary>Starts the session.</summary>
    /// <param name="failure">Receives why the session did not start, when it did not.</param>
    /// <returns><see langword="true"/> when the session started.</returns>
    private bool Start(out DecoderFailure failure)
    {
        failure = default;
        using var request = PixelBufferRequest.Create();
        if (request is null)
        {
            failure = new("CoreVideo does not name the picture layout", 0);
            return false;
        }

        _self = GCHandle.Alloc(this);
        nint* callback = stackalloc nint[CallbackWords];
        callback[0] = (nint)(delegate* unmanaged<void*, void*, int, uint, void*, CoreMediaTime, CoreMediaTime, void>)&OnPicture;
        callback[1] = GCHandle.ToIntPtr(_self);
        void* session;
        var status = NativeMethods.VTDecompressionSessionCreate(null, _description.Pointer, null, request.Pointer, callback, &session);
        if (status != 0)
        {
            failure = new("VideoToolbox has no decoder for the coding", status);
            return false;
        }

        _session.Adopt(session);
        return true;
    }

    /// <summary>Gives the decoder one coded picture and waits for it to be decoded.</summary>
    /// <param name="packet">The packet.</param>
    /// <exception cref="InvalidDataException">VideoToolbox did not accept the packet.</exception>
    private void Submit(MediaPacket packet)
    {
        var data = packet.Data;
        if (data.IsEmpty)
        {
            throw new InvalidDataException("A packet of pictures is empty.");
        }

        void* block;
        var status = NativeMethods.CMBlockBufferCreateWithMemoryBlock(null, null, data.Length, null, null, 0, data.Length, AssureMemoryNow, &block);
        Require(status, "make a buffer");
        using CoreFoundationHandle blockHandle = new(block);
        fixed (byte* bytes = data)
        {
            Require(NativeMethods.CMBlockBufferReplaceDataBytes(bytes, block, 0, data.Length), "fill a buffer");
        }

        var timing = new SampleTiming(
            packet.Duration > 0 ? CoreMediaTime.FromSeconds(packet.Duration) : CoreMediaTime.Invalid,
            CoreMediaTime.FromSeconds(packet.Time),
            CoreMediaTime.Invalid);
        nint size = data.Length;
        void* sample;
        Require(NativeMethods.CMSampleBufferCreateReady(null, block, _description.Pointer, 1, 1, &timing, 1, &size, &sample), "make a sample");
        using CoreFoundationHandle sampleHandle = new(sample);
        uint info;
        Require(NativeMethods.VTDecompressionSessionDecodeFrame(_session.Pointer, sample, 0, null, &info), "decode a picture");
        Require(NativeMethods.VTDecompressionSessionWaitForAsynchronousFrames(_session.Pointer), "finish decoding a picture");
    }

    /// <summary>Stores a decoded picture, or the reason there is none.</summary>
    /// <param name="status">An OSStatus; 0 when the picture decoded.</param>
    /// <param name="image">The decoded picture.</param>
    /// <param name="time">When the picture is shown.</param>
    private void Accept(int status, void* image, CoreMediaTime time)
    {
        string? failure = null;
        VideoFrame? frame = null;
        if (status != 0 || image is null)
        {
            failure = $"VideoToolbox could not decode a picture (0x{status:X8}).";
        }
        else
        {
            frame = new(Width, Height, time.Seconds);
            failure = TryCopy(image, frame);
        }

        lock (_gate)
        {
            if (failure is not null)
            {
                _failure = failure;
            }
            else if (frame is not null)
            {
                _pending.Add(frame);
            }
        }
    }

    /// <summary>Gives out the earliest pictures, keeping some back.</summary>
    /// <param name="output">Receives the pictures, earliest first.</param>
    /// <param name="keep">How many pictures stay held back.</param>
    private void Release(List<VideoFrame> output, int keep)
    {
        lock (_gate)
        {
            _pending.Sort(static (first, second) => first.Time.CompareTo(second.Time));
            var count = Math.Max(0, _pending.Count - keep);
            for (var i = 0; i < count; i++)
            {
                output.Add(_pending[i]);
            }

            _pending.RemoveRange(0, count);
        }
    }

    /// <summary>Throws when a picture could not be decoded.</summary>
    /// <exception cref="InvalidDataException">A picture could not be decoded.</exception>
    private void ThrowIfFailed()
    {
        string? failure;
        lock (_gate)
        {
            failure = _failure;
            _failure = null;
        }

        if (failure is not null)
        {
            throw new InvalidDataException(failure);
        }
    }
}
