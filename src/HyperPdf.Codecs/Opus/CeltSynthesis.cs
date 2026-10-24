// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Opus;

/// <summary>Turns the decoded shape and energy of each band into a signal (RFC 6716 section 4.3.6 and 4.3.7).</summary>
internal static class CeltSynthesis
{
    /// <summary>Scales each band's normalised shape by its amplitude.</summary>
    /// <param name="frame">The frame, whose bands, channels and size say what is in use.</param>
    /// <param name="shapes">The normalised shapes of the channels, one after another, each <paramref name="size"/> long.</param>
    /// <param name="amplitudes">The amplitude of each band of each channel.</param>
    /// <param name="spectrum">Receives the spectrum, laid out as <paramref name="shapes"/> is; entries above the last band are zeroed.</param>
    /// <param name="size">The entries in a channel of the frame.</param>
    internal static void Denormalise(CeltFrame frame, ReadOnlySpan<float> shapes, ReadOnlySpan<float> amplitudes, Span<float> spectrum, int size)
    {
        var edges = CeltTables.BandEdges;
        var scale = 1 << frame.FrameSize;
        for (var channel = 0; channel < frame.Channels; channel++)
        {
            var offset = channel * size;
            for (var band = 0; band < frame.End; band++)
            {
                var gain = amplitudes[(channel * CeltEnergy.Bands) + band];
                for (var j = scale * edges[band]; j < scale * edges[band + 1]; j++)
                {
                    spectrum[offset + j] = shapes[offset + j] * gain;
                }
            }

            var top = scale * edges[frame.End];
            spectrum.Slice(offset + top, size - top).Clear();
        }
    }

    /// <summary>Turns the spectrum of a channel into time samples and adds the overlap left by the block before.</summary>
    /// <param name="mdct">The transform.</param>
    /// <param name="workspace">The scratch arrays for the transform and for the block being built.</param>
    /// <param name="spectrum">The channel's spectrum.</param>
    /// <param name="shortBlocks">Whether the spectrum is made of short blocks.</param>
    /// <param name="history">The channel's history: the earlier output, then room for the new output, then the overlap kept for the next block.</param>
    internal static void Inverse(CeltMdct mdct, CeltSynthesisWorkspace workspace, ReadOnlySpan<float> spectrum, bool shortBlocks, Span<float> history)
    {
        var frameSize = spectrum.Length;
        var overlap = CeltDecoder.Overlap;
        var blocks = shortBlocks ? frameSize / CeltDecoder.ShortBlockSize : 1;
        var blockSize = frameSize / blocks;
        var shift = shortBlocks ? CeltDecoder.MaxShift : CeltDecoder.MaxShift - BitOperations.Log2((uint)(frameSize / CeltDecoder.ShortBlockSize));
        var block = workspace.Block;
        block.AsSpan(0, overlap).Clear();
        var shape = new CeltMdctShape(CeltFloatTables.Window, overlap, shift);
        for (var b = 0; b < blocks; b++)
        {
            mdct.Backward(spectrum[b..], blocks, block.AsSpan(blockSize * b), shape, workspace.Transform);
        }

        var output = history.Slice(CeltDecoder.HistorySize - frameSize, frameSize);
        var tail = history.Slice(CeltDecoder.HistorySize, overlap);
        for (var j = 0; j < overlap; j++)
        {
            output[j] = block[j] + tail[j];
        }

        block.AsSpan(overlap, frameSize - overlap).CopyTo(output[overlap..]);
        block.AsSpan(frameSize, overlap).CopyTo(tail);
    }
}
