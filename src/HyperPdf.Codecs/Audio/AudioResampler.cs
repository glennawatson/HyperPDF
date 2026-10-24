// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdf.Codecs.Audio;

/// <summary>
/// Turns decoded sound of any layout and sample rate into stereo at one fixed rate. Channels are mixed first, then a bank of
/// windowed sinc filters is read at the exact fractional position of each output sample, with the two nearest filters blended, so
/// any ratio works without a table for each ratio. Each tap sum is a run of vector multiply and add steps, and sound that is
/// already at the output rate is only mixed.
/// </summary>
[DebuggerDisplay("AudioResampler: {_inputChannels} channels to stereo at {OutputRate} Hz")]
public sealed class AudioResampler
{
    /// <summary>The channels of the output.</summary>
    private const int StereoChannels = 2;

    /// <summary>How many times bigger the arrays become when they are full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The filters in the bank, not counting the extra one the blend reads past the last.</summary>
    private const int PhaseCount = 512;

    /// <summary>The bits of a fractional position that choose a filter: 2 to the power of this is <see cref="PhaseCount"/>.</summary>
    private const int PhaseBits = 9;

    /// <summary>The bits of a position that count the fraction of an input sample.</summary>
    private const int FractionBits = 32;

    /// <summary>The taps of each filter, which is the same for every ratio and a multiple of every vector size.</summary>
    private const int Taps = 64;

    /// <summary>The taps on each side of the centre of a filter.</summary>
    private const int HalfTaps = Taps / StereoChannels;

    /// <summary>The Kaiser window shape; higher values cut the ripple and widen the transition.</summary>
    private const double KaiserBeta = 9;

    /// <summary>The part of the output Nyquist rate the pass band reaches, which leaves room for the transition.</summary>
    private const double Rolloff = 0.94;

    /// <summary>The terms summed to find a modified Bessel function of order zero.</summary>
    private const int BesselTerms = 40;

    /// <summary>One quarter, the weight of the square in the Bessel series.</summary>
    private const double Quarter = 0.25;

    /// <summary>An argument of the sinc function so close to zero that its value is 1.</summary>
    private const double SincEpsilon = 1e-12;

    /// <summary>The fraction mask of a position.</summary>
    private const ulong FractionMask = (1UL << FractionBits) - 1;

    /// <summary>The bits of the position kept after the filter choice for the blend.</summary>
    private const int BlendBits = FractionBits - PhaseBits;

    /// <summary>The mask of the blend bits.</summary>
    private const ulong BlendMask = (1UL << BlendBits) - 1;

    /// <summary>The scale from the blend bits to a weight between 0 and 1.</summary>
    private const float BlendScale = 1F / (1UL << BlendBits);

    /// <summary>The number of channels in the input.</summary>
    private readonly int _inputChannels;

    /// <summary>How far the position moves for each output sample, as 32.32 fixed point input samples; zero when the sound is not resampled.</summary>
    private readonly ulong _step;

    /// <summary>The filter bank: <see cref="PhaseCount"/> plus one rows of <see cref="Taps"/> values; empty when the sound is not resampled.</summary>
    private readonly float[] _bank;

    /// <summary>The left channel, with the history the filters reach back into first.</summary>
    private float[] _left = [];

    /// <summary>The right channel, with the history the filters reach back into first.</summary>
    private float[] _right = [];

    /// <summary>How many samples of each channel are held.</summary>
    private int _filled;

    /// <summary>Where the next output sample is, in input samples from the start of the held samples, as 32.32 fixed point.</summary>
    private ulong _position;

    /// <summary>Initializes a new instance of the <see cref="AudioResampler"/> class.</summary>
    /// <param name="inputRate">The samples a second of the input.</param>
    /// <param name="inputChannels">The number of channels of the input.</param>
    /// <param name="outputRate">The samples a second of the output.</param>
    /// <exception cref="ArgumentOutOfRangeException">A rate or the channel count is below 1.</exception>
    public AudioResampler(int inputRate, int inputChannels, int outputRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(inputRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(inputChannels, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(outputRate, 1);
        _inputChannels = inputChannels;
        OutputRate = outputRate;
        _bank = inputRate == outputRate ? [] : BuildBank(Math.Min(1D, (double)outputRate / inputRate) * Rolloff);
        _step = inputRate == outputRate ? 0 : (ulong)Math.Round((double)inputRate / outputRate * (1UL << FractionBits));
        Reset();
    }

    /// <summary>Gets the samples a second of the output.</summary>
    public int OutputRate { get; }

    /// <summary>Forgets the held samples, for sound that starts again after a jump.</summary>
    public void Reset()
    {
        _filled = 0;
        _position = 0;
        if (_bank.Length == 0)
        {
            return;
        }

        // The filters reach back before the first sample, where the sound is silence.
        Ensure(HalfTaps);
        Array.Clear(_left, 0, HalfTaps);
        Array.Clear(_right, 0, HalfTaps);
        _filled = HalfTaps;
        _position = (ulong)HalfTaps << FractionBits;
    }

    /// <summary>Converts sound and appends the result to a buffer.</summary>
    /// <param name="input">The interleaved input samples; a whole number of frames.</param>
    /// <param name="output">Receives interleaved stereo samples at the output rate.</param>
    public void Process(ReadOnlySpan<float> input, PcmBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var frames = input.Length / _inputChannels;
        if (_bank.Length == 0)
        {
            MixInterleaved(input, frames, output);
            return;
        }

        Ensure(_filled + frames);
        ChannelMixer.Mix(input, _inputChannels, _left.AsSpan(_filled, frames), _right.AsSpan(_filled, frames));
        _filled += frames;
        Convert(output);
        Compact();
    }

    /// <summary>Builds the filter bank for a pass band.</summary>
    /// <param name="cutoff">The edge of the pass band as a fraction of the input sample rate; at most 1.</param>
    /// <returns>The rows of filters.</returns>
    private static float[] BuildBank(double cutoff)
    {
        var bank = new float[(PhaseCount + 1) * Taps];
        var scale = 1 / Bessel(KaiserBeta);
        for (var phase = 0; phase <= PhaseCount; phase++)
        {
            var row = bank.AsSpan(phase * Taps, Taps);
            var fraction = (double)phase / PhaseCount;
            var sum = 0D;
            for (var tap = 0; tap < Taps; tap++)
            {
                // The distance from the output position to the input sample this tap reads, in input samples.
                var distance = fraction + HalfTaps - 1 - tap;
                var value = cutoff * Sinc(cutoff * distance) * Kaiser(distance / HalfTaps, scale);
                row[tap] = (float)value;
                sum += value;
            }

            // Each filter passes a steady level unchanged.
            for (var tap = 0; tap < Taps; tap++)
            {
                row[tap] = (float)(row[tap] / sum);
            }
        }

        return bank;
    }

    /// <summary>The normalised sinc function.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The value of sin(pi x) over pi x.</returns>
    private static double Sinc(double x) => Math.Abs(x) < SincEpsilon ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);

    /// <summary>The Kaiser window.</summary>
    /// <param name="position">Where in the window, from -1 to 1.</param>
    /// <param name="scale">One over the Bessel function at the window shape.</param>
    /// <returns>The window value.</returns>
    private static double Kaiser(double position, double scale)
    {
        var inside = 1 - (position * position);
        return inside <= 0 ? 0 : Bessel(KaiserBeta * Math.Sqrt(inside)) * scale;
    }

    /// <summary>A modified Bessel function of order zero, found by summing its series.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>The value.</returns>
    private static double Bessel(double x)
    {
        var sum = 1D;
        var term = 1D;
        var square = x * x * Quarter;
        for (var n = 1; n <= BesselTerms; n++)
        {
            term *= square / ((double)n * n);
            sum += term;
        }

        return sum;
    }

    /// <summary>Adds up the products of a run of samples and one filter.</summary>
    /// <param name="samples">The samples, at least <see cref="Taps"/> long.</param>
    /// <param name="filter">The filter, <see cref="Taps"/> long.</param>
    /// <returns>The sum.</returns>
    private static float Dot(ReadOnlySpan<float> samples, ReadOnlySpan<float> filter)
    {
        var step = Vector<float>.Count;
        var total = Vector<float>.Zero;
        for (var tap = 0; tap < Taps; tap += step)
        {
            total += new Vector<float>(samples.Slice(tap, step)) * new Vector<float>(filter.Slice(tap, step));
        }

        return Vector.Sum(total);
    }

    /// <summary>Reads one output sample from two neighbouring filters.</summary>
    /// <param name="samples">The input run.</param>
    /// <param name="bank">The filter bank.</param>
    /// <param name="phase">The first filter.</param>
    /// <param name="blend">The weight of the second filter, between 0 and 1.</param>
    /// <returns>The sample.</returns>
    private static float Blend(ReadOnlySpan<float> samples, ReadOnlySpan<float> bank, int phase, float blend)
    {
        var low = Dot(samples, bank.Slice(phase * Taps, Taps));
        var high = Dot(samples, bank.Slice((phase + 1) * Taps, Taps));
        return low + ((high - low) * blend);
    }

    /// <summary>Mixes sound to stereo without changing its rate and appends it.</summary>
    /// <param name="input">The interleaved input samples.</param>
    /// <param name="frames">The number of frames.</param>
    /// <param name="output">Receives the stereo samples.</param>
    private void MixInterleaved(ReadOnlySpan<float> input, int frames, PcmBuffer output)
    {
        var count = frames * StereoChannels;
        if (_inputChannels == StereoChannels)
        {
            input[..count].CopyTo(output.Append(count));
            return;
        }

        Ensure(frames);
        ChannelMixer.Mix(input, _inputChannels, _left.AsSpan(0, frames), _right.AsSpan(0, frames));
        var samples = output.Append(count);
        for (var frame = 0; frame < frames; frame++)
        {
            samples[frame * StereoChannels] = _left[frame];
            samples[(frame * StereoChannels) + 1] = _right[frame];
        }
    }

    /// <summary>Makes sure both channel arrays hold a number of samples.</summary>
    /// <param name="count">The number of samples.</param>
    private void Ensure(int count)
    {
        if (_left.Length >= count)
        {
            return;
        }

        var size = Math.Max(count, _left.Length * GrowthFactor);
        Array.Resize(ref _left, size);
        Array.Resize(ref _right, size);
    }

    /// <summary>Produces every output sample the held input can give.</summary>
    /// <param name="output">Receives the stereo samples.</param>
    private void Convert(PcmBuffer output)
    {
        while (true)
        {
            // The filter reads Taps samples starting this far before the position.
            var first = (int)(_position >> FractionBits) - HalfTaps + 1;
            if (first + Taps > _filled)
            {
                return;
            }

            var fraction = _position & FractionMask;
            var phase = (int)(fraction >> BlendBits);
            var blend = (fraction & BlendMask) * BlendScale;
            var pair = output.Append(StereoChannels);
            pair[0] = Blend(_left.AsSpan(first, Taps), _bank, phase, blend);
            pair[1] = Blend(_right.AsSpan(first, Taps), _bank, phase, blend);
            _position += _step;
        }
    }

    /// <summary>Drops the held samples the filters will not read again.</summary>
    private void Compact()
    {
        var drop = (int)(_position >> FractionBits) - HalfTaps + 1;
        if (drop <= 0)
        {
            return;
        }

        var keep = _filled - drop;
        _left.AsSpan(drop, keep).CopyTo(_left);
        _right.AsSpan(drop, keep).CopyTo(_right);
        _filled = keep;
        _position -= (ulong)drop << FractionBits;
    }
}
