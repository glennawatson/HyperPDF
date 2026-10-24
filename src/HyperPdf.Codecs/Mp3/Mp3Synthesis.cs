// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Mp3;

/// <summary>
/// The polyphase synthesis filterbank, which turns the 32 subband samples of each time step back into 32 samples of sound. Each step is
/// matrixed into 64 values that are pushed on a ring of the last 16 steps, and the output is the window applied to the right values of the ring.
/// </summary>
internal sealed class Mp3Synthesis
{
    /// <summary>The time steps the ring holds.</summary>
    private const int RingSteps = 16;

    /// <summary>The values in one step of the ring.</summary>
    private const int StepLength = Mp3Tables.MatrixOutputs;

    /// <summary>The samples one time step produces.</summary>
    private const int StepSamples = Mp3Tables.Subbands;

    /// <summary>The pairs of ring steps the window is applied to.</summary>
    private const int WindowPairs = RingSteps / 2;

    /// <summary>The ring of matrixed values, newest step first.</summary>
    private readonly float[] _ring = new float[RingSteps * StepLength];

    /// <summary>The index of the newest step in the ring.</summary>
    private int _head;

    /// <summary>Clears the filter, as at the start of a stream or after a seek.</summary>
    internal void Reset()
    {
        Array.Clear(_ring);
        _head = 0;
    }

    /// <summary>Turns a granule of subband samples into sound.</summary>
    /// <param name="slots">The subband samples: 32 for each of the 18 time steps.</param>
    /// <param name="output">Receives the samples.</param>
    /// <param name="offset">The index of the first sample in the output.</param>
    /// <param name="stride">The distance between the samples of this channel in the output, which is the number of channels.</param>
    internal void Run(ReadOnlySpan<float> slots, Span<float> output, int offset, int stride)
    {
        Span<float> samples = stackalloc float[StepSamples];
        for (var step = 0; step < Mp3Tables.SubbandSamples; step++)
        {
            Push(slots.Slice(step * Mp3Tables.Subbands, Mp3Tables.Subbands));
            Window(samples);
            var target = offset + (step * StepSamples * stride);
            for (var index = 0; index < StepSamples; index++)
            {
                output[target + (index * stride)] = samples[index];
            }
        }
    }

    /// <summary>Matrixes one time step of subband samples and pushes it on the ring.</summary>
    /// <param name="subbands">The 32 subband samples.</param>
    private void Push(ReadOnlySpan<float> subbands)
    {
        _head = (_head + RingSteps - 1) & (RingSteps - 1);
        var target = _ring.AsSpan(_head * StepLength, StepLength);
        target.Clear();
        var matrix = Mp3Tables.SynthesisMatrix.Span;
        for (var subband = 0; subband < subbands.Length; subband++)
        {
            var value = subbands[subband];
            if (value == 0F)
            {
                continue;
            }

            var scale = new Vector<float>(value);
            var row = matrix.Slice(subband * StepLength, StepLength);
            for (var index = 0; index < StepLength; index += Vector<float>.Count)
            {
                var sum = new Vector<float>(target[index..]) + (new Vector<float>(row[index..]) * scale);
                sum.CopyTo(target[index..]);
            }
        }
    }

    /// <summary>Applies the window to the ring.</summary>
    /// <param name="samples">Receives 32 samples.</param>
    private void Window(Span<float> samples)
    {
        samples.Clear();
        var window = Mp3Tables.SynthesisWindow.Span;
        for (var pair = 0; pair < WindowPairs; pair++)
        {
            var first = pair << 1;
            var even = _ring.AsSpan(((_head + first) & (RingSteps - 1)) * StepLength, StepLength);
            var odd = _ring.AsSpan(((_head + first + 1) & (RingSteps - 1)) * StepLength, StepLength);
            var evenWindow = window.Slice(first * StepSamples, StepSamples);
            var oddWindow = window.Slice((first + 1) * StepSamples, StepSamples);
            for (var index = 0; index < StepSamples; index += Vector<float>.Count)
            {
                var sum = new Vector<float>(samples[index..])
                    + (new Vector<float>(even[index..]) * new Vector<float>(evenWindow[index..]))
                    + (new Vector<float>(odd[(StepSamples + index)..]) * new Vector<float>(oddWindow[index..]));
                sum.CopyTo(samples[index..]);
            }
        }
    }
}
