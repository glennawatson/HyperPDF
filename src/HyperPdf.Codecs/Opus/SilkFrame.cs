// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Decodes one frame of one channel of the SILK layer: its side information, excitation and synthesis, and the concealment and comfort noise that go with them.</summary>
internal static class SilkFrame
{
    /// <summary>Decodes a frame, or makes up one that was lost.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="output">Receives the speech.</param>
    /// <param name="lost">Whether the frame was lost and is to be concealed.</param>
    /// <param name="codingMode">How the frame is coded.</param>
    /// <param name="scratch">The arrays the synthesis works in.</param>
    /// <returns>The samples of the frame.</returns>
    internal static int Decode(SilkChannel channel, ref OpusRangeDecoder decoder, Span<short> output, bool lost, int codingMode, SilkSynthesisScratch scratch)
    {
        var length = channel.FrameLength;
        channel.Parameters.LtpScaleQ14 = 0;
        if (lost)
        {
            SilkPlc.Process(channel, output, lost: true);
        }
        else
        {
            SilkIndexReader.Read(channel, ref decoder, channel.FramesDecoded, redundant: false, codingMode);
            SilkPulses.Decode(ref decoder, scratch.Pulses, channel.Indices.SignalType, channel.Indices.QuantOffsetType, channel.FrameLength);
            SilkParameters.Decode(channel, codingMode);
            length = channel.FrameLength;
            SilkSynthesis.Decode(channel, output, scratch);
            SilkPlc.Process(channel, output, lost: false);
            channel.LossCount = 0;
            channel.PreviousSignalType = channel.Indices.SignalType;
            channel.FirstFrameAfterReset = false;
        }

        UpdateOutputBuffer(channel, output);
        SilkPlc.GlueFrames(channel, output, length);
        SilkCng.Apply(channel, output, length);
        channel.PreviousLag = channel.Parameters.PitchLags[channel.SubframeCount - 1];
        return length;
    }

    /// <summary>Keeps the frame in the buffer of past output that the long-term prediction reads.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="output">The decoded frame.</param>
    private static void UpdateOutputBuffer(SilkChannel channel, ReadOnlySpan<short> output)
    {
        var keep = channel.LtpMemoryLength - channel.FrameLength;
        channel.OutputBuffer.AsSpan(channel.FrameLength, keep).CopyTo(channel.OutputBuffer);
        output[..channel.FrameLength].CopyTo(channel.OutputBuffer.AsSpan(keep));
    }
}
