// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Dsp;

namespace HyperPdf.Codecs.Aac;

/// <summary>
/// The synthesis filterbank of one decoder: an inverse MDCT of each window, a window function, and the addition of the overlap from
/// the frame before. The transforms and the working buffers belong to the decoder, which uses them for one channel at a time.
/// </summary>
[DebuggerDisplay("AacFilterbank")]
internal sealed class AacFilterbank
{
    /// <summary>The samples in the transform output of a long window.</summary>
    private const int LongWindow = AacChannel.LongSize << 1;

    /// <summary>The samples of flat window before and after the short part of a start or stop window.</summary>
    private const int FlatSize = (AacChannel.LongSize - AacChannel.ShortSize) / 2;

    /// <summary>The window sequence that goes from a long window to short ones.</summary>
    private const int LongStart = 1;

    /// <summary>The window sequence that goes from short windows to a long one.</summary>
    private const int LongStop = 3;

    /// <summary>The transform of a long window.</summary>
    private readonly InverseMdct _long = new(AacChannel.LongSize);

    /// <summary>The transform of a short window.</summary>
    private readonly InverseMdct _short = new(AacChannel.ShortSize);

    /// <summary>The output of the transform.</summary>
    private readonly float[] _transformed = new float[LongWindow];

    /// <summary>The frame being built from the windows.</summary>
    private readonly float[] _frame = new float[LongWindow];

    /// <summary>Turns the spectrum of a channel into the samples of the frame and keeps the overlap for the next.</summary>
    /// <param name="channel">The channel, with its spectrum filled.</param>
    /// <param name="output">Receives 1024 samples.</param>
    internal void Run(AacChannel channel, Span<float> output)
    {
        if (channel.IsShort)
        {
            Array.Clear(_frame);
            for (var window = 0; window < AacChannel.ShortCount; window++)
            {
                _short.Inverse(channel.Spectrum.AsSpan(window * AacChannel.ShortSize, AacChannel.ShortSize), _transformed);
                AddShortWindow(channel, window);
            }
        }
        else
        {
            _long.Inverse(channel.Spectrum, _transformed);
            WindowLong(channel);
        }

        for (var index = 0; index < AacChannel.LongSize; index++)
        {
            output[index] = channel.Overlap[index] + _frame[index];
        }

        _frame.AsSpan(AacChannel.LongSize).CopyTo(channel.Overlap);
        channel.PreviousShape = channel.WindowShape;
    }

    /// <summary>Gets the first half of the window at a place.</summary>
    /// <param name="sequence">The window sequence.</param>
    /// <param name="index">The place in the half.</param>
    /// <param name="longRise">The rising half of the long window of the previous frame's shape.</param>
    /// <param name="shortRise">The rising half of the short window of the previous frame's shape.</param>
    /// <returns>The multiplier.</returns>
    private static float Rising(int sequence, int index, float[] longRise, float[] shortRise)
    {
        if (sequence != LongStop)
        {
            return longRise[index];
        }

        if (index < FlatSize)
        {
            return 0F;
        }

        var shortIndex = index - FlatSize;
        return shortIndex < AacChannel.ShortSize ? shortRise[shortIndex] : 1F;
    }

    /// <summary>Gets the second half of the window at a place.</summary>
    /// <param name="sequence">The window sequence.</param>
    /// <param name="index">The place in the half.</param>
    /// <param name="longRise">The rising half of the long window of this frame's shape.</param>
    /// <param name="shortRise">The rising half of the short window of this frame's shape.</param>
    /// <returns>The multiplier.</returns>
    private static float Falling(int sequence, int index, float[] longRise, float[] shortRise)
    {
        if (sequence != LongStart)
        {
            return longRise[AacChannel.LongSize - 1 - index];
        }

        if (index < FlatSize)
        {
            return 1F;
        }

        var shortIndex = index - FlatSize;
        return shortIndex < AacChannel.ShortSize ? shortRise[AacChannel.ShortSize - 1 - shortIndex] : 0F;
    }

    /// <summary>Windows the transform of a long, start or stop window into the frame.</summary>
    /// <param name="channel">The channel.</param>
    private void WindowLong(AacChannel channel)
    {
        var previous = AacWindows.Long(channel.PreviousShape);
        var current = AacWindows.Long(channel.WindowShape);
        var shortPrevious = AacWindows.Short(channel.PreviousShape);
        var shortCurrent = AacWindows.Short(channel.WindowShape);
        var sequence = channel.WindowSequence;
        for (var index = 0; index < AacChannel.LongSize; index++)
        {
            var offset = index + AacChannel.LongSize;
            _frame[index] = _transformed[index] * Rising(sequence, index, previous, shortPrevious);
            _frame[offset] = _transformed[offset] * Falling(sequence, index, current, shortCurrent);
        }
    }

    /// <summary>Windows the transform of one short window and adds it into the frame.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="window">The number of the short window.</param>
    private void AddShortWindow(AacChannel channel, int window)
    {
        var rising = AacWindows.Short(window == 0 ? channel.PreviousShape : channel.WindowShape);
        var falling = AacWindows.Short(channel.WindowShape);
        var start = FlatSize + (window * AacChannel.ShortSize);
        for (var index = 0; index < AacChannel.ShortSize; index++)
        {
            _frame[start + index] += _transformed[index] * rising[index];
            _frame[start + AacChannel.ShortSize + index] += _transformed[AacChannel.ShortSize + index] * falling[AacChannel.ShortSize - 1 - index];
        }
    }
}
