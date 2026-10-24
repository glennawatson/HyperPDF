// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>Source generated entry points of the macOS CoreFoundation, CoreMedia, CoreVideo and VideoToolbox functions the video decoder uses, all part of macOS itself.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The path of the CoreFoundation framework.</summary>
    internal const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>The path of the CoreVideo framework.</summary>
    internal const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";

    /// <summary>The path of the CoreMedia framework.</summary>
    internal const string CoreMedia = "/System/Library/Frameworks/CoreMedia.framework/CoreMedia";

    /// <summary>The path of the VideoToolbox framework.</summary>
    internal const string VideoToolbox = "/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox";

    /// <summary>Native <c>CFRelease</c>.</summary>
    /// <param name="instance">The object to let go of.</param>
    [LibraryImport(CoreFoundation, EntryPoint = "CFRelease")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial void CFRelease(void* instance);

    /// <summary>Native <c>CFNumberCreate</c>.</summary>
    /// <param name="allocator">The allocator, or null for the default.</param>
    /// <param name="type">The kind of number.</param>
    /// <param name="value">The number.</param>
    /// <returns>The number object.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFNumberCreate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial void* CFNumberCreate(void* allocator, int type, int* value);

    /// <summary>Native <c>CFDictionaryCreate</c>.</summary>
    /// <param name="allocator">The allocator, or null for the default.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="values">The values.</param>
    /// <param name="count">The number of pairs.</param>
    /// <param name="keyCallbacks">How the dictionary treats keys.</param>
    /// <param name="valueCallbacks">How the dictionary treats values.</param>
    /// <returns>The dictionary.</returns>
    [LibraryImport(CoreFoundation, EntryPoint = "CFDictionaryCreate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial void* CFDictionaryCreate(void* allocator, void** keys, void** values, nint count, void* keyCallbacks, void* valueCallbacks);

    /// <summary>Native <c>CMVideoFormatDescriptionCreateFromH264ParameterSets</c>.</summary>
    /// <param name="allocator">The allocator, or null for the default.</param>
    /// <param name="count">The number of parameter sets.</param>
    /// <param name="sets">The start of each set.</param>
    /// <param name="sizes">The length of each set.</param>
    /// <param name="lengthSize">The bytes of each NAL unit's length in the pictures.</param>
    /// <param name="description">Receives the description.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(CoreMedia, EntryPoint = "CMVideoFormatDescriptionCreateFromH264ParameterSets")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CMVideoFormatDescriptionCreateFromH264ParameterSets(void* allocator, nint count, byte** sets, nint* sizes, int lengthSize, void** description);

    /// <summary>Native <c>CMVideoFormatDescriptionCreateFromHEVCParameterSets</c>.</summary>
    /// <param name="allocator">The allocator, or null for the default.</param>
    /// <param name="count">The number of parameter sets.</param>
    /// <param name="sets">The start of each set.</param>
    /// <param name="sizes">The length of each set.</param>
    /// <param name="lengthSize">The bytes of each NAL unit's length in the pictures.</param>
    /// <param name="extensions">Extra description keys, or null.</param>
    /// <param name="description">Receives the description.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(CoreMedia, EntryPoint = "CMVideoFormatDescriptionCreateFromHEVCParameterSets")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CMVideoFormatDescriptionCreateFromHEVCParameterSets(void* allocator, nint count, byte** sets, nint* sizes, int lengthSize, void* extensions, void** description);

    /// <summary>Native <c>CMBlockBufferCreateWithMemoryBlock</c>.</summary>
    /// <param name="allocator">The allocator of the buffer object, or null.</param>
    /// <param name="memory">The memory to wrap, or null to have it allocated.</param>
    /// <param name="length">The size of the memory.</param>
    /// <param name="blockAllocator">The allocator of the memory, or null.</param>
    /// <param name="customSource">A custom source, or null.</param>
    /// <param name="offset">Where the data starts in the memory.</param>
    /// <param name="dataLength">The length of the data.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="buffer">Receives the buffer.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(CoreMedia, EntryPoint = "CMBlockBufferCreateWithMemoryBlock")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CMBlockBufferCreateWithMemoryBlock(
        void* allocator,
        void* memory,
        nint length,
        void* blockAllocator,
        void* customSource,
        nint offset,
        nint dataLength,
        uint flags,
        void** buffer);

    /// <summary>Native <c>CMBlockBufferReplaceDataBytes</c>.</summary>
    /// <param name="source">The bytes to copy in.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">Where in the buffer the bytes go.</param>
    /// <param name="length">The number of bytes.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(CoreMedia, EntryPoint = "CMBlockBufferReplaceDataBytes")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CMBlockBufferReplaceDataBytes(byte* source, void* buffer, nint offset, nint length);

    /// <summary>Native <c>CMSampleBufferCreateReady</c>.</summary>
    /// <param name="allocator">The allocator, or null.</param>
    /// <param name="data">The block buffer holding the coded picture.</param>
    /// <param name="description">The format description.</param>
    /// <param name="sampleCount">The number of samples.</param>
    /// <param name="timingCount">The number of timing entries.</param>
    /// <param name="timing">The timing entries.</param>
    /// <param name="sizeCount">The number of size entries.</param>
    /// <param name="sizes">The size entries.</param>
    /// <param name="sample">Receives the sample buffer.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(CoreMedia, EntryPoint = "CMSampleBufferCreateReady")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CMSampleBufferCreateReady(
        void* allocator,
        void* data,
        void* description,
        nint sampleCount,
        nint timingCount,
        SampleTiming* timing,
        nint sizeCount,
        nint* sizes,
        void** sample);

    /// <summary>Native <c>VTDecompressionSessionCreate</c>.</summary>
    /// <param name="allocator">The allocator, or null.</param>
    /// <param name="description">The format description of the coded pictures.</param>
    /// <param name="specification">Decoder choices, or null.</param>
    /// <param name="attributes">What the output pictures must look like.</param>
    /// <param name="callback">The two words of a <c>VTDecompressionOutputCallbackRecord</c>: the function that receives the pictures, then the value passed to it.</param>
    /// <param name="session">Receives the session.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(VideoToolbox, EntryPoint = "VTDecompressionSessionCreate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int VTDecompressionSessionCreate(
        void* allocator,
        void* description,
        void* specification,
        void* attributes,
        nint* callback,
        void** session);

    /// <summary>Native <c>VTDecompressionSessionDecodeFrame</c>.</summary>
    /// <param name="session">The session.</param>
    /// <param name="sample">The coded picture.</param>
    /// <param name="flags">The decode flags; 0 decodes before returning.</param>
    /// <param name="frameReference">A value handed back with the picture.</param>
    /// <param name="info">Receives facts about the decode.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(VideoToolbox, EntryPoint = "VTDecompressionSessionDecodeFrame")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int VTDecompressionSessionDecodeFrame(void* session, void* sample, uint flags, void* frameReference, uint* info);

    /// <summary>Native <c>VTDecompressionSessionWaitForAsynchronousFrames</c>.</summary>
    /// <param name="session">The session.</param>
    /// <returns>An OSStatus.</returns>
    [LibraryImport(VideoToolbox, EntryPoint = "VTDecompressionSessionWaitForAsynchronousFrames")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int VTDecompressionSessionWaitForAsynchronousFrames(void* session);

    /// <summary>Native <c>VTDecompressionSessionInvalidate</c>.</summary>
    /// <param name="session">The session.</param>
    [LibraryImport(VideoToolbox, EntryPoint = "VTDecompressionSessionInvalidate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial void VTDecompressionSessionInvalidate(void* session);

    /// <summary>Native <c>CVPixelBufferLockBaseAddress</c>.</summary>
    /// <param name="buffer">The pixel buffer.</param>
    /// <param name="flags">The lock flags; 1 for read only.</param>
    /// <returns>A CVReturn.</returns>
    [LibraryImport(CoreVideo, EntryPoint = "CVPixelBufferLockBaseAddress")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CVPixelBufferLockBaseAddress(void* buffer, ulong flags);

    /// <summary>Native <c>CVPixelBufferUnlockBaseAddress</c>.</summary>
    /// <param name="buffer">The pixel buffer.</param>
    /// <param name="flags">The flags the buffer was locked with.</param>
    /// <returns>A CVReturn.</returns>
    [LibraryImport(CoreVideo, EntryPoint = "CVPixelBufferUnlockBaseAddress")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial int CVPixelBufferUnlockBaseAddress(void* buffer, ulong flags);

    /// <summary>Native <c>CVPixelBufferGetPixelFormatType</c>.</summary>
    /// <param name="buffer">The pixel buffer.</param>
    /// <returns>The four character code of the layout.</returns>
    [LibraryImport(CoreVideo, EntryPoint = "CVPixelBufferGetPixelFormatType")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial uint CVPixelBufferGetPixelFormatType(void* buffer);

    /// <summary>Native <c>CVPixelBufferGetBaseAddressOfPlane</c>.</summary>
    /// <param name="buffer">The pixel buffer, locked.</param>
    /// <param name="plane">The plane.</param>
    /// <returns>The start of the plane.</returns>
    [LibraryImport(CoreVideo, EntryPoint = "CVPixelBufferGetBaseAddressOfPlane")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial byte* CVPixelBufferGetBaseAddressOfPlane(void* buffer, nint plane);

    /// <summary>Native <c>CVPixelBufferGetBytesPerRowOfPlane</c>.</summary>
    /// <param name="buffer">The pixel buffer.</param>
    /// <param name="plane">The plane.</param>
    /// <returns>The distance between rows of the plane in bytes.</returns>
    [LibraryImport(CoreVideo, EntryPoint = "CVPixelBufferGetBytesPerRowOfPlane")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial nint CVPixelBufferGetBytesPerRowOfPlane(void* buffer, nint plane);

    /// <summary>Native <c>CVPixelBufferGetHeightOfPlane</c>.</summary>
    /// <param name="buffer">The pixel buffer.</param>
    /// <param name="plane">The plane.</param>
    /// <returns>The rows of the plane.</returns>
    [LibraryImport(CoreVideo, EntryPoint = "CVPixelBufferGetHeightOfPlane")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial nint CVPixelBufferGetHeightOfPlane(void* buffer, nint plane);
}
