// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>Builds the CoreFoundation dictionary that asks VideoToolbox for pictures in the NV12 layout, with a luma plane and one plane of interleaved colour.</summary>
internal static unsafe class PixelBufferRequest
{
    /// <summary>The four character code <c>420v</c>: NV12 with video range samples (<c>kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange</c>).</summary>
    internal const uint VideoRangeNv12 = 0x3432_3076;

    /// <summary>The four character code <c>420f</c>: NV12 with full range samples (<c>kCVPixelFormatType_420YpCbCr8BiPlanarFullRange</c>).</summary>
    internal const uint FullRangeNv12 = 0x3432_3066;

    /// <summary>The kind of number that is a 32 bit signed integer (<c>kCFNumberSInt32Type</c>).</summary>
    private const int SignedInt32 = 3;

    /// <summary>The number of pairs in the dictionary.</summary>
    private const int PairCount = 1;

    /// <summary>The name of the exported key that names the pixel layout.</summary>
    private const string PixelFormatKeyName = "kCVPixelBufferPixelFormatTypeKey";

    /// <summary>The name of the exported table that tells dictionaries how to treat keys.</summary>
    private const string KeyCallbacksName = "kCFTypeDictionaryKeyCallBacks";

    /// <summary>The name of the exported table that tells dictionaries how to treat values.</summary>
    private const string ValueCallbacksName = "kCFTypeDictionaryValueCallBacks";

    /// <summary>Makes the request.</summary>
    /// <returns>The dictionary, or <see langword="null"/> when CoreFoundation or CoreVideo do not export what the request needs.</returns>
    internal static CoreFoundationHandle? Create()
    {
        if (!NativeLibrary.TryLoad(NativeMethods.CoreVideo, out var video)
            || !NativeLibrary.TryLoad(NativeMethods.CoreFoundation, out var foundation)
            || !NativeLibrary.TryGetExport(video, PixelFormatKeyName, out var keyAddress)
            || !NativeLibrary.TryGetExport(foundation, KeyCallbacksName, out var keyCallbacks)
            || !NativeLibrary.TryGetExport(foundation, ValueCallbacksName, out var valueCallbacks))
        {
            return null;
        }

        var format = stackalloc int[1];
        format[0] = (int)VideoRangeNv12;
        var number = NativeMethods.CFNumberCreate(null, SignedInt32, format);
        if (number is null)
        {
            return null;
        }

        using CoreFoundationHandle numberHandle = new(number);
        var key = *(void**)keyAddress;
        var dictionary = NativeMethods.CFDictionaryCreate(null, &key, &number, PairCount, (void*)keyCallbacks, (void*)valueCallbacks);
        return dictionary is null ? null : new CoreFoundationHandle(dictionary);
    }
}
