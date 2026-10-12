// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures HyperPDF's font layer: loading a font from its dictionary, building and reading cached glyph outlines,
/// reading codes, widths and text, and rendering a page of text. Fonts are an embedded TrueType simple font, the same
/// program as an Identity-H composite font, and Helvetica drawn with a system font.
/// </summary>
public class HyperPdfFontBenchmarks
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The tile edge in pixels.</summary>
    private const int TileSize = 512;

    /// <summary>The render scale (150% at 96 DPI).</summary>
    private const float Scale = 1.5F * 96F / 72F;

    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 40;

    /// <summary>The line of text repeated down the page.</summary>
    private const string Line = "The quick brown fox jumps over the lazy dog. 0123456789 AVWaw";

    /// <summary>The text buffer a code is written into; the font contract's longest text.</summary>
    private const int TextLength = 8;

    /// <summary>The two-byte Shift-JIS codes read by <see cref="PredefinedCMapLookup"/>.</summary>
    private const int ShiftJisCodes = 256;

    /// <summary>The first Shift-JIS lead byte of the kanji rows.</summary>
    private const int FirstLeadByte = 0x88;

    /// <summary>The first Shift-JIS trail byte.</summary>
    private const int FirstTrailByte = 0x40;

    /// <summary>The trail bytes used per lead byte, 0x40 to 0x7E.</summary>
    private const int TrailBytes = 63;

    /// <summary>The CMap read cold by <see cref="LoadPredefinedCMap"/>: the largest Japanese table.</summary>
    private const string ColdCMapName = "UniJIS-UCS2-H";

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[TileSize * TileSize * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The codes of the line, encoded for the font.</summary>
    private byte[] _codes = [];

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The font resources selected by this benchmark's drawing services.</summary>
    private PdfFontResources _fontResources = null!;

    /// <summary>The renderer, already holding the recorded page.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>The font dictionary.</summary>
    private PdfDictionary _fontDictionary = null!;

    /// <summary>The font, loaded once.</summary>
    private PdfFont _font = null!;

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The Shift-JIS string read by <see cref="PredefinedCMapLookup"/>.</summary>
    private byte[] _shiftJis = [];

    /// <summary>The cached 90ms-RKSJ-H CMap.</summary>
    private CompositeCMap _shiftJisMap = null!;

    /// <summary>The cached Adobe-Japan1 CID-to-Unicode table.</summary>
    private CidToUnicodeTable _japan1 = null!;

    /// <summary>Gets or sets the font kind: TrueType, Composite or Standard.</summary>
    [Params("TrueType", "Composite", "Standard")]
    public string Kind { get; set; } = "TrueType";

    /// <summary>Builds the document, loads the font and records the page.</summary>
    /// <returns>A task completing when the requested resources are cached.</returns>
    /// <exception cref="InvalidOperationException">The font did not load.</exception>
    [GlobalSetup]
    public async Task Setup()
    {
        _codes = EncodeLine(Kind);
        _pdf = CreateDocument(Kind, _codes);
        _document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        _fontResources = _document.DrawingServices.FontResources;
        await PdfDocumentPages.PrefetchPageAsync(_document, 0, CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_fontResources, ColdCMapName, CancellationToken.None);
        await PredefinedCMaps.EnsureAsync(_fontResources, "90ms-RKSJ-H", CancellationToken.None);
        await CidToUnicodeTable.EnsureAsync(_fontResources, CjkScript.Japanese, CancellationToken.None);
        await BundledFaces.EnsureAsync(_fontResources, BundledFamily.Sans, false, false, CancellationToken.None);
        await BundledFaces.EnsureAsync(_fontResources, BundledFamily.Sans, true, false, CancellationToken.None);
        _renderer = new(_document);
        var fonts = PdfDocumentPages.GetPage(_document, 0).Resources?.GetDictionary(KnownName.Font) ?? throw new InvalidOperationException("No fonts.");
        _fontDictionary = fonts.Get(fonts.GetKeyAt(0)).AsDictionary() ?? throw new InvalidOperationException("No font.");
        _font = PdfFontLoader.Load(_fontDictionary) ?? throw new InvalidOperationException("The font did not load.");
        _ = OutlineCodes();
        _ = RenderTile();
        _shiftJis = new byte[ShiftJisCodes * (1 + 1)];
        for (var i = 0; i < ShiftJisCodes; i++)
        {
            _shiftJis[i * (1 + 1)] = (byte)(FirstLeadByte + (i / TrailBytes));
            _shiftJis[(i * (1 + 1)) + 1] = (byte)(FirstTrailByte + (i % TrailBytes));
        }

        _shiftJisMap = PredefinedCMaps.Get(_fontResources, "90ms-RKSJ-H"u8);
        _japan1 = CidToUnicodeTable.Get(_fontResources, CjkScript.Japanese) ?? throw new InvalidOperationException("No Adobe-Japan1 table.");
    }

    /// <summary>Releases the renderer and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Prepares a page whose font data is already cached.</summary>
    /// <returns>The completed preparation.</returns>
    [Benchmark]
    public ValueTask WarmPagePreparation() => PdfDocumentPages.PrefetchPageAsync(_document, 0, CancellationToken.None);

    /// <summary>Prefetches the same page bytes without font preparation.</summary>
    /// <returns>The page.</returns>
    [Benchmark]
    public ValueTask<PdfPage> WarmPageBytes() => PdfDocumentPages.GetPageAsync(_document, 0, CancellationToken.None);

    /// <summary>Loads the font from its dictionary: descriptor, program, encoding and widths.</summary>
    /// <returns>The font.</returns>
    [Benchmark]
    public PdfFont? LoadFont() => PdfFontLoader.Load(_fontDictionary);

    /// <summary>Loads the font and builds the outline of every code of the line.</summary>
    /// <returns>The points in the outlines.</returns>
    [Benchmark]
    public int LoadFontAndBuildOutlines()
    {
        var font = PdfFontLoader.Load(_fontDictionary)!;
        return OutlineCodes(font);
    }

    /// <summary>Reads the cached outline of every code of the line.</summary>
    /// <returns>The points in the outlines.</returns>
    [Benchmark]
    public int WarmOutlines() => OutlineCodes();

    /// <summary>Reads every code of the line with its width and text, as the interpreter does per glyph.</summary>
    /// <returns>A sum so the work is kept.</returns>
    [Benchmark]
    public float CodesWidthsAndText()
    {
        Span<char> text = stackalloc char[TextLength];
        ReadOnlySpan<byte> bytes = _codes;
        float total = 0;
        while (!bytes.IsEmpty)
        {
            var used = _font.ReadCode(bytes, out var code);
            total += _font.GetWidth(code) + _font.GetUnicode(code, text);
            bytes = bytes[used..];
        }

        return total;
    }

    /// <summary>Reads a cached open font program from disk and parses it, as on first use of a substituted font.</summary>
    /// <returns>The face.</returns>
    [Benchmark]
    public object? LoadBundledFace() => BundledFaces.Read(BundledFamily.Sans, false, false);

    /// <summary>Chooses the substitute for Helvetica-Bold once the bundled face is cached, as every non-embedded font load does.</summary>
    /// <returns>The face.</returns>
    [Benchmark]
    public object MatchBundledFace() => _document.DrawingServices.FontMatcher.Match(new("Helvetica-Bold", StandardFont.HelveticaBold, FontFlags.Nonsymbolic, 0, CjkScript.None));

    /// <summary>Reads a predefined CMap from disk: decompresses and decodes UniJIS-UCS2-H, as on first use.</summary>
    /// <returns>The CMap.</returns>
    [Benchmark]
    public object? LoadPredefinedCMap() => PredefinedCMaps.Load(_fontResources, ColdCMapName);

    /// <summary>Reads the Adobe-Japan1 CID-to-Unicode table from disk, as on first use.</summary>
    /// <returns>The table.</returns>
    [Benchmark]
    public object LoadCidToUnicode() => CidToUnicodeTable.Read(CjkScript.Japanese);

    /// <summary>Reads Shift-JIS codes through 90ms-RKSJ-H, maps each to its CID and the CID to Unicode.</summary>
    /// <returns>A sum so the work is kept.</returns>
    [Benchmark]
    public int PredefinedCMapLookup()
    {
        ReadOnlySpan<byte> bytes = _shiftJis;
        var cmap = _shiftJisMap;
        var total = 0;
        while (!bytes.IsEmpty)
        {
            var used = cmap.Map.ReadCode(bytes, out var code);
            total += cmap.ToCid(code) + cmap.ToUnicode(code, _japan1);
            bytes = bytes[used..];
        }

        return total;
    }

    /// <summary>Renders a tile of the recorded text page.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool WarmTextTile() => RenderTile();

    /// <summary>Opens the document and renders its first tile, which loads the font and builds its outlines.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool ColdTextTile()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));
    }

    /// <summary>Encodes the line for a font kind: one byte per character, or two-byte glyph ids for the composite font.</summary>
    /// <param name="kind">The font kind.</param>
    /// <returns>The string bytes.</returns>
    private static byte[] EncodeLine(string kind)
    {
        if (kind != "Composite")
        {
            return "The quick brown fox jumps over the lazy dog. 0123456789 AVWaw"u8.ToArray();
        }

        var codes = new byte[Line.Length * (1 + 1)];
        for (var i = 0; i < Line.Length; i++)
        {
            // The generated test font gives printable ASCII glyph ids from 1, so a character's glyph id is its code less 31.
            codes[(i * (1 + 1)) + 1] = (byte)(Line[i] - ' ' + 1);
        }

        return codes;
    }

    /// <summary>Creates the text document.</summary>
    /// <param name="kind">The font kind.</param>
    /// <param name="codes">The encoded line.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateDocument(string kind, byte[] codes)
    {
        var content = new StringBuilder("BT /F1 11 Tf 14 TL 20 780 Td ");
        var hex = Convert.ToHexString(codes);
        for (var i = 0; i < Lines; i++)
        {
            _ = content.Append('<').Append(hex).Append("> Tj T* ");
        }

        _ = content.Append("ET");
        string[] page =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
        ];
        return MiniPdf.Build([.. page, .. FontObjects(kind)]);
    }

    /// <summary>Writes the font objects, numbered from 5.</summary>
    /// <param name="kind">The font kind.</param>
    /// <returns>The object bodies.</returns>
    private static string[] FontObjects(string kind)
    {
        const string metrics = "/FontBBox [0 -200 1000 900] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80";
        var program = MiniPdf.Stream(string.Empty, Encoding.Latin1.GetString(TestFont.Create()));
        return kind switch
        {
            "Standard" => ["<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"],
            "Composite" =>
            [
                "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [6 0 R] >>",
                "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Test /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 7 0 R /DW 500 >>",
                $"<< /Type /FontDescriptor /FontName /Test /Flags 4 {metrics} /FontFile2 8 0 R >>",
                program,
            ],
            _ =>
            [
                "<< /Type /Font /Subtype /TrueType /BaseFont /Test /Encoding /WinAnsiEncoding /FontDescriptor 6 0 R >>",
                $"<< /Type /FontDescriptor /FontName /Test /Flags 32 {metrics} /FontFile2 7 0 R >>",
                program,
            ],
        };
    }

    /// <summary>Reads the cached outline of every code of the line from the loaded font.</summary>
    /// <returns>The points in the outlines.</returns>
    private int OutlineCodes() => OutlineCodes(_font);

    /// <summary>Reads the outline of every code of the line.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The points in the outlines.</returns>
    private int OutlineCodes(PdfFont font)
    {
        ReadOnlySpan<byte> bytes = _codes;
        var points = 0;
        while (!bytes.IsEmpty)
        {
            var used = font.ReadCode(bytes, out var code);
            points += font.GetOutline(code)?.PointCount ?? 0;
            bytes = bytes[used..];
        }

        return points;
    }

    /// <summary>Renders the top-left tile.</summary>
    /// <returns>Whether the tile rendered.</returns>
    private bool RenderTile() =>
        _renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));
}
