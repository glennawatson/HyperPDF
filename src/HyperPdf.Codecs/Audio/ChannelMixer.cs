// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Audio;

/// <summary>
/// Mixes interleaved sound of any number of channels down to a left and a right channel. Channels follow the usual file order: left,
/// right, centre, low frequency, then the surrounds. The centre and the surrounds are mixed in at -3 dB so the total level stays
/// the same, and the low frequency channel is left out because the output has no speaker for it.
/// </summary>
public static class ChannelMixer
{
    /// <summary>The weight of a channel that is shared between both sides, which is -3 dB.</summary>
    private const float SharedWeight = 0.70710678F;

    /// <summary>The number of channels of mono sound.</summary>
    private const int Mono = 1;

    /// <summary>The number of channels of stereo sound.</summary>
    private const int Stereo = 2;

    /// <summary>The number of channels of sound with a centre channel and no surrounds.</summary>
    private const int ThreeChannels = 3;

    /// <summary>The number of channels of quadraphonic sound.</summary>
    private const int FourChannels = 4;

    /// <summary>The index of the right channel.</summary>
    private const int Right = 1;

    /// <summary>The index of the centre channel.</summary>
    private const int Centre = 2;

    /// <summary>The index of the first channel after the centre in a layout without a low frequency channel.</summary>
    private const int FirstSurroundWithoutLowFrequency = 2;

    /// <summary>The index of the left surround in a layout with a low frequency channel.</summary>
    private const int LeftSurround = 4;

    /// <summary>The index of the right surround in a layout with a low frequency channel.</summary>
    private const int RightSurround = 5;

    /// <summary>The fewest channels of a layout with a low frequency channel and surrounds.</summary>
    private const int SurroundLayout = 6;

    /// <summary>Mixes sound to two channels.</summary>
    /// <param name="input">The interleaved samples; a whole number of frames.</param>
    /// <param name="channels">The number of channels in <paramref name="input"/>.</param>
    /// <param name="left">Receives the left channel, one sample for each frame.</param>
    /// <param name="right">Receives the right channel, one sample for each frame.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="channels"/> is below 1.</exception>
    public static void Mix(ReadOnlySpan<float> input, int channels, Span<float> left, Span<float> right)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, Mono);
        var frames = input.Length / channels;
        switch (channels)
        {
            case Mono:
            {
                for (var frame = 0; frame < frames; frame++)
                {
                    left[frame] = input[frame];
                    right[frame] = input[frame];
                }

                break;
            }

            case Stereo:
            {
                for (var frame = 0; frame < frames; frame++)
                {
                    left[frame] = input[frame * Stereo];
                    right[frame] = input[(frame * Stereo) + Right];
                }

                break;
            }

            default:
            {
                MixMany(input, channels, frames, left, right);
                break;
            }
        }
    }

    /// <summary>Mixes sound of more than two channels.</summary>
    /// <param name="input">The interleaved samples.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="frames">The number of frames.</param>
    /// <param name="left">Receives the left channel.</param>
    /// <param name="right">Receives the right channel.</param>
    private static void MixMany(ReadOnlySpan<float> input, int channels, int frames, Span<float> left, Span<float> right)
    {
        var centre = channels >= ThreeChannels ? Centre : -1;
        var (leftSurround, rightSurround) = SurroundsOf(channels);
        for (var frame = 0; frame < frames; frame++)
        {
            var at = frame * channels;
            var sum = 0F;
            var leftSide = input[at];
            var rightSide = input[at + Right];
            if (centre >= 0)
            {
                sum = input[at + centre] * SharedWeight;
            }

            if (leftSurround >= 0)
            {
                leftSide += input[at + leftSurround] * SharedWeight;
                rightSide += input[at + rightSurround] * SharedWeight;
            }

            left[frame] = leftSide + sum;
            right[frame] = rightSide + sum;
        }
    }

    /// <summary>Finds where the surround channels are in a layout.</summary>
    /// <param name="channels">The number of channels.</param>
    /// <returns>The indexes of the left and right surrounds, or -1 for both when the layout has none.</returns>
    private static SurroundPlaces SurroundsOf(int channels) =>
        channels switch
        {
            FourChannels => new(FirstSurroundWithoutLowFrequency, FirstSurroundWithoutLowFrequency + Right),
            >= SurroundLayout => new(LeftSurround, RightSurround),
            _ => new(-1, -1),
        };

    /// <summary>Where the surround channels are in a layout.</summary>
    /// <param name="Left">The index of the left surround, or -1.</param>
    /// <param name="Right">The index of the right surround, or -1.</param>
    private readonly record struct SurroundPlaces(int Left, int Right);
}
