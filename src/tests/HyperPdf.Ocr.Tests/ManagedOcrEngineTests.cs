// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the managed recogniser end to end on a page drawn by HyperPDF.</summary>
public sealed class ManagedOcrEngineTests
{
    /// <summary>The language the app ships.</summary>
    private const string Language = "eng";

    /// <summary>The scan resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The least confidence a word of a clean, drawn page may have.</summary>
    private const float CleanConfidence = 80F;

    /// <summary>The name of a language no one ships.</summary>
    private const string UnknownLanguage = "zzz";

    /// <summary>The bytes of the damaged data file.</summary>
    private static readonly byte[] DamagedData = "not a language data file"u8.ToArray();

    /// <summary>The words of the test sentence the recogniser must find.</summary>
    private static readonly string[] ExpectedWords = ["quick", "brown", "fox", "jumps", "lazy"];

    /// <summary>A page of text is read word for word, with each word placed on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsADrawnPage()
    {
        using var engine = new ManagedOcrEngine(Language);
        var page = RenderPage(out var width, out var height);
        var words = new List<OcrWord>();

        engine.Recognize(page, width, height, ScanScale, words);

        var text = string.Join(' ', words.ConvertAll(static word => word.Text));
        await Assert.That(engine.Status).IsEqualTo(OcrEngineStatus.Ready);
        foreach (var expected in ExpectedWords)
        {
            await Assert.That(text).Contains(expected);
        }

        foreach (var word in words)
        {
            await Assert.That(word.Confidence).IsGreaterThanOrEqualTo(CleanConfidence);
            await Assert.That(word.Bounds.Left).IsGreaterThanOrEqualTo(0F);
            await Assert.That(word.Bounds.Right).IsLessThanOrEqualTo(width / ScanScale);
            await Assert.That(word.Bounds.Bottom).IsLessThanOrEqualTo(height / ScanScale);
        }
    }

    /// <summary>Light text on a dark page is read as well as dark text on a light one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsLightTextOnDark()
    {
        using var engine = new ManagedOcrEngine(Language);
        var page = RenderPage(out var width, out var height);
        for (var i = 0; i < page.Length; i++)
        {
            page[i] = (byte)(byte.MaxValue - page[i]);
        }

        var words = new List<OcrWord>();
        engine.Recognize(page, width, height, ScanScale, words);

        await Assert.That(string.Join(' ', words.ConvertAll(static word => word.Text))).Contains(ExpectedWords[0]);
    }

    /// <summary>A language with no data reports it and recognises nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingLanguageIsReported()
    {
        using var engine = new ManagedOcrEngine(UnknownLanguage);
        var words = new List<OcrWord>();

        engine.Recognize(new byte[1], 1, 1, 1F, words);

        await Assert.That(engine.IsAvailable).IsFalse();
        await Assert.That(engine.Status).IsEqualTo(OcrEngineStatus.LanguageMissing);
        await Assert.That(words.Count).IsEqualTo(0);
    }

    /// <summary>Data that is not a language data file is reported as failing to start.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDataIsReported()
    {
        var directory = Directory.CreateTempSubdirectory("hyperpdf-ocr-");
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "bad.traineddata"), DamagedData);

            using var engine = new ManagedOcrEngine("bad", directory.FullName);

            await Assert.That(engine.IsAvailable).IsFalse();
            await Assert.That(engine.Status).IsEqualTo(OcrEngineStatus.StartFailed);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>After it is disposed the engine recognises nothing and does not fail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposedEngineRecognisesNothing()
    {
        var engine = new ManagedOcrEngine(Language);
        var page = RenderPage(out var width, out var height);
        engine.Dispose();
        var words = new List<OcrWord>();

        engine.Recognize(page, width, height, ScanScale, words);

        await Assert.That(words.Count).IsEqualTo(0);
    }

    /// <summary>Renders the first page of a test document at scanning resolution.</summary>
    /// <param name="width">Receives the width in pixels.</param>
    /// <param name="height">Receives the height in pixels.</param>
    /// <returns>The greyscale pixels.</returns>
    private static byte[] RenderPage(out int width, out int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-ocr-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, TestPdf.Create(1));
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var size = document.GetPageSize(0);
            width = (int)MathF.Ceiling(size.Width * ScanScale);
            height = (int)MathF.Ceiling(size.Height * ScanScale);
            var pixels = new byte[width * height * BytesPerPixel];
            _ = document.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
            var grey = new byte[width * height];
            OcrRunner.ToGrey(pixels, grey);
            return grey;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
