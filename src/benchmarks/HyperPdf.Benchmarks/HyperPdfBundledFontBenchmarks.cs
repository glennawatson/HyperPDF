// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures creating a one-page document with the fonts embedded in the library, opening those fonts, and the offline
/// font data gate that skips a missing external asset without generating or downloading it.
/// </summary>
public class HyperPdfBundledFontBenchmarks
{
    /// <summary>The page width.</summary>
    private const float PageWidth = 300F;

    /// <summary>The page height.</summary>
    private const float PageHeight = 200F;

    /// <summary>The font size.</summary>
    private const float FontSize = 14F;

    /// <summary>The left edge of the drawn line.</summary>
    private const float Left = 10F;

    /// <summary>The baseline of the drawn line.</summary>
    private const float Baseline = 100F;

    /// <summary>The number of lowercase letters the generated lines choose from.</summary>
    private const int LetterCount = 26;

    /// <summary>The asset the offline gate is asked for; it is never created.</summary>
    private const string MissingAsset = "missing-benchmark-asset.bin";

    /// <summary>The line drawn.</summary>
    private const string Line = "The quick brown fox jumps over the lazy dog. 0123456789";

    /// <summary>Resources in offline mode; the folder holds nothing, so every request is a miss.</summary>
    private readonly PdfFontResources _offline = new(PdfFontDataMode.Offline, null);

    /// <summary>The host-owned font resources shared by created documents.</summary>
    private readonly PdfDrawingServices _services = new(new());

    /// <summary>A folder that does not exist.</summary>
    private readonly string _emptyFolder = Path.Combine(Path.GetTempPath(), "hyperpdf-benchmark-no-font-data");

    /// <summary>The generator the offline gate must never call.</summary>
    private readonly Func<CancellationToken, ValueTask<byte[]>> _never = static _ => throw new InvalidOperationException("The offline gate generated an asset.");

    /// <summary>The call count, whose bits choose which letters a line holds.</summary>
    private int _calls;

    /// <summary>Opens the shared embedded font once so the creation benchmark measures document work.</summary>
    [GlobalSetup]
    public void Setup() => _ = PdfBundledFonts.Open(_services, PdfBundledFontFamily.Sans, false, false);

    /// <summary>Opens an embedded font already opened by these host services.</summary>
    /// <returns>The shared result.</returns>
    [Benchmark]
    public object OpenWarmBundledFont() => PdfBundledFonts.Open(_services, PdfBundledFontFamily.Sans, false, false);

    /// <summary>Creates a one-page document that embeds a subset of the library's Arimo and saves it to memory.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark(Baseline = true)]
    public int CreateDocumentWithBundledFont() => CreateDocument(Line);

    /// <summary>
    /// Creates the same document with a different set of letters every call, so the font subset is new each time and the
    /// compressed font stream cache cannot hit: the cost of a document whose glyphs have not been embedded before.
    /// </summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int CreateDocumentWithNewGlyphSet() => CreateDocument(NextLetters());

    /// <summary>Asks an offline service for a missing external asset: it is recorded as missing and nothing is generated.</summary>
    /// <returns>A task completing when the request has been skipped.</returns>
    [Benchmark]
    public ValueTask OfflineMissingAsset() =>
        FontDataResources.EnsureAsync(_offline.Data, _emptyFolder, "CMaps", MissingAsset, _never, CancellationToken.None);

    /// <summary>Creates a one-page document that embeds a subset of the library's Arimo and saves it to memory.</summary>
    /// <param name="text">The line to draw.</param>
    /// <returns>The bytes written.</returns>
    private int CreateDocument(string text)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(PdfBundledFonts.Open(_services, PdfBundledFontFamily.Sans, false, false).Source!);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(font, FontSize, Left, Baseline, text);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        return creator.ToArray().Length;
    }

    /// <summary>Builds a line of the letters whose bit is set in the call count, so each call has its own glyph set.</summary>
    /// <returns>The line, never empty.</returns>
    private string NextLetters()
    {
        _calls++;
        return string.Create(LetterCount + 1, _calls & ((1 << LetterCount) - 1), static (span, state) =>
        {
            span[0] = 'a';
            for (var letter = 0; letter < LetterCount; letter++)
            {
                span[letter + 1] = (state & (1 << letter)) != 0 ? (char)('a' + letter) : ' ';
            }
        });
    }
}
