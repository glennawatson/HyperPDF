// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>One Media Foundation video decoder: a transform that takes coded pictures and gives NV12 pictures. It is used by one thread at a time.</summary>
[DebuggerDisplay("VideoTransform: {OutputWidth}x{OutputHeight}")]
internal sealed unsafe class VideoTransform : IDisposable
{
    /// <summary>The message that throws away the pictures the transform holds (<c>MFT_MESSAGE_COMMAND_FLUSH</c>).</summary>
    internal const int MessageFlush = 0;

    /// <summary>The message that makes the transform give every picture it holds (<c>MFT_MESSAGE_COMMAND_DRAIN</c>).</summary>
    internal const int MessageDrain = 1;

    /// <summary>The result a transform gives when it needs more input before it can give output.</summary>
    internal const int NeedMoreInput = unchecked((int)0xC00D6D72);

    /// <summary>The result a transform gives when its output type must be chosen again.</summary>
    internal const int StreamChange = unchecked((int)0xC00D6D61);

    /// <summary>The result a transform gives when it cannot take more input until output is read.</summary>
    internal const int NotAccepting = unchecked((int)0xC00D36B5);

    /// <summary>The version of Media Foundation the code was written for.</summary>
    private const uint MediaFoundationVersion = 0x0002_0070;

    /// <summary>The flags that list only software transforms in this process that work on the calling thread, best first: <c>SYNCMFT | LOCALMFT | SORTANDFILTER</c>.</summary>
    private const uint SyncLocalSorted = 0x1 | 0x10 | 0x40;

    /// <summary>The multithreaded COM apartment.</summary>
    private const uint MultiThreaded = 0;

    /// <summary>The message that says pictures are about to be sent (<c>MFT_MESSAGE_NOTIFY_BEGIN_STREAMING</c>).</summary>
    private const int MessageBeginStreaming = 0x1000_0000;

    /// <summary>The message that says the first picture is about to be sent (<c>MFT_MESSAGE_NOTIFY_START_OF_STREAM</c>).</summary>
    private const int MessageStartOfStream = 0x1000_0003;

    /// <summary>The value of the interlace attribute for pictures that are not interlaced (<c>MFVideoInterlace_Progressive</c>).</summary>
    private const uint Progressive = 2;

    /// <summary>The <c>IMFActivate::ActivateObject</c> slot.</summary>
    private const int ActivateObjectSlot = 33;

    /// <summary>The <c>IMFTransform::GetOutputStreamInfo</c> slot.</summary>
    private const int GetOutputStreamInfoSlot = 7;

    /// <summary>The <c>IMFTransform::GetOutputAvailableType</c> slot.</summary>
    private const int GetOutputAvailableTypeSlot = 14;

    /// <summary>The <c>IMFTransform::SetInputType</c> slot.</summary>
    private const int SetInputTypeSlot = 15;

    /// <summary>The <c>IMFTransform::SetOutputType</c> slot.</summary>
    private const int SetOutputTypeSlot = 16;

    /// <summary>The <c>IMFTransform::GetOutputCurrentType</c> slot.</summary>
    private const int GetOutputCurrentTypeSlot = 18;

    /// <summary>The <c>IMFTransform::ProcessMessage</c> slot.</summary>
    private const int ProcessMessageSlot = 23;

    /// <summary>The <c>IMFTransform::ProcessInput</c> slot.</summary>
    private const int ProcessInputSlot = 24;

    /// <summary>The <c>IMFTransform::ProcessOutput</c> slot.</summary>
    private const int ProcessOutputSlot = 25;

    /// <summary>The number of bits a frame's width is shifted up in the 64 bit frame size attribute.</summary>
    private const int WidthShift = 32;

    /// <summary>The IMFTransform.</summary>
    private readonly ComHandle _transform;

    /// <summary>Whether Media Foundation was started for this transform.</summary>
    private bool _started;

    /// <summary>Initializes a new instance of the <see cref="VideoTransform"/> class.</summary>
    /// <param name="transform">The IMFTransform, whose reference the new object takes over.</param>
    private VideoTransform(ComHandle transform)
    {
        _transform = transform;
        _started = true;
    }

    /// <summary>Gets the width of the pictures the transform gives, which may be a little more than the display width.</summary>
    internal int OutputWidth { get; private set; }

    /// <summary>Gets the height of the pictures the transform gives, which may be a little more than the display height.</summary>
    internal int OutputHeight { get; private set; }

    /// <summary>Gets the distance between the starts of two rows of the pictures the transform gives, in bytes.</summary>
    internal int OutputStride { get; private set; }

    /// <summary>Gets the size of the memory the transform needs for a picture it gives, in bytes.</summary>
    internal int OutputSize { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transform.Dispose();
        if (!_started)
        {
            return;
        }

        _ = NativeMethods.MFShutdown();
        _started = false;
    }

    /// <summary>Finds a software decoder for a coding, gives it the coded picture size and asks for NV12 pictures.</summary>
    /// <param name="subtype">The coding, <c>MFVideoFormat_H264</c> or <c>MFVideoFormat_HEVC</c>.</param>
    /// <param name="width">The width of the pictures in pixels.</param>
    /// <param name="height">The height of the pictures in pixels.</param>
    /// <param name="failure">Receives why no transform was made, when none was.</param>
    /// <returns>The transform, or <see langword="null"/> when this computer has no decoder for the coding.</returns>
    internal static VideoTransform? Open(Guid subtype, int width, int height, out DecoderFailure failure)
    {
        _ = NativeMethods.CoInitializeEx(0, MultiThreaded);
        var started = NativeMethods.MFStartup(MediaFoundationVersion, 0);
        if (started < 0)
        {
            failure = new("Media Foundation did not start", started);
            return null;
        }

        var transform = Activate(subtype, out failure);
        if (transform is null)
        {
            _ = NativeMethods.MFShutdown();
            return null;
        }

        var result = new VideoTransform(transform);
        if (result.Initialise(subtype, width, height, out failure))
        {
            return result;
        }

        result.Dispose();
        return null;
    }

    /// <summary>Sends a control message to the transform.</summary>
    /// <param name="message">The message, one of the <c>MFT_MESSAGE_TYPE</c> values.</param>
    internal void Message(int message) =>
        _ = ((delegate* unmanaged[Stdcall]<void*, int, nuint, int>)Com.Slot(_transform.Pointer, ProcessMessageSlot))(_transform.Pointer, message, 0);

    /// <summary>Gives the transform a coded picture.</summary>
    /// <param name="sample">The sample holding the picture.</param>
    /// <returns>An HRESULT.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Input(void* sample) =>
        ((delegate* unmanaged[Stdcall]<void*, uint, void*, uint, int>)Com.Slot(_transform.Pointer, ProcessInputSlot))(_transform.Pointer, 0, sample, 0);

    /// <summary>Asks the transform for a decoded picture.</summary>
    /// <param name="buffer">Where the picture goes.</param>
    /// <returns>An HRESULT.</returns>
    internal int Output(ref OutputDataBuffer buffer)
    {
        uint status;
        fixed (OutputDataBuffer* pointer = &buffer)
        {
            return ((delegate* unmanaged[Stdcall]<void*, uint, uint, OutputDataBuffer*, uint*, int>)Com.Slot(_transform.Pointer, ProcessOutputSlot))(_transform.Pointer, 0, 1, pointer, &status);
        }
    }

    /// <summary>Chooses the NV12 output type again, after the transform says the stream changed, and reads the new geometry.</summary>
    /// <returns><see langword="true"/> when an NV12 output type was set.</returns>
    internal bool ChooseOutput()
    {
        uint index = 0;
        while (true)
        {
            void* type;
            if (((delegate* unmanaged[Stdcall]<void*, uint, uint, void**, int>)Com.Slot(_transform.Pointer, GetOutputAvailableTypeSlot))(_transform.Pointer, 0, index, &type) < 0)
            {
                return false;
            }

            var isNv12 = Com.TryGetGuid(type, MediaFoundationIds.SubtypeKey, out var subtype) && subtype == MediaFoundationIds.Nv12Subtype;
            var set = isNv12 && ((delegate* unmanaged[Stdcall]<void*, uint, void*, uint, int>)Com.Slot(_transform.Pointer, SetOutputTypeSlot))(_transform.Pointer, 0, type, 0) >= 0;
            Com.Release(type);
            if (set)
            {
                return ReadOutputGeometry();
            }

            index++;
        }
    }

    /// <summary>Makes the best decoder for a coding.</summary>
    /// <param name="subtype">The coding.</param>
    /// <param name="failure">Receives why no transform was made, when none was.</param>
    /// <returns>The transform, or null.</returns>
    private static ComHandle? Activate(Guid subtype, out DecoderFailure failure)
    {
        var input = new RegisterTypeInfo(MediaFoundationIds.VideoType, subtype);
        void** activates;
        uint count;
        var listed = NativeMethods.MFTEnumEx(MediaFoundationIds.VideoDecoderCategory, SyncLocalSorted, &input, null, &activates, &count);
        if (listed < 0 || activates is null || count == 0)
        {
            failure = new("Windows lists no software decoder for the coding", listed);
            return null;
        }

        ComHandle? transform = null;
        var made = 0;
        for (uint i = 0; i < count; i++)
        {
            transform ??= ActivateTransform(activates[i], out made);
            Com.Release(activates[i]);
        }

        NativeMethods.CoTaskMemFree(activates);
        failure = new("A listed decoder could not be started", made);
        return transform;
    }

    /// <summary>Makes the transform an activation object stands for.</summary>
    /// <param name="activate">The activation object.</param>
    /// <param name="result">Receives the HRESULT of making it.</param>
    /// <returns>The transform, or null.</returns>
    private static ComHandle? ActivateTransform(void* activate, out int result)
    {
        var iid = MediaFoundationIds.TransformInterface;
        void* made;
        result = ((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)Com.Slot(activate, ActivateObjectSlot))(activate, &iid, &made);
        return result >= 0 ? new ComHandle(made) : null;
    }

    /// <summary>Sets up the decoder.</summary>
    /// <param name="subtype">The coding.</param>
    /// <param name="width">The width of the pictures.</param>
    /// <param name="height">The height of the pictures.</param>
    /// <param name="failure">Receives why setting up failed, when it did.</param>
    /// <returns><see langword="true"/> when the decoder takes the coding and gives NV12.</returns>
    private bool Initialise(Guid subtype, int width, int height, out DecoderFailure failure)
    {
        var input = SetInput(subtype, width, height);
        if (input < 0)
        {
            failure = new("The decoder did not accept the picture type", input);
            return false;
        }

        if (!ChooseOutput())
        {
            failure = new("The decoder offers no NV12 output", 0);
            return false;
        }

        Message(MessageBeginStreaming);
        Message(MessageStartOfStream);
        failure = default;
        return true;
    }

    /// <summary>Tells the decoder what it will be given.</summary>
    /// <param name="subtype">The coding.</param>
    /// <param name="width">The width of the pictures.</param>
    /// <param name="height">The height of the pictures.</param>
    /// <returns>An HRESULT: 0 when the decoder accepted the type.</returns>
    private int SetInput(Guid subtype, int width, int height)
    {
        void* type;
        var created = NativeMethods.MFCreateMediaType(&type);
        if (created < 0)
        {
            return created;
        }

        var result = Com.SetGuid(type, MediaFoundationIds.MajorTypeKey, MediaFoundationIds.VideoType);
        result = result < 0 ? result : Com.SetGuid(type, MediaFoundationIds.SubtypeKey, subtype);
        result = result < 0 ? result : Com.SetUInt64(type, MediaFoundationIds.FrameSizeKey, ((ulong)(uint)width << WidthShift) | (uint)height);
        result = result < 0 ? result : Com.SetUInt32(type, MediaFoundationIds.InterlaceModeKey, Progressive);
        result = result < 0
            ? result
            : ((delegate* unmanaged[Stdcall]<void*, uint, void*, uint, int>)Com.Slot(_transform.Pointer, SetInputTypeSlot))(_transform.Pointer, 0, type, 0);
        Com.Release(type);
        return result;
    }

    /// <summary>Reads the size, row distance and memory size of the pictures the decoder gives.</summary>
    /// <returns><see langword="true"/> when the output type could be read.</returns>
    private bool ReadOutputGeometry()
    {
        void* type;
        if (((delegate* unmanaged[Stdcall]<void*, uint, void**, int>)Com.Slot(_transform.Pointer, GetOutputCurrentTypeSlot))(_transform.Pointer, 0, &type) < 0)
        {
            return false;
        }

        var hasSize = Com.TryGetUInt64(type, MediaFoundationIds.FrameSizeKey, out var packed);
        OutputWidth = (int)(packed >> WidthShift);
        OutputHeight = (int)(packed & uint.MaxValue);
        OutputStride = Com.TryGetUInt32(type, MediaFoundationIds.DefaultStrideKey, out var stride) && (int)stride > 0 ? (int)stride : OutputWidth;
        Com.Release(type);
        OutputStreamInfo info;
        var read = ((delegate* unmanaged[Stdcall]<void*, uint, OutputStreamInfo*, int>)Com.Slot(_transform.Pointer, GetOutputStreamInfoSlot))(_transform.Pointer, 0, &info) >= 0;
        OutputSize = read ? (int)info.Size : 0;
        return hasSize && read && OutputWidth > 0 && OutputHeight > 0;
    }
}
