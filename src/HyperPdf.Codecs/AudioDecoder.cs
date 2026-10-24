// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs;

/// <summary>Turns the packets of one sound track into samples. A decoder keeps what it needs from one packet to the next, so packets go in the order the file stores them.</summary>
[DebuggerDisplay("AudioDecoder: {SampleRate} Hz, {Channels} channels")]
public abstract class AudioDecoder : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="AudioDecoder"/> class.</summary>
    /// <param name="sampleRate">The samples each second for each channel.</param>
    /// <param name="channels">The number of channels.</param>
    protected AudioDecoder(int sampleRate, int channels)
    {
        SampleRate = sampleRate;
        Channels = channels;
    }

    /// <summary>Gets the samples each second for each channel of the decoded sound.</summary>
    public int SampleRate { get; }

    /// <summary>Gets the number of channels of the decoded sound.</summary>
    public int Channels { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Decodes a packet and appends its samples, interleaved by channel, to a buffer.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="output">Receives the samples.</param>
    /// <exception cref="InvalidDataException">The packet is not valid for this coding.</exception>
    public abstract void Decode(MediaPacket packet, PcmBuffer output);

    /// <summary>Forgets what was kept from earlier packets, for decoding to start again at a packet after a jump in the file.</summary>
    public abstract void Reset();

    /// <summary>Lets go of what the decoder holds.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected abstract void Dispose(bool disposing);
}
