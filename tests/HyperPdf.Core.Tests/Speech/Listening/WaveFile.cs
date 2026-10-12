// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>Writes mono 16-bit PCM WAV files, so a listening session can be heard.</summary>
internal static class WaveFile
{
    /// <summary>The size of a WAV header.</summary>
    private const int HeaderBytes = 44;

    /// <summary>Bytes per 16-bit sample.</summary>
    private const int SampleBytes = 2;

    /// <summary>The bits per sample.</summary>
    private const short Bits = 16;

    /// <summary>The size of the format chunk.</summary>
    private const int FormatChunkBytes = 16;

    /// <summary>The bytes before the RIFF chunk's contents: its tag and size.</summary>
    private const int RiffPreambleBytes = 8;

    /// <summary>The format code of integer PCM.</summary>
    private const short PcmFormat = 1;

    /// <summary>One channel.</summary>
    private const short MonoChannels = 1;

    /// <summary>Writes samples to a file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="samples">The samples, from -1 to 1.</param>
    /// <param name="sampleRate">The sample rate.</param>
    internal static void Write(string path, float[] samples, int sampleRate)
    {
        var data = samples.Length * SampleBytes;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(HeaderBytes - RiffPreambleBytes + data);
        writer.Write("WAVEfmt "u8);
        writer.Write(FormatChunkBytes);
        writer.Write(PcmFormat);
        writer.Write(MonoChannels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * SampleBytes);
        writer.Write((short)SampleBytes);
        writer.Write(Bits);
        writer.Write("data"u8);
        writer.Write(data);
        foreach (var sample in samples)
        {
            writer.Write((short)(Math.Clamp(sample, -1F, 1F) * short.MaxValue));
        }
    }
}
