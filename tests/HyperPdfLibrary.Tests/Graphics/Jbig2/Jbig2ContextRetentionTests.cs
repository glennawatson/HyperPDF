// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Tests of the bitmap coding context retention of T.88 section 7.4.2.2: a symbol dictionary whose "bitmap coding context used" bit is
/// set starts its generic region statistics from the final statistics of the last symbol dictionary it refers to, which must have
/// had the "bitmap coding context retained" bit set. Integer statistics are always reset.
/// </summary>
public sealed class Jbig2ContextRetentionTests
{
    /// <summary>The symbol dictionary flag: bitmap coding context used (bit 8).</summary>
    private const int UsedFlag = 0x0100;

    /// <summary>The symbol dictionary flag: bitmap coding context retained (bit 9).</summary>
    private const int RetainedFlag = 0x0200;

    /// <summary>The integer coders a dictionary needs: height, width and export run.</summary>
    private const int IntegerCoders = 3;

    /// <summary>The first size guess for the coded data.</summary>
    private const int InitialOutput = 256;

    /// <summary>The symbols each dictionary defines.</summary>
    private const int SymbolsPerDictionary = 2;

    /// <summary>The number of the first dictionary segment.</summary>
    private const int FirstNumber = 1;

    /// <summary>The number of the second dictionary segment.</summary>
    private const int SecondNumber = 2;

    /// <summary>The page the segments belong to.</summary>
    private static readonly Jbig2Size Page = new(16, 16);

    /// <summary>The size of every symbol.</summary>
    private static readonly Jbig2Size SymbolSize = new(5, 4);

    /// <summary>The rows of the symbols of both dictionaries, MSB first: a box, a cross, a bar and a hook.</summary>
    private static readonly byte[][] Glyphs = [[0xF8, 0x88, 0x88, 0xF8], [0x20, 0xF8, 0x20, 0x20], [0xF8, 0xF8, 0x00, 0xF8], [0xF0, 0x10, 0x10, 0x70]];

    /// <summary>A dictionary that uses the retained statistics of the dictionary before it decodes the symbols coded with them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsedContextsContinueFromTheRetainedOnes()
    {
        var symbols = Decode(true);

        await Assert.That(symbols).IsNotNull();
        for (var i = 0; i < SymbolsPerDictionary; i++)
        {
            await Assert.That(Matches(symbols!, i, Glyphs[SymbolsPerDictionary + i])).IsTrue();
        }
    }

    /// <summary>A dictionary that asks for retained statistics the dictionary before it did not keep is not decoded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsedContextsThatWereNotRetainedFail() => await Assert.That(Decode(false)).IsNull();

    /// <summary>Decodes the two dictionaries.</summary>
    /// <param name="firstRetains">Whether the first dictionary retains its statistics.</param>
    /// <returns>The symbols of the second dictionary, or null when it did not decode.</returns>
    private static Jbig2SymbolStore? Decode(bool firstRetains)
    {
        var pools = new ScratchPools();
        var workspace = new Jbig2Workspace(pools);
        var page = Jbig2Bitmap.Create(Page.Width, Page.Height, pools)!;
        var state = new Jbig2DecodeState(page, workspace, null);
        _ = Jbig2Segments.DecodeSequential(state, TwoDictionaries(firstRetains));
        var second = state.Segments.Find(static segment => segment.Number == SecondNumber)?.Symbols?.Symbols;
        return second is null ? null : Copy(second, pools);
    }

    /// <summary>Copies a store so it can be read after the decoder's state is gone.</summary>
    /// <param name="source">The store.</param>
    /// <param name="pools">The scratch cache.</param>
    /// <returns>The copy.</returns>
    private static Jbig2SymbolStore Copy(Jbig2SymbolStore source, ScratchPools pools)
    {
        var copy = new Jbig2SymbolStore(pools);
        for (var i = 0; i < source.Count; i++)
        {
            _ = copy.TryAdd(source.Get(i));
        }

        return copy;
    }

    /// <summary>Determines whether a symbol has the pixels of a glyph.</summary>
    /// <param name="symbols">The symbols.</param>
    /// <param name="index">The symbol.</param>
    /// <param name="rows">The glyph's rows.</param>
    /// <returns><see langword="true"/> when the pixels match.</returns>
    private static bool Matches(Jbig2SymbolStore symbols, int index, byte[] rows)
    {
        var symbol = symbols.Get(index);
        for (var y = 0; y < SymbolSize.Height; y++)
        {
            for (var x = 0; x < SymbolSize.Width; x++)
            {
                var expected = (rows[y] & (Jbig2Bits.FirstPixelBit >> x)) != 0 ? 1 : 0;
                if (symbol.GetPixel(x, y) != expected)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Codes a page information segment and two dictionaries, the second coded with the first one's statistics.</summary>
    /// <param name="firstRetains">Whether the first dictionary sets the retained bit.</param>
    /// <returns>The segments.</returns>
    private static byte[] TwoDictionaries(bool firstRetains)
    {
        var output = default(PooledBuffer);
        var parameters = Jbig2GenericRegionWriter.Nominal(0, false);
        var bitmapContexts = ArrayPool<byte>.Shared.Rent(parameters.ContextCount);
        try
        {
            bitmapContexts.AsSpan(0, parameters.ContextCount).Clear();
            Jbig2SegmentWriter.WritePageInformation(ref output, 0, Page.Width, Page.Height, true);
            WriteDictionary(ref output, FirstNumber, [], firstRetains ? RetainedFlag : 0, 0, bitmapContexts, parameters);
            WriteDictionary(ref output, SecondNumber, [FirstNumber], UsedFlag, SymbolsPerDictionary, bitmapContexts, parameters);
            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bitmapContexts);
            output.Dispose();
        }
    }

    /// <summary>Writes one dictionary of two symbols, exporting only the new ones.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="referred">The segments the dictionary refers to.</param>
    /// <param name="flags">The dictionary flags.</param>
    /// <param name="inputs">The symbols the dictionary imports.</param>
    /// <param name="bitmapContexts">The generic region contexts, carried from one dictionary to the next.</param>
    /// <param name="parameters">The generic region parameters.</param>
    private static void WriteDictionary(ref PooledBuffer output, int number, ReadOnlySpan<int> referred, int flags, int inputs, byte[] bitmapContexts, in Jbig2GenericParameters parameters)
    {
        var position = Jbig2SegmentWriter.BeginSegment(ref output, number, Jbig2SegmentWriter.SymbolDictionaryType, referred, 1, true);
        Jbig2SegmentWriter.WriteInt16(ref output, flags);
        Jbig2GenericRegionWriter.WriteAdaptivePixels(ref output, parameters);
        Jbig2SegmentWriter.WriteInt32(ref output, SymbolsPerDictionary);
        Jbig2SegmentWriter.WriteInt32(ref output, SymbolsPerDictionary);
        var integerContexts = new byte[IntegerCoders * Jbig2IntegerDecoder.ContextCount];
        var heights = integerContexts.AsSpan(0, Jbig2IntegerDecoder.ContextCount);
        var widths = integerContexts.AsSpan(Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
        var exports = integerContexts.AsSpan((IntegerCoders - 1) * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
        var encoder = new Jbig2MqEncoder(InitialOutput);
        var first = (number - FirstNumber) * SymbolsPerDictionary;
        Jbig2IntegerEncoder.Encode(ref encoder, heights, SymbolSize.Height);
        Jbig2IntegerEncoder.Encode(ref encoder, widths, SymbolSize.Width);
        Jbig2GenericEncoder.Encode(ref encoder, bitmapContexts, new(Glyphs[first], SymbolSize.Width, SymbolSize.Height), parameters, CancellationToken.None);
        Jbig2IntegerEncoder.Encode(ref encoder, widths, 0);
        Jbig2GenericEncoder.Encode(ref encoder, bitmapContexts, new(Glyphs[first + 1], SymbolSize.Width, SymbolSize.Height), parameters, CancellationToken.None);
        Jbig2IntegerEncoder.EncodeOutOfBand(ref encoder, widths);
        Jbig2IntegerEncoder.Encode(ref encoder, exports, inputs);
        Jbig2IntegerEncoder.Encode(ref encoder, exports, SymbolsPerDictionary);
        encoder.Finish(ref output);
        Jbig2SegmentWriter.EndSegment(ref output, position);
    }
}
