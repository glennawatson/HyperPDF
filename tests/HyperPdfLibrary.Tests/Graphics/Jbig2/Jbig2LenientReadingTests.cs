// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Tests of two places where the decoder reads more than T.88 requires, because ITU's own conformance streams need it: a text
/// region that refers to no symbol dictionary, and a symbol dictionary whose export flags cannot be read.
/// </summary>
public sealed class Jbig2LenientReadingTests
{
    /// <summary>The bytes before the referred-to segment numbers of a segment header: number, flags and the count byte.</summary>
    private const int HeaderPrefixBytes = 6;

    /// <summary>The bytes of a 32-bit field.</summary>
    private const int IntBytes = 4;

    /// <summary>The shift of the referred-to count in its byte.</summary>
    private const int CountShift = 5;

    /// <summary>The mask of the segment type in the segment flags.</summary>
    private const int TypeMask = 0x3F;

    /// <summary>The symbols the test dictionary defines.</summary>
    private const int DefinedSymbols = 2;

    /// <summary>The symbols the partly exporting dictionary says it exports.</summary>
    private const int PartlyExported = 1;

    /// <summary>The integer coders the test dictionary uses: height, width and export run.</summary>
    private const int IntegerCoders = 3;

    /// <summary>The first size guess for the coded data.</summary>
    private const int InitialOutput = 256;

    /// <summary>The text region segment types: intermediate, immediate and immediate lossless.</summary>
    private static readonly int[] TextTypes = [4, 6, 7];

    /// <summary>The page the text tests code.</summary>
    private static readonly Jbig2Size TextPage = new(320, 220);

    /// <summary>The text style of the text test: large glyphs, no stray pixels.</summary>
    private static readonly Jbig2TestTextStyle Style = new(3, 9, 0);

    /// <summary>The page the dictionary tests use.</summary>
    private static readonly Jbig2Size DictionaryPage = new(16, 16);

    /// <summary>The size of each symbol in the dictionary tests.</summary>
    private static readonly Jbig2Size SymbolSize = new(5, 4);

    /// <summary>The rows of the first symbol: a 5 by 4 box.</summary>
    private static readonly byte[] FirstSymbol = [0xF8, 0x88, 0x88, 0xF8];

    /// <summary>The rows of the second symbol: a 5 by 4 cross.</summary>
    private static readonly byte[] SecondSymbol = [0x20, 0xF8, 0x20, 0x20];

    /// <summary>A text region with no referred-to dictionary uses the dictionaries decoded before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextRegionWithoutReferencesUsesPrecedingDictionaries()
    {
        var rows = Jbig2TestPages.Text(TextPage, Style);
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });
        var stripped = StripReferences(coded.Data);
        var decoded = Jbig2TestPages.Decode(stripped, coded.Globals, TextPage);

        await Assert.That(stripped.Length).IsLessThan(coded.Data.Length);
        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>A dictionary that exports every symbol it defines keeps them when its export flags cannot be read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreadableExportFlagsExportEveryNewSymbol()
    {
        var pools = new ScratchPools();
        using var workspace = new Jbig2Workspace(pools);
        using var page = Jbig2Bitmap.Create(DictionaryPage.Width, DictionaryPage.Height, pools);
        using var state = new Jbig2DecodeState(page, workspace, null);
        _ = Jbig2Segments.DecodeSequential(state, DictionaryWithoutExportFlags(DefinedSymbols));
        var dictionaries = state.Segments.FindAll(static segment => segment.Symbols is not null);

        await Assert.That(dictionaries.Count).IsEqualTo(1);
        await Assert.That(dictionaries[0].Symbols!.Symbols.Count).IsEqualTo(DefinedSymbols);
    }

    /// <summary>A dictionary that exports fewer symbols than it defines is dropped when its export flags cannot be read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreadableExportFlagsOfAPartialExportDropTheDictionary()
    {
        var pools = new ScratchPools();
        using var workspace = new Jbig2Workspace(pools);
        using var page = Jbig2Bitmap.Create(DictionaryPage.Width, DictionaryPage.Height, pools);
        using var state = new Jbig2DecodeState(page, workspace, null);
        _ = Jbig2Segments.DecodeSequential(state, DictionaryWithoutExportFlags(PartlyExported));

        await Assert.That(state.Segments.Exists(static segment => segment.Symbols is not null)).IsFalse();
    }

    /// <summary>Rewrites every text region segment header to refer to no segments.</summary>
    /// <param name="data">The page data the encoder wrote; every segment has a short-form header and its referred-to numbers take one byte.</param>
    /// <returns>The rewritten data.</returns>
    private static byte[] StripReferences(byte[] data)
    {
        var result = new List<byte>(data.Length);
        for (var offset = 0; offset < data.Length;)
        {
            var references = data[offset + HeaderPrefixBytes - 1] >> CountShift;
            var headerLength = HeaderPrefixBytes + references + 1 + IntBytes;
            var dataLength = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + headerLength - IntBytes));
            var type = data[offset + IntBytes] & TypeMask;
            if (Array.IndexOf(TextTypes, type) >= 0)
            {
                result.AddRange(data.AsSpan(offset, HeaderPrefixBytes - 1).ToArray());
                result.Add(0);
                result.Add(data[offset + headerLength - IntBytes - 1]);
                result.AddRange(data.AsSpan(offset + headerLength - IntBytes, IntBytes + dataLength).ToArray());
            }
            else
            {
                result.AddRange(data.AsSpan(offset, headerLength + dataLength).ToArray());
            }

            offset += headerLength + dataLength;
        }

        return [.. result];
    }

    /// <summary>Codes a page information segment and an arithmetic symbol dictionary of two symbols that stops before the export flags.</summary>
    /// <param name="exported">The number of symbols the dictionary header says it exports.</param>
    /// <returns>The segments.</returns>
    private static byte[] DictionaryWithoutExportFlags(int exported)
    {
        var output = default(PooledBuffer);
        var parameters = Jbig2GenericRegionWriter.Nominal(0, false);
        var bitmapContexts = ArrayPool<byte>.Shared.Rent(parameters.ContextCount);
        var integerContexts = ArrayPool<byte>.Shared.Rent(IntegerCoders * Jbig2IntegerDecoder.ContextCount);
        var encoder = new Jbig2MqEncoder(InitialOutput);
        try
        {
            bitmapContexts.AsSpan(0, parameters.ContextCount).Clear();
            integerContexts.AsSpan(0, IntegerCoders * Jbig2IntegerDecoder.ContextCount).Clear();
            Jbig2SegmentWriter.WritePageInformation(ref output, 0, DictionaryPage.Width, DictionaryPage.Height, true);
            var position = Jbig2SegmentWriter.BeginSegment(ref output, 1, Jbig2SegmentWriter.SymbolDictionaryType, [], 1, true);
            Jbig2SegmentWriter.WriteInt16(ref output, 0);
            Jbig2GenericRegionWriter.WriteAdaptivePixels(ref output, parameters);
            Jbig2SegmentWriter.WriteInt32(ref output, exported);
            Jbig2SegmentWriter.WriteInt32(ref output, DefinedSymbols);
            var heights = integerContexts.AsSpan(0, Jbig2IntegerDecoder.ContextCount);
            var widths = integerContexts.AsSpan(Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
            Jbig2IntegerEncoder.Encode(ref encoder, heights, SymbolSize.Height);
            Jbig2IntegerEncoder.Encode(ref encoder, widths, SymbolSize.Width);
            Jbig2GenericEncoder.Encode(ref encoder, bitmapContexts, new(FirstSymbol, SymbolSize.Width, SymbolSize.Height), parameters, CancellationToken.None);
            Jbig2IntegerEncoder.Encode(ref encoder, widths, 0);
            Jbig2GenericEncoder.Encode(ref encoder, bitmapContexts, new(SecondSymbol, SymbolSize.Width, SymbolSize.Height), parameters, CancellationToken.None);
            Jbig2IntegerEncoder.EncodeOutOfBand(ref encoder, widths);
            encoder.Finish(ref output);
            Jbig2SegmentWriter.EndSegment(ref output, position);
            return output.ToArray();
        }
        catch
        {
            encoder.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bitmapContexts);
            ArrayPool<byte>.Shared.Return(integerContexts);
            output.Dispose();
        }
    }
}
