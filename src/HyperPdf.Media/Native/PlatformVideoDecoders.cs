// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs;
using HyperPdf.Media.Native.MediaFoundation;
using HyperPdf.Media.Native.VaApi;
using HyperPdf.Media.Native.VideoToolbox;

namespace HyperPdf.Media.Native;

/// <summary>Makes the decoder for a picture track: the one the operating system provides when it has one for the coding, otherwise the one written in C#.</summary>
public static class PlatformVideoDecoders
{
    /// <summary>The pixels a picture needs before the system's decoder is tried first, because hardware pays about 2 ms for each picture (measured with VA-API).</summary>
    private const int MinimumSystemPixels = 640 * 360;

    /// <summary>Makes a decoder.</summary>
    /// <param name="track">The picture track.</param>
    /// <returns>The decoder, or <see langword="null"/> when no decoder reads the track's coding.</returns>
    public static VideoDecoder? Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return IsLargeEnough(track) ? TryCreateNative(track, out _) ?? VideoDecoders.Create(track) : VideoDecoders.Create(track) ?? TryCreateNative(track, out _);
    }

    /// <summary>Makes the operating system's decoder for a track.</summary>
    /// <param name="track">The picture track.</param>
    /// <param name="reason">Receives why there is no decoder, when there is none.</param>
    /// <returns>The decoder, or <see langword="null"/> when this system has none for the coding.</returns>
    internal static VideoDecoder? TryCreateNative(MediaTrack track, out string? reason)
    {
        reason = "This system has no operating system decoder.";
        if (OperatingSystem.IsWindows())
        {
            var windows = MediaFoundationDecoder.TryCreate(track, out var windowsFailure);
            reason = windows is null ? windowsFailure.ToString() : null;
            return windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return TryCreateLinux(track, out reason);
        }

        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var mac = VideoToolboxDecoder.TryCreate(track, out var macFailure);
        reason = mac is null ? macFailure.ToString() : null;
        return mac;
    }

    /// <summary>Finds whether a track's pictures are big enough for the system's decoder to be the first choice.</summary>
    /// <param name="track">The picture track.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool IsLargeEnough(MediaTrack track) => (long)track.Width * track.Height >= MinimumSystemPixels;

    /// <summary>Makes the graphics hardware's decoder for a track on Linux, through VA-API.</summary>
    /// <param name="track">The picture track.</param>
    /// <param name="reason">Receives why there is no decoder, when there is none.</param>
    /// <returns>The decoder, or <see langword="null"/>.</returns>
    private static VideoDecoder? TryCreateLinux(MediaTrack track, out string? reason)
    {
        DecoderFailure failure;
        VideoDecoder? decoder = track.Codec == MediaCodec.H265 ? VaApiH265Decoder.TryCreate(track, out failure) : VaApiH264Decoder.TryCreate(track, out failure);
        reason = decoder is null ? failure.ToString() : null;
        return decoder;
    }
}
