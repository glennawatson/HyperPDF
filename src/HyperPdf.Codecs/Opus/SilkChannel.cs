// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The state of the SILK decoder for one channel: what a frame needs from the frames before it, and the size of the frames being decoded.</summary>
internal sealed class SilkChannel
{
    /// <summary>The gain, in Q16, that leaves a signal unchanged.</summary>
    internal const int UnityGain = 1 << 16;

    /// <summary>The gain index the decoder starts from, and returns to after a loss.</summary>
    internal const int InitialGainIndex = 10;

    /// <summary>The pitch lag the decoder starts from.</summary>
    internal const int InitialLag = 100;

    /// <summary>The state of the 2x upsampler, one for each rate.</summary>
    private readonly SilkResampler?[] _resamplers = new SilkResampler?[3];

    /// <summary>Gets or sets the previous gain, in Q16.</summary>
    internal int PreviousGainQ16 { get; set; }

    /// <summary>Gets the excitation of the frame, in Q14.</summary>
    internal int[] Excitation { get; } = new int[SilkConstants.MaxFrameLength];

    /// <summary>Gets the state of the short-term prediction filter, in Q14.</summary>
    internal int[] LpcState { get; } = new int[SilkConstants.MaxLpcOrder];

    /// <summary>Gets the output of the frames before, which the long-term prediction reads.</summary>
    internal short[] OutputBuffer { get; } = new short[SilkConstants.OutputBufferLength];

    /// <summary>Gets or sets the pitch lag of the last subframe of the frame before.</summary>
    internal int PreviousLag { get; set; }

    /// <summary>Gets or sets the gain index of the last subframe of the frame before.</summary>
    internal int LastGainIndex { get; set; }

    /// <summary>Gets or sets the internal sampling rate in kHz, or zero before the first frame.</summary>
    internal int SampleRateKhz { get; set; }

    /// <summary>Gets or sets the output sampling rate in Hz, or zero before the first frame.</summary>
    internal int ApiRateHz { get; set; }

    /// <summary>Gets or sets the number of subframes in a frame.</summary>
    internal int SubframeCount { get; set; }

    /// <summary>Gets or sets the samples in a frame.</summary>
    internal int FrameLength { get; set; }

    /// <summary>Gets or sets the samples in a subframe.</summary>
    internal int SubframeLength { get; set; }

    /// <summary>Gets or sets the samples of output the long-term prediction reads.</summary>
    internal int LtpMemoryLength { get; set; }

    /// <summary>Gets or sets the order of the short-term prediction filter.</summary>
    internal int LpcOrder { get; set; }

    /// <summary>Gets the line spectral frequencies of the frame before, in Q15.</summary>
    internal short[] PreviousNlsf { get; } = new short[SilkConstants.MaxLpcOrder];

    /// <summary>Gets or sets a value indicating whether no frame has been decoded since the state was reset.</summary>
    internal bool FirstFrameAfterReset { get; set; }

    /// <summary>Gets or sets the frames of the packet decoded so far.</summary>
    internal int FramesDecoded { get; set; }

    /// <summary>Gets or sets the frames in the packet.</summary>
    internal int FramesPerPacket { get; set; }

    /// <summary>Gets or sets the signal type of the frame before, as the entropy coding sees it.</summary>
    internal int EntropyPreviousSignalType { get; set; }

    /// <summary>Gets or sets the pitch lag index of the frame before, as the entropy coding sees it.</summary>
    internal int EntropyPreviousLagIndex { get; set; }

    /// <summary>Gets the voice activity flag of each frame of the packet.</summary>
    internal bool[] VadFlags { get; } = new bool[SilkConstants.MaxFramesPerPacket];

    /// <summary>Gets or sets a value indicating whether the packet has redundancy frames for this channel.</summary>
    internal bool LbrrFlag { get; set; }

    /// <summary>Gets the redundancy flag of each frame of the packet.</summary>
    internal bool[] LbrrFlags { get; } = new bool[SilkConstants.MaxFramesPerPacket];

    /// <summary>Gets or sets the resampler for the current rate.</summary>
    internal SilkResampler? Resampler { get; set; }

    /// <summary>Gets or sets the codebook of the line spectral frequencies.</summary>
    internal SilkCodebook Codebook { get; set; }

    /// <summary>Gets the indices of the frame being decoded.</summary>
    internal SilkIndices Indices { get; } = new();

    /// <summary>Gets the comfort noise state.</summary>
    internal SilkCngState Cng { get; } = new();

    /// <summary>Gets or sets the frames lost in a row.</summary>
    internal int LossCount { get; set; }

    /// <summary>Gets or sets the signal type of the last good frame.</summary>
    internal int PreviousSignalType { get; set; }

    /// <summary>Gets the concealment state.</summary>
    internal SilkPlcState Plc { get; } = new();

    /// <summary>Gets the parameters of the frame being decoded.</summary>
    internal SilkFrameParameters Parameters { get; } = new();

    /// <summary>Gets the inverse cumulative frequencies of the low bits of the pitch lag.</summary>
    internal ReadOnlySpan<byte> PitchLagLowBitsIcdf => SampleRateKhz switch
    {
        SilkConstants.WidebandKhz => SilkTables.Uniform8Icdf,
        SilkConstants.MediumbandKhz => SilkTables.Uniform6Icdf,
        _ => SilkTables.Uniform4Icdf,
    };

    /// <summary>Gets the inverse cumulative frequencies of the pitch contour.</summary>
    internal ReadOnlySpan<byte> PitchContourIcdf => SampleRateKhz == SilkConstants.NarrowbandKhz ? NarrowContourIcdf : WideContourIcdf;

    /// <summary>Gets the inverse cumulative frequencies of the pitch contour at 8 kHz.</summary>
    private ReadOnlySpan<byte> NarrowContourIcdf =>
        SubframeCount == SilkConstants.MaxSubframes ? SilkTables.PitchContourNarrowIcdf : SilkTables.PitchContour10MsNarrowIcdf;

    /// <summary>Gets the inverse cumulative frequencies of the pitch contour at 12 and 16 kHz.</summary>
    private ReadOnlySpan<byte> WideContourIcdf =>
        SubframeCount == SilkConstants.MaxSubframes ? SilkTables.PitchContourIcdf : SilkTables.PitchContour10MsIcdf;

    /// <summary>Puts the state back to what it is before the first frame, like a newly made decoder.</summary>
    internal void Initialize()
    {
        PreviousGainQ16 = UnityGain;
        Array.Clear(Excitation);
        Array.Clear(LpcState);
        Array.Clear(OutputBuffer);
        Array.Clear(PreviousNlsf);
        PreviousLag = 0;
        LastGainIndex = 0;
        SampleRateKhz = 0;
        ApiRateHz = 0;
        SubframeCount = 0;
        FrameLength = 0;
        SubframeLength = 0;
        LtpMemoryLength = 0;
        LpcOrder = 0;
        FirstFrameAfterReset = true;
        FramesDecoded = 0;
        FramesPerPacket = 0;
        EntropyPreviousSignalType = 0;
        EntropyPreviousLagIndex = 0;
        Array.Clear(VadFlags);
        Array.Clear(LbrrFlags);
        LbrrFlag = false;
        Resampler = null;
        LossCount = 0;
        PreviousSignalType = SilkConstants.TypeNoVoiceActivity;
        SilkCng.Reset(this);
        SilkPlc.Reset(this);
        Plc.SampleRateKhz = 0;
        Cng.SampleRateKhz = 0;
    }

    /// <summary>Sets the size of the frames from the sampling rate and the subframes in a frame, resetting what depends on the rate when it changes.</summary>
    /// <param name="rateKhz">The internal sampling rate in kHz: 8, 12 or 16.</param>
    /// <param name="apiRateHz">The output sampling rate in Hz.</param>
    internal void SetSampleRate(int rateKhz, int apiRateHz)
    {
        SubframeLength = SilkConstants.SubframeMs * rateKhz;
        var frameLength = SubframeCount * SubframeLength;
        if (SampleRateKhz != rateKhz || ApiRateHz != apiRateHz)
        {
            Resampler = GetResampler(rateKhz);
            Resampler.Reset();
            ApiRateHz = apiRateHz;
        }

        if (SampleRateKhz == rateKhz && frameLength == FrameLength)
        {
            return;
        }

        if (SampleRateKhz != rateKhz)
        {
            ResetForNewRate(rateKhz);
        }

        SampleRateKhz = rateKhz;
        FrameLength = frameLength;
    }

    /// <summary>Copies the resampler state of another channel, for a stream that changes from one channel to two.</summary>
    /// <param name="other">The channel to copy from.</param>
    internal void CopyResamplerFrom(SilkChannel other)
    {
        if (Resampler is not null && other.Resampler is not null && Resampler.InputKhz == other.Resampler.InputKhz)
        {
            Resampler.CopyFrom(other.Resampler);
        }
    }

    /// <summary>Gets the resampler for an internal rate, making it the first time.</summary>
    /// <param name="rateKhz">The internal rate in kHz.</param>
    /// <returns>The resampler.</returns>
    private SilkResampler GetResampler(int rateKhz) => _resamplers[SilkConstants.RateIndex(rateKhz)] ??= new SilkResampler(rateKhz);

    /// <summary>Resets what depends on the internal rate: the memory lengths, the codebook and the history.</summary>
    /// <param name="rateKhz">The new internal rate in kHz.</param>
    private void ResetForNewRate(int rateKhz)
    {
        LtpMemoryLength = SilkConstants.LtpMemoryMs * rateKhz;
        var wide = rateKhz == SilkConstants.WidebandKhz;
        LpcOrder = wide ? SilkConstants.MaxLpcOrder : SilkConstants.MinLpcOrder;
        Codebook = new(wide);
        FirstFrameAfterReset = true;
        PreviousLag = InitialLag;
        LastGainIndex = InitialGainIndex;
        PreviousSignalType = SilkConstants.TypeNoVoiceActivity;
        Array.Clear(OutputBuffer);
        Array.Clear(LpcState);
    }
}
