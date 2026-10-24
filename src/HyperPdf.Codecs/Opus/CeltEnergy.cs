// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Reads the energy of each CELT band (RFC 6716 section 4.3.2): a coarse value in whole steps of 6 dB, predicted from the
/// last frame and the band before it, then finer bits that fill in the gaps between those steps.
/// </summary>
internal static class CeltEnergy
{
    /// <summary>The bands in the 48 kHz mode.</summary>
    internal const int Bands = 21;

    /// <summary>The most fine energy bits a band takes.</summary>
    internal const int MaxFineBits = 8;

    /// <summary>The lowest energy a band is predicted from, in steps of 6 dB.</summary>
    private const float PredictionFloor = -9F;

    /// <summary>The bits the coarse energy of a band needs at least before a full Laplace symbol is read.</summary>
    private const int LaplaceBits = 15;

    /// <summary>The bits needed to read the three way small energy symbol.</summary>
    private const int SmallBits = 2;

    /// <summary>The bits of the total of the Laplace distribution.</summary>
    private const int LaplaceTotalBits = 15;

    /// <summary>The smallest probability the Laplace distribution gives any value, out of 32768.</summary>
    private const int LaplaceMinimum = 1;

    /// <summary>The number of values in each direction the Laplace distribution can always represent.</summary>
    private const int LaplaceMinimumReach = 16;

    /// <summary>The most bands a band index scales its probability model at.</summary>
    private const int LastModelBand = 20;

    /// <summary>The bits an energy model probability is shifted up by.</summary>
    private const int ProbabilityShift = 7;

    /// <summary>The bits an energy model decay is shifted up by.</summary>
    private const int DecayShift = 6;

    /// <summary>The size of the probability space the Laplace distribution is over.</summary>
    private const int LaplaceTotal = 1 << LaplaceTotalBits;

    /// <summary>The two sides, positive and negative, of the Laplace distribution; also the pair of numbers the energy model holds for each band.</summary>
    private const int Sides = 2;

    /// <summary>The scale of the decay: a decay of this much would keep every value the same.</summary>
    private const int DecayScale = 0x4000;

    /// <summary>The entries of the model for one frame size and kind.</summary>
    private const int ModelBandEntries = 42;

    /// <summary>The scale the prediction and carry shares are stored at.</summary>
    private const float ShareScale = 32768F;

    /// <summary>The share of the error carried to the next band when no prediction from the last frame is used.</summary>
    private const float IntraCarry = 4915F / ShareScale;

    /// <summary>Half, the centre of the interval a fine energy value stands for.</summary>
    private const float Half = 0.5F;

    /// <summary>The size of one step of the band means.</summary>
    private const float MeanStep = 0.0625F;

    /// <summary>The base of the logarithm the energies are in.</summary>
    private const float LogBase = 2F;

    /// <summary>The largest exponent turned into an amplitude, which keeps it finite.</summary>
    private const float MaxExponent = 32F;

    /// <summary>Gets the mean energy of each band in sixteenths of a step of 6 dB, which the coded energy is relative to.</summary>
    internal static ReadOnlySpan<byte> MeanSixteenths =>
    [
        0x67, 0x64, 0x5C, 0x55, 0x51,
        0x4D, 0x48, 0x46, 0x4E, 0x4B,
        0x49, 0x47, 0x4E, 0x4A, 0x45,
        0x48, 0x46, 0x4A, 0x4C, 0x47,
        0x3C, 0x3C, 0x3C, 0x3C, 0x3C,
    ];

    /// <summary>Gets the share of the last frame's energy that is kept in the prediction, out of 32768, for each frame size.</summary>
    private static ReadOnlySpan<ushort> PredictionShares => [0x7300, 0x6600, 0x5300, 0x4000];

    /// <summary>Gets the share of a band's error that carries over to the next band, out of 32768, for each frame size.</summary>
    private static ReadOnlySpan<ushort> CarryShares => [0x75C3, 0x570A, 0x2F5C, 0x199A];

    /// <summary>Reads the coarse energy of every band from the frame's first band up to its last.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="energies">The energy of each band of each channel, band after band, one channel after another; updated in place.</param>
    /// <param name="frame">The frame, whose bands, channels and size say what to read.</param>
    /// <param name="intra">Whether the frame is coded without prediction from the last one.</param>
    internal static void ReadCoarse(ref OpusRangeDecoder decoder, Span<float> energies, CeltFrame frame, bool intra)
    {
        var model = CeltTables.EnergyModel.Slice(((frame.FrameSize * Sides) + (intra ? 1 : 0)) * ModelBandEntries, ModelBandEntries);
        var prediction = intra ? 0F : PredictionShares[frame.FrameSize] / ShareScale;
        var carry = intra ? IntraCarry : CarryShares[frame.FrameSize] / ShareScale;
        var budget = decoder.TotalBits;
        Span<float> previous = [0F, 0F];
        for (var band = frame.Start; band < frame.End; band++)
        {
            for (var channel = 0; channel < frame.Channels; channel++)
            {
                var step = ReadStep(ref decoder, budget - decoder.Tell(), model, band);
                var at = band + (channel * Bands);
                energies[at] = (prediction * Math.Max(PredictionFloor, energies[at])) + previous[channel] + step;
                previous[channel] = previous[channel] + step - (carry * step);
            }
        }
    }

    /// <summary>Reads the fine energy bits of every band.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="energies">The energies, updated in place.</param>
    /// <param name="frame">The frame, whose allocation holds the fine bits of each band.</param>
    internal static void ReadFine(ref OpusRangeDecoder decoder, Span<float> energies, CeltFrame frame)
    {
        for (var band = frame.Start; band < frame.End; band++)
        {
            var fineBits = frame.FineBits[band];
            if (fineBits <= 0)
            {
                continue;
            }

            for (var channel = 0; channel < frame.Channels; channel++)
            {
                var bits = decoder.ReadRaw(fineBits);
                energies[band + (channel * Bands)] += ((bits + Half) / (1 << fineBits)) - Half;
            }
        }
    }

    /// <summary>Spends the bits left over after the shapes were read on one more fine energy bit for the bands that most need it.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="energies">The energies, updated in place.</param>
    /// <param name="frame">The frame, whose allocation holds the fine bits and priority of each band.</param>
    /// <param name="bitsLeft">The whole bits left in the frame.</param>
    internal static void ReadFinalBits(ref OpusRangeDecoder decoder, Span<float> energies, CeltFrame frame, int bitsLeft)
    {
        var channels = frame.Channels;
        for (var round = 0; round < Sides; round++)
        {
            for (var band = frame.Start; band < frame.End && bitsLeft >= channels; band++)
            {
                if (frame.FineBits[band] >= MaxFineBits || frame.FinePriority[band] != round)
                {
                    continue;
                }

                for (var channel = 0; channel < channels; channel++)
                {
                    var bit = decoder.ReadRaw(1);
                    energies[band + (channel * Bands)] += (bit - Half) / (1 << (frame.FineBits[band] + 1));
                    bitsLeft--;
                }
            }
        }
    }

    /// <summary>Turns the log energies into the amplitude of each band.</summary>
    /// <param name="energies">The log energies, in steps of 6 dB, relative to the band means.</param>
    /// <param name="amplitudes">Receives the amplitudes; bands outside the frame's range get zero.</param>
    /// <param name="frame">The frame, whose bands and channels say which energies are in use.</param>
    internal static void ToAmplitudes(ReadOnlySpan<float> energies, Span<float> amplitudes, CeltFrame frame)
    {
        for (var channel = 0; channel < frame.Channels; channel++)
        {
            for (var band = 0; band < Bands; band++)
            {
                var at = band + (channel * Bands);
                var exponent = Math.Min(MaxExponent, energies[at] + (MeanSixteenths[band] * MeanStep));
                amplitudes[at] = band < frame.Start || band >= frame.End ? 0F : MathF.Pow(LogBase, exponent);
            }
        }
    }

    /// <summary>Reads the whole number of steps a band's energy differs from its prediction by.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="bitsAvailable">The bits left in the frame.</param>
    /// <param name="model">The probability model.</param>
    /// <param name="band">The band.</param>
    /// <returns>The steps, which may be negative.</returns>
    private static int ReadStep(ref OpusRangeDecoder decoder, int bitsAvailable, ReadOnlySpan<byte> model, int band)
    {
        if (bitsAvailable >= LaplaceBits)
        {
            var at = Sides * Math.Min(band, LastModelBand);
            return ReadLaplace(ref decoder, (uint)(model[at] << ProbabilityShift), (uint)(model[at + 1] << DecayShift));
        }

        if (bitsAvailable >= SmallBits)
        {
            var symbol = decoder.ReadSymbol(CeltTables.SmallEnergyIcdf, SmallBits);
            return (symbol >> 1) ^ -(symbol & 1);
        }

        return bitsAvailable >= 1 ? -(decoder.ReadBit(1) ? 1 : 0) : -1;
    }

    /// <summary>Reads a number from a two sided geometric distribution (RFC 6716 section 4.3.2.1).</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="zeroFrequency">The frequency of zero out of 32768.</param>
    /// <param name="decay">How quickly the frequency falls away from zero, out of 16384.</param>
    /// <returns>The number.</returns>
    private static int ReadLaplace(ref OpusRangeDecoder decoder, uint zeroFrequency, uint decay)
    {
        var value = 0;
        var found = decoder.DecodeBinary(LaplaceTotalBits);
        uint low = 0;
        var frequency = zeroFrequency;
        if (found >= frequency)
        {
            value++;
            low = frequency;
            frequency = (((LaplaceTotal - (LaplaceMinimum * Sides * LaplaceMinimumReach) - frequency) * (DecayScale - decay)) >> LaplaceTotalBits) + LaplaceMinimum;
            while (frequency > LaplaceMinimum && found >= low + (Sides * frequency))
            {
                frequency *= Sides;
                low += frequency;
                frequency = (uint)((((int)frequency - (Sides * LaplaceMinimum)) * (int)decay) >> LaplaceTotalBits) + LaplaceMinimum;
                value++;
            }

            if (frequency <= LaplaceMinimum)
            {
                var extra = (int)((found - low) >> 1);
                value += extra;
                low += (uint)(Sides * extra * LaplaceMinimum);
            }

            if (found < low + frequency)
            {
                value = -value;
            }
            else
            {
                low += frequency;
            }
        }

        decoder.Update(low, Math.Min(low + frequency, LaplaceTotal), LaplaceTotal);
        return value;
    }
}
