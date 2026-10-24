// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>The identifiers Media Foundation names media types, attributes and kinds of transform with.</summary>
internal static class MediaFoundationIds
{
    /// <summary>The kind of transform that decodes video, <c>MFT_CATEGORY_VIDEO_DECODER</c>.</summary>
    internal static readonly Guid VideoDecoderCategory = new("d6c02d4b-6833-45b4-971a-05a4b04bab91");

    /// <summary>The interface id of <c>IMFTransform</c>.</summary>
    internal static readonly Guid TransformInterface = new("bf94c121-5b05-4e6f-8000-ba598961414d");

    /// <summary>The attribute holding a media type's major type, <c>MF_MT_MAJOR_TYPE</c>.</summary>
    internal static readonly Guid MajorTypeKey = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");

    /// <summary>The attribute holding a media type's subtype, <c>MF_MT_SUBTYPE</c>.</summary>
    internal static readonly Guid SubtypeKey = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

    /// <summary>The attribute holding a picture's width and height, <c>MF_MT_FRAME_SIZE</c>.</summary>
    internal static readonly Guid FrameSizeKey = new("1652c33d-d6b2-4012-b834-72030849a37d");

    /// <summary>The attribute saying how pictures are interlaced, <c>MF_MT_INTERLACE_MODE</c>.</summary>
    internal static readonly Guid InterlaceModeKey = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");

    /// <summary>The attribute holding the distance between rows, <c>MF_MT_DEFAULT_STRIDE</c>.</summary>
    internal static readonly Guid DefaultStrideKey = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");

    /// <summary>The major type of video, <c>MFMediaType_Video</c>.</summary>
    internal static readonly Guid VideoType = new("73646976-0000-0010-8000-00aa00389b71");

    /// <summary>The subtype of H.264 video, <c>MFVideoFormat_H264</c>.</summary>
    internal static readonly Guid H264Subtype = new("34363248-0000-0010-8000-00aa00389b71");

    /// <summary>The subtype of H.265 video, <c>MFVideoFormat_HEVC</c>.</summary>
    internal static readonly Guid HevcSubtype = new("43564548-0000-0010-8000-00aa00389b71");

    /// <summary>The subtype of pictures with a luma plane followed by interleaved colour planes, <c>MFVideoFormat_NV12</c>.</summary>
    internal static readonly Guid Nv12Subtype = new("3231564e-0000-0010-8000-00aa00389b71");
}
