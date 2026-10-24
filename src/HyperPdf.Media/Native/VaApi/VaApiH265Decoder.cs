// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs;
using HyperPdf.Codecs.H265;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>Decodes H.265 pictures on the graphics hardware through VA-API, by running the C# decoder's header reading and picture buffer logic with the hardware doing the reconstruction.</summary>
[DebuggerDisplay("VaApiH265Decoder: {Width}x{Height}")]
internal sealed class VaApiH265Decoder : VideoDecoder
{
    /// <summary>The C# decoder, set up to hand picture reconstruction to the hardware.</summary>
    private readonly H265Decoder _inner;

    /// <summary>The hardware.</summary>
    private readonly VaApiH265Hardware _hardware;

    /// <summary>Initializes a new instance of the <see cref="VaApiH265Decoder"/> class.</summary>
    /// <param name="inner">The C# decoder.</param>
    /// <param name="hardware">The hardware.</param>
    private VaApiH265Decoder(H265Decoder inner, VaApiH265Hardware hardware)
        : base(inner.Width, inner.Height)
    {
        _inner = inner;
        _hardware = hardware;
    }

    /// <inheritdoc/>
    public override void Decode(MediaPacket packet, List<VideoFrame> output) => _inner.Decode(packet, output);

    /// <inheritdoc/>
    public override void Flush(List<VideoFrame> output) => _inner.Flush(output);

    /// <inheritdoc/>
    public override void Reset() => _inner.Reset();

    /// <summary>Makes a decoder for a track.</summary>
    /// <param name="track">The H.265 track.</param>
    /// <param name="failure">Receives why no decoder was made, when none was.</param>
    /// <returns>The decoder, or <see langword="null"/> when this system's graphics hardware cannot decode the track.</returns>
    internal static VaApiH265Decoder? TryCreate(MediaTrack track, out DecoderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(track);
        var sps = H265Decoder.ReadFirstSps(track);
        if (sps is null)
        {
            failure = new("The H.265 configuration has no sequence parameter set", 0);
            return null;
        }

        var hardware = VaApiH265Hardware.TryOpen(sps, out failure);
        if (hardware is null)
        {
            return null;
        }

        try
        {
            return new(H265Decoder.CreateForHardware(track, hardware), hardware);
        }
        catch (NotSupportedException exception)
        {
            hardware.Dispose();
            failure = new(exception.Message, 0);
            return null;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _inner.Dispose();
        _hardware.Dispose();
    }
}
