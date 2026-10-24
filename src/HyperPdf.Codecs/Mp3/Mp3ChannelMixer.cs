// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp3;

/// <summary>Adapts the channels of a frame to the channels of the track when a stream changes between mono and stereo.</summary>
internal static class Mp3ChannelMixer
{
    /// <summary>One half, which averages two channels.</summary>
    private const float Half = 0.5F;

    /// <summary>Writes one sample of every channel.</summary>
    /// <param name="source">The frame's channels.</param>
    /// <param name="target">The output channels.</param>
    internal static void WriteSample(ReadOnlySpan<float> source, Span<float> target)
    {
        if (source.Length == target.Length)
        {
            source.CopyTo(target);
        }
        else if (source.Length == 1)
        {
            target.Fill(source[0]);
        }
        else
        {
            target[0] = (source[0] + source[1]) * Half;
        }
    }
}
