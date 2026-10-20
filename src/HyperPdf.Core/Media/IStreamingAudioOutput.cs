// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Media;

/// <summary>
/// Plays a continuous stream of sound, such as the sound of a video. The caller writes interleaved 32-bit float samples from one
/// thread; a write waits while the device has enough sound buffered, so the caller stays about a fifth of a second ahead. The
/// latency is how long written sound takes to be heard, which the player subtracts from its clock so the picture matches what is heard.
/// </summary>
public interface IStreamingAudioOutput : IDisposable
{
    /// <summary>Gets a value indicating whether sound can be played on this computer.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets how long the sound written so far takes to be heard.</summary>
    TimeSpan Latency { get; }

    /// <summary>Opens a stream.</summary>
    /// <param name="sampleRate">The samples each second for each channel.</param>
    /// <param name="channels">The number of channels.</param>
    /// <returns><see langword="true"/> when the stream is open.</returns>
    bool Start(int sampleRate, int channels);

    /// <summary>Writes samples, waiting while the device is full.</summary>
    /// <param name="samples">Interleaved samples, one for each channel in turn.</param>
    /// <param name="cancellationToken">Stops waiting; the samples are then dropped.</param>
    /// <returns><see langword="true"/> when the samples were accepted.</returns>
    bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken);

    /// <summary>Throws away the sound that has not been heard yet.</summary>
    void Flush();

    /// <summary>Closes the stream.</summary>
    void Close();
}
