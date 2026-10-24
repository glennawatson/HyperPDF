// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The sizes and limits of the SILK layer (RFC 6716 section 4.2).</summary>
internal static class SilkConstants
{
    /// <summary>The signal type of a frame without voice activity.</summary>
    internal const int TypeNoVoiceActivity = 0;

    /// <summary>The signal type of an unvoiced frame.</summary>
    internal const int TypeUnvoiced = 1;

    /// <summary>The signal type of a voiced frame.</summary>
    internal const int TypeVoiced = 2;

    /// <summary>The coding of a frame that does not depend on the one before.</summary>
    internal const int CodeIndependently = 0;

    /// <summary>The coding of a frame that does not depend on the one before and has no long-term prediction scaling.</summary>
    internal const int CodeIndependentlyNoLtpScaling = 1;

    /// <summary>The coding of a frame that depends on the one before.</summary>
    internal const int CodeConditionally = 2;

    /// <summary>The most subframes in a frame.</summary>
    internal const int MaxSubframes = 4;

    /// <summary>The length of a subframe in milliseconds.</summary>
    internal const int SubframeMs = 5;

    /// <summary>The most samples a subframe holds, at 16 kHz.</summary>
    internal const int MaxSubframeLength = 80;

    /// <summary>The most samples a frame holds, 20 ms at 16 kHz.</summary>
    internal const int MaxFrameLength = 320;

    /// <summary>The most frames in a packet.</summary>
    internal const int MaxFramesPerPacket = 3;

    /// <summary>The highest order of the short-term prediction filter.</summary>
    internal const int MaxLpcOrder = 16;

    /// <summary>The lowest order of the short-term prediction filter.</summary>
    internal const int MinLpcOrder = 10;

    /// <summary>The taps of the long-term prediction filter.</summary>
    internal const int LtpOrder = 5;

    /// <summary>The index of the middle tap of the long-term prediction filter, which is also the taps on either side.</summary>
    internal const int LtpCentre = 2;

    /// <summary>The milliseconds of past output the long-term prediction reads.</summary>
    internal const int LtpMemoryMs = 20;

    /// <summary>The samples of a shell block.</summary>
    internal const int ShellBlockLength = 16;

    /// <summary>The log2 of the samples of a shell block.</summary>
    internal const int ShellBlockLengthLog2 = 4;

    /// <summary>The most shell blocks in a frame.</summary>
    internal const int MaxShellBlocks = MaxFrameLength / ShellBlockLength;

    /// <summary>The rate levels of the excitation.</summary>
    internal const int RateLevels = 10;

    /// <summary>The most pulses the number of pulses in a block codes before it counts least significant bits.</summary>
    internal const int MaxPulses = 16;

    /// <summary>The number of gain levels.</summary>
    internal const int GainLevels = 64;

    /// <summary>The lowest change of a gain index.</summary>
    internal const int MinDeltaGain = -4;

    /// <summary>The highest change of a gain index.</summary>
    internal const int MaxDeltaGain = 36;

    /// <summary>The largest amplitude a line spectral frequency residual codes before its extension.</summary>
    internal const int NlsfQuantMaxAmplitude = 4;

    /// <summary>The Q-scale of the weights of the line spectral frequencies.</summary>
    internal const int NlsfWeightBits = 2;

    /// <summary>The size of the table of cosines used to turn line spectral frequencies into filter coefficients, less one.</summary>
    internal const int LsfCosineSize = 128;

    /// <summary>The most times the stabiliser of line spectral frequencies moves the closest pair apart.</summary>
    internal const int NlsfStabilizeLoops = 20;

    /// <summary>The most times the filter coefficients are limited to fit a 16 bit number.</summary>
    internal const int MaxLpcLimitIterations = 10;

    /// <summary>The most times the filter is shrunk to make it stable.</summary>
    internal const int MaxLpcStabilizeIterations = 16;

    /// <summary>The bandwidth expansion applied to the filter after a lost packet, in Q16.</summary>
    internal const int BandwidthExpansionAfterLoss = 63_570;

    /// <summary>The channels of a stereo stream, the halves of a split and other pairs.</summary>
    internal const int Pair = 2;

    /// <summary>The interpolation factor, in quarters, that means the first half of the frame uses the filter of the second.</summary>
    internal const int NoInterpolation = 4;

    /// <summary>The hertz in a kilohertz.</summary>
    internal const int HertzPerKilohertz = 1000;

    /// <summary>The offset of the quantisation levels towards zero, in Q10.</summary>
    internal const int QuantLevelAdjust = 80;

    /// <summary>The samples of past output kept for the output buffer of the long-term prediction: a frame and two subframes.</summary>
    internal const int OutputBufferLength = MaxFrameLength + (2 * MaxSubframeLength);

    /// <summary>The milliseconds of a stereo predictor interpolation.</summary>
    internal const int StereoInterpolationMs = 8;

    /// <summary>The number of sub steps between two quantised stereo prediction values.</summary>
    internal const int StereoQuantSubSteps = 5;

    /// <summary>The lowest sampling rate of the layer in kHz.</summary>
    internal const int NarrowbandKhz = 8;

    /// <summary>The middle sampling rate of the layer in kHz.</summary>
    internal const int MediumbandKhz = 12;

    /// <summary>The highest sampling rate of the layer in kHz.</summary>
    internal const int WidebandKhz = 16;

    /// <summary>The index of the 12 kHz rate among the rates.</summary>
    private const int MediumbandIndex = 1;

    /// <summary>The index of the 16 kHz rate among the rates.</summary>
    private const int WidebandIndex = 2;

    /// <summary>Gets the index of an internal sampling rate among the three there are.</summary>
    /// <param name="rateKhz">The rate in kHz: 8, 12 or 16.</param>
    /// <returns>0, 1 or 2.</returns>
    internal static int RateIndex(int rateKhz) => rateKhz switch
    {
        NarrowbandKhz => 0,
        MediumbandKhz => MediumbandIndex,
        _ => WidebandIndex,
    };
}
