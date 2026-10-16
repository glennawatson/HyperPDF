// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Generation;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdf.Benchmarks;

/// <summary>Measures valid font and CMap parsing alongside the checks that bound damaged lengths.</summary>
public class FontBinaryParsingBenchmarks
{
    /// <summary>The Type 1 eexec cipher's initial key.</summary>
    private const ushort EexecKey = 55_665;

    /// <summary>The Type 1 cipher multiplier.</summary>
    private const int CipherMultiplier = 52_845;

    /// <summary>The Type 1 cipher increment.</summary>
    private const int CipherIncrement = 22_719;

    /// <summary>The shift selecting the key's high byte.</summary>
    private const int KeyShift = 8;

    /// <summary>The random prefix discarded by eexec decoding.</summary>
    private const int RandomPrefix = 4;

    /// <summary>The number of location entries for one glyph and its sentinel.</summary>
    private const int LocationEntries = 2;

    /// <summary>A CFF INDEX containing one byte.</summary>
    private static readonly byte[] IndexData = [0, 1, 1, 1, 2, 0x41];

    /// <summary>A packed number followed by a byte string.</summary>
    private static readonly byte[] PackedData = [0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x41];

    /// <summary>A binary Unicode mapping for one CID.</summary>
    private static readonly byte[] UnicodeData = [0, 0x81, 1, 0, 1, 0x30, 0x42];

    /// <summary>A binary CMap metadata string.</summary>
    private static readonly byte[] MetadataData = [1, 0x41];

    /// <summary>A packed CMap containing one code range.</summary>
    private static readonly byte[] RangeData = [1, 0, 0, 0, 0, 0, 1, 0x41, 0, 2];

    /// <summary>A one-glyph long TrueType location table.</summary>
    private static readonly byte[] LocationData = [0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>A format 12 TrueType cmap mapping one code to glyph one.</summary>
    private static readonly byte[] CoverageData =
        [0, 0x0C, 0, 0, 0, 0, 0, 0x1C, 0, 0, 0, 0, 0, 0, 0, 1,
         0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];

    /// <summary>A GSUB extension wrapping one single-glyph substitution.</summary>
    private static readonly byte[] ExtensionData =
        [0, 1, 0, 0, 0, 0, 0, 0, 0, 0x0A,
         0, 1, 0, 0x04,
         0, 0x07, 0, 0, 0, 1, 0, 0x08,
         0, 1, 0, 1, 0, 0, 0, 0x08,
         0, 1, 0, 0x06, 0, 1,
         0, 1, 0, 1, 0, 1];

    /// <summary>A mark glyph set containing glyph one.</summary>
    private static readonly byte[] MarkSetData =
        [0, 1, 0, 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x0E,
         0, 1, 0, 1, 0, 0, 0, 0x08,
         0, 1, 0, 1, 0, 1];

    /// <summary>The single substitution stage.</summary>
    private static readonly LookupEntry[] ExtensionStage = [new(0, 1, 1)];

    /// <summary>The font resources selected for the packed mapping.</summary>
    private readonly PdfFontResources _fontResources = new();

    /// <summary>The reusable glyph storage for the extension lookup.</summary>
    private readonly GlyphBuffer _extensionGlyphs = new(1);

    /// <summary>The extension lookup engine.</summary>
    private readonly LayoutEngine _extensionEngine;

    /// <summary>The mark-set table.</summary>
    private readonly OtGdef _markSets = new(MarkSetData);

    /// <summary>A Type 1 font with one unencrypted charstring inside an encrypted eexec section.</summary>
    private byte[] _type1Data = [];

    /// <summary>Initializes a new instance of the <see cref="FontBinaryParsingBenchmarks"/> class.</summary>
    public FontBinaryParsingBenchmarks() =>
        _extensionEngine = new(new(ExtensionData), new(ReadOnlyMemory<byte>.Empty), _extensionGlyphs, false, false);

    /// <summary>Builds the encrypted Type 1 input outside measured parsing.</summary>
    [GlobalSetup]
    public void Setup()
    {
        byte[] secret = [.. new byte[RandomPrefix], .. "/lenIV -1 def /CharStrings 1 dict dup begin /.notdef 1 RD "u8, 0x0E, .. " ND end"u8];
        var key = EexecKey;
        for (var index = 0; index < secret.Length; index++)
        {
            var cipher = (byte)(secret[index] ^ (key >> KeyShift));
            secret[index] = cipher;
            key = (ushort)(((cipher + key) * CipherMultiplier) + CipherIncrement);
        }

        _type1Data = [.. "currentfile eexec\n"u8, .. secret];
    }

    /// <summary>Returns the glyph storage to its shared pool.</summary>
    [GlobalCleanup]
    public void Cleanup() => _extensionGlyphs.Dispose();

    /// <summary>Reads an INDEX and accesses its first object without allocating.</summary>
    /// <returns>The object's byte count and INDEX end.</returns>
    [Benchmark]
    public int ReadCffIndex()
    {
        var index = CffIndex.Read(IndexData, 0, out var end);
        return end + index.Get(IndexData, 0).Length;
    }

    /// <summary>Reads the largest packed unsigned number and a byte range without allocating.</summary>
    /// <returns>The decoded number.</returns>
    [Benchmark]
    public uint ReadPackedNumberAndBytes()
    {
        var reader = new PackedDataReader(PackedData);
        var number = reader.ReadNumber();
        return number ^ reader.ReadBytes(1)[0];
    }

    /// <summary>Reads a bounded metadata string.</summary>
    /// <returns>The decoded string.</returns>
    [Benchmark]
    public string ReadBinaryCMapMetadata()
    {
        var reader = new BinaryCMapStream(MetadataData);
        return reader.ReadString();
    }

    /// <summary>Parses one Unicode mapping through the complete resource parser.</summary>
    /// <returns>The decoded map.</returns>
    [Benchmark]
    public object ParseBinaryCMap() => BinaryCMapReader.Parse(UnicodeData, CancellationToken.None);

    /// <summary>Reads range counts, relative codes and CID deltas in a packed CMap.</summary>
    /// <returns>The decoded map.</returns>
    [Benchmark]
    public object ParsePackedCMap() => PredefinedCMaps.Parse(_fontResources, RangeData);

    /// <summary>Parses the binary charstring's length and copies its bytes.</summary>
    /// <returns>Whether the font parsed.</returns>
    [Benchmark]
    public bool ParseType1Font() => Type1Parser.TryParse(_type1Data, 0, out _);

    /// <summary>Reads a coverage group's bounds and glyph offset without allocating.</summary>
    /// <returns>The glyph.</returns>
    [Benchmark]
    public int LookupTrueTypeCoverage() => TrueTypeCmap.Lookup(CoverageData, 0, 0);

    /// <summary>Checks TrueType location counts before reading into caller-owned storage.</summary>
    /// <returns>Whether the location table is valid.</returns>
    [Benchmark]
    public bool ReadTrueTypeLocations()
    {
        Span<int> locations = stackalloc int[LocationEntries];
        return TrueTypeLocations.TryRead(LocationData, 0, 1, true, locations);
    }

    /// <summary>Applies a bounded 32-bit extension offset using pooled glyph storage.</summary>
    /// <returns>The substituted glyph.</returns>
    [Benchmark]
    public int ApplyOpenTypeExtension()
    {
        _extensionGlyphs.Clear(1);
        _extensionGlyphs.Add(new(1, 0, 0));
        _extensionEngine.RunStage(ExtensionStage);
        return _extensionGlyphs.Items[0].Glyph;
    }

    /// <summary>Reads a bounded 32-bit mark-set coverage offset.</summary>
    /// <returns>Whether the mark set contains the glyph.</returns>
    [Benchmark]
    public bool ReadOpenTypeMarkSet() => _markSets.IsInMarkSet(0, 1);
}
