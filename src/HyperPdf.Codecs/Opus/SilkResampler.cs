// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Brings the 8, 12 or 16 kHz output of the SILK layer up to 48 kHz: first an upsampler of two all-pass chains doubles the
/// rate, then a polyphase filter with twelve phases interpolates to the final rate.
/// </summary>
internal sealed class SilkResampler
{
    /// <summary>The rate the decoder resamples to, in kHz.</summary>
    internal const int OutputKhz = 48;

    /// <summary>The milliseconds of input one batch holds.</summary>
    private const int BatchMs = 10;

    /// <summary>The taps of the polyphase filter.</summary>
    private const int FilterTaps = 8;

    /// <summary>The phases of the polyphase filter.</summary>
    private const int Phases = 12;

    /// <summary>The taps in each half of a phase of the filter.</summary>
    private const int TapsPerHalf = 4;

    /// <summary>The state of the upsampler's sections.</summary>
    private const int UpsamplerStates = 6;

    /// <summary>The sections in each all-pass chain.</summary>
    private const int ChainLength = 3;

    /// <summary>The most samples an input batch holds.</summary>
    private const int MaxBatch = BatchMs * SilkConstants.WidebandKhz;

    /// <summary>The bits the samples are scaled up by inside the upsampler.</summary>
    private const int InternalBits = 10;

    /// <summary>The fractional bits of the position in the interpolator.</summary>
    private const int PositionBits = 16;

    /// <summary>The mask of the fraction of a position.</summary>
    private const int PositionMask = 0xFFFF;

    /// <summary>The bits of the interpolated sample before it is rounded to 16 bits.</summary>
    private const int SampleBits = 15;

    /// <summary>The bits of the ratio between the rates before the upsampler doubles it.</summary>
    private const int RatioBits = 15;

    /// <summary>The bits the ratio is shifted up by to give it the scale of a position.</summary>
    private const int RatioShift = 2;

    /// <summary>The output rate in Hz.</summary>
    private const int OutputHz = OutputKhz * SilkConstants.HertzPerKilohertz;

    /// <summary>The filter state of the two all-pass chains of the upsampler.</summary>
    private readonly int[] _upsamplerState = new int[UpsamplerStates];

    /// <summary>The last samples of the filtered signal, which the interpolator needs in front of the next batch.</summary>
    private readonly short[] _filterState = new short[FilterTaps];

    /// <summary>The samples held back to delay the input.</summary>
    private readonly short[] _delayBuffer = new short[SilkConstants.WidebandKhz];

    /// <summary>The upsampled batch with the filter state in front.</summary>
    private readonly short[] _batch = new short[(SilkConstants.Pair * MaxBatch) + FilterTaps];

    /// <summary>The samples of delay in front of the input.</summary>
    private readonly int _inputDelay;

    /// <summary>The samples of input in a batch.</summary>
    private readonly int _batchSize;

    /// <summary>The input samples it takes to make one output sample, in Q16 of the upsampled signal.</summary>
    private readonly int _inverseRatio;

    /// <summary>Initializes a new instance of the <see cref="SilkResampler"/> class.</summary>
    /// <param name="inputKhz">The input rate in kHz: 8, 12 or 16.</param>
    internal SilkResampler(int inputKhz)
    {
        InputKhz = inputKhz;
        _inputDelay = InputDelays[SilkConstants.RateIndex(inputKhz)];
        _batchSize = inputKhz * BatchMs;
        var inputHz = inputKhz * SilkConstants.HertzPerKilohertz;
        _inverseRatio = ((inputHz << RatioBits) / OutputHz) << RatioShift;
        while (SilkMath.MultiplyWord(_inverseRatio, OutputHz) < (inputHz << 1))
        {
            _inverseRatio++;
        }
    }

    /// <summary>Gets the input rate in kHz.</summary>
    internal int InputKhz { get; }

    /// <summary>Gets the samples of delay the decoder adds to each input rate to equalise the delay of the different rates, for 8, 12 and 16 kHz.</summary>
    private static ReadOnlySpan<byte> InputDelays => [0x00, 0x04, 0x07];

    /// <summary>Resamples a stretch of signal; there must be at least a millisecond of input.</summary>
    /// <param name="output">Receives the 48 kHz samples: <c>input.Length * 48 / rate</c> of them.</param>
    /// <param name="input">The signal.</param>
    internal void Resample(Span<short> output, ReadOnlySpan<short> input)
    {
        var count = InputKhz - _inputDelay;
        input[..count].CopyTo(_delayBuffer.AsSpan(_inputDelay));
        var written = UpsampleBatches(output, _delayBuffer.AsSpan(0, InputKhz));
        _ = UpsampleBatches(output[written..], input.Slice(count, input.Length - InputKhz));
        input.Slice(input.Length - _inputDelay, _inputDelay).CopyTo(_delayBuffer);
    }

    /// <summary>Clears the filters back to silence.</summary>
    internal void Reset()
    {
        Array.Clear(_upsamplerState);
        Array.Clear(_filterState);
        Array.Clear(_delayBuffer);
    }

    /// <summary>Copies the state of another resampler of the same rate.</summary>
    /// <param name="other">The resampler to copy.</param>
    internal void CopyFrom(SilkResampler other)
    {
        other._upsamplerState.CopyTo(_upsamplerState, 0);
        other._filterState.CopyTo(_filterState, 0);
        other._delayBuffer.CopyTo(_delayBuffer, 0);
    }

    /// <summary>Upsamples the signal by two with a pair of all-pass chains, one for the even and one for the odd output samples.</summary>
    /// <param name="state">The six filter states.</param>
    /// <param name="output">Receives two samples for each input sample.</param>
    /// <param name="input">The signal.</param>
    private static void UpsampleTwice(Span<int> state, Span<short> output, ReadOnlySpan<short> input)
    {
        var even = SilkResamplerTables.UpsampleEven;
        var odd = SilkResamplerTables.UpsampleOdd;
        for (var k = 0; k < input.Length; k++)
        {
            var value = input[k] << InternalBits;
            output[SilkConstants.Pair * k] = (short)AllPass(state, 0, even, value);
            output[(SilkConstants.Pair * k) + 1] = (short)AllPass(state, ChainLength, odd, value);
        }
    }

    /// <summary>Runs a sample through three all-pass sections.</summary>
    /// <param name="state">The filter states.</param>
    /// <param name="first">The index of the first of the three states used.</param>
    /// <param name="coefficients">The three coefficients.</param>
    /// <param name="value">The sample, scaled up by 10 bits.</param>
    /// <returns>The output sample, saturated to 16 bits.</returns>
    private static int AllPass(Span<int> state, int first, ReadOnlySpan<short> coefficients, int value)
    {
        var y = value - state[first];
        var x = SilkMath.MultiplyWordBottom(y, coefficients[0]);
        var one = state[first] + x;
        state[first] = value + x;
        y = one - state[first + 1];
        x = SilkMath.MultiplyWordBottom(y, coefficients[1]);
        var two = state[first + 1] + x;
        state[first + 1] = one + x;
        y = two - state[first + SilkConstants.Pair];
        x = SilkMath.MultiplyAddWordBottom(y, y, coefficients[SilkConstants.Pair]);
        var three = state[first + SilkConstants.Pair] + x;
        state[first + SilkConstants.Pair] = two + x;
        return SilkMath.Saturate16(SilkMath.RoundShift(three, InternalBits));
    }

    /// <summary>Interpolates the upsampled signal at the positions of the output samples.</summary>
    /// <param name="output">Receives the samples.</param>
    /// <param name="batch">The upsampled signal, with the filter state in front.</param>
    /// <param name="maxIndex">The position in Q16 at which to stop.</param>
    /// <param name="increment">The step between output positions in Q16.</param>
    /// <returns>The samples written.</returns>
    private static int Interpolate(Span<short> output, ReadOnlySpan<short> batch, int maxIndex, int increment)
    {
        var taps = SilkResamplerTables.InterpolationTaps;
        var written = 0;
        for (var index = 0; index < maxIndex; index += increment)
        {
            var phase = SilkMath.MultiplyWordBottom(index & PositionMask, Phases);
            var window = batch.Slice(index >> PositionBits, FilterTaps);
            var front = phase * TapsPerHalf;
            var mirrored = (Phases - 1 - phase) * TapsPerHalf;
            var sum = SilkMath.MultiplyBottoms(window[0], taps[front]);
            for (var t = 1; t < TapsPerHalf; t++)
            {
                sum = SilkMath.MultiplyAddBottoms(sum, window[t], taps[front + t]);
            }

            for (var t = 0; t < TapsPerHalf; t++)
            {
                sum = SilkMath.MultiplyAddBottoms(sum, window[TapsPerHalf + t], taps[mirrored + TapsPerHalf - 1 - t]);
            }

            output[written] = (short)SilkMath.Saturate16(SilkMath.RoundShift(sum, SampleBits));
            written++;
        }

        return written;
    }

    /// <summary>Upsamples a stretch of signal batch by batch.</summary>
    /// <param name="output">Receives the samples.</param>
    /// <param name="input">The signal.</param>
    /// <returns>The samples written.</returns>
    private int UpsampleBatches(Span<short> output, ReadOnlySpan<short> input)
    {
        _filterState.CopyTo(_batch, 0);
        var written = 0;
        var remaining = input.Length;
        var position = 0;
        int count;
        while (true)
        {
            count = Math.Min(remaining, _batchSize);
            UpsampleTwice(_upsamplerState, _batch.AsSpan(FilterTaps), input.Slice(position, count));
            written += Interpolate(output[written..], _batch, count << (PositionBits + 1), _inverseRatio);
            position += count;
            remaining -= count;
            if (remaining <= 0)
            {
                break;
            }

            _batch.AsSpan(count << 1, FilterTaps).CopyTo(_batch);
        }

        _batch.AsSpan(count << 1, FilterTaps).CopyTo(_filterState);
        return written;
    }
}
