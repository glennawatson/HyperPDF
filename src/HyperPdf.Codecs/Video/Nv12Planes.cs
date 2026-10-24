// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.Video;

/// <summary>Turns the NV12 pictures operating system decoders give (a luma plane, then one plane of interleaved Cb and Cr) into the three planes of a <see cref="VideoFrame"/>.</summary>
public static class Nv12Planes
{
    /// <summary>The number of colour sample pairs a 128 bit vector holds.</summary>
    private const int PairsPerVector = 8;

    /// <summary>The number of bytes in a pair of Cb and Cr samples.</summary>
    private const int PairSize = 2;

    /// <summary>The indexes that gather the even bytes of a vector, then the odd bytes, as two 64 bit halves.</summary>
    private const ulong GatherEven = 0x0E0C_0A08_0604_0200;

    /// <summary>The indexes that gather the odd bytes of a vector.</summary>
    private const ulong GatherOdd = 0x0F0D_0B09_0705_0301;

    /// <summary>Copies a picture out of NV12 memory.</summary>
    /// <param name="source">The NV12 memory: rows of luma, each <paramref name="stride"/> long, then rows of interleaved colour.</param>
    /// <param name="stride">The distance between the starts of two rows, in bytes.</param>
    /// <param name="lumaRows">The number of rows of luma in the memory, which is where the colour rows begin.</param>
    /// <param name="frame">Receives the picture; its size says how much is copied.</param>
    /// <exception cref="ArgumentException">The memory is too short for the picture.</exception>
    public static void Copy(ReadOnlySpan<byte> source, int stride, int lumaRows, VideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var colourStart = stride * lumaRows;
        var needed = colourStart + (stride * (frame.ChromaHeight - 1)) + (frame.ChromaWidth * PairSize);
        if (stride < frame.Width || lumaRows < frame.Height || source.Length < needed)
        {
            throw new ArgumentException("The NV12 memory is too short for the picture.", nameof(source));
        }

        Copy(source[..colourStart], stride, source[colourStart..], stride, frame);
    }

    /// <summary>Copies a picture out of NV12 memory whose luma and colour rows are in separate runs of memory.</summary>
    /// <param name="luma">The rows of luma.</param>
    /// <param name="lumaStride">The distance between the starts of two rows of luma, in bytes.</param>
    /// <param name="colour">The rows of interleaved Cb and Cr.</param>
    /// <param name="colourStride">The distance between the starts of two rows of colour, in bytes.</param>
    /// <param name="frame">Receives the picture; its size says how much is copied.</param>
    /// <exception cref="ArgumentException">The memory is too short for the picture.</exception>
    public static void Copy(ReadOnlySpan<byte> luma, int lumaStride, ReadOnlySpan<byte> colour, int colourStride, VideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var lumaNeeded = (lumaStride * (frame.Height - 1)) + frame.Width;
        var colourNeeded = (colourStride * (frame.ChromaHeight - 1)) + (frame.ChromaWidth * PairSize);
        if (lumaStride < frame.Width || colourStride < frame.ChromaWidth * PairSize || luma.Length < lumaNeeded || colour.Length < colourNeeded)
        {
            throw new ArgumentException("The NV12 memory is too short for the picture.", nameof(luma));
        }

        for (var row = 0; row < frame.Height; row++)
        {
            luma.Slice(row * lumaStride, frame.Width).CopyTo(frame.Y.AsSpan(row * frame.Width, frame.Width));
        }

        for (var row = 0; row < frame.ChromaHeight; row++)
        {
            Deinterleave(
                colour.Slice(row * colourStride, frame.ChromaWidth * PairSize),
                frame.U.AsSpan(row * frame.ChromaWidth, frame.ChromaWidth),
                frame.V.AsSpan(row * frame.ChromaWidth, frame.ChromaWidth));
        }
    }

    /// <summary>Splits pairs of Cb and Cr samples into a run of each.</summary>
    /// <param name="pairs">The samples, Cb then Cr, over and over.</param>
    /// <param name="cb">Receives the Cb samples.</param>
    /// <param name="cr">Receives the Cr samples.</param>
    public static void Deinterleave(ReadOnlySpan<byte> pairs, Span<byte> cb, Span<byte> cr)
    {
        var count = Math.Min(cb.Length, cr.Length);
        var done = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            var gather = Vector128.Create(GatherEven, GatherOdd).AsByte();
            for (; done + PairsPerVector <= count; done += PairsPerVector)
            {
                var gathered = Vector128.Shuffle(Vector128.Create(pairs[(done * PairSize)..]), gather).AsUInt64();
                MemoryMarshal.Write(cb[done..], gathered.GetElement(0));
                MemoryMarshal.Write(cr[done..], gathered.GetElement(1));
            }
        }

        for (; done < count; done++)
        {
            cb[done] = pairs[done * PairSize];
            cr[done] = pairs[(done * PairSize) + 1];
        }
    }
}
