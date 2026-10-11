// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Ocr;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="TextRecognitionViewModel"/>.</summary>
public sealed class TextRecognitionViewModelTests
{
    /// <summary>The scan resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The pages in the text document.</summary>
    private const int TextPages = 2;

    /// <summary>Verifies recognising a scanned tab gives it text, marks it unsaved and says what happened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesScannedTab()
    {
        using var test = new TestServices();
        RequireTesseract(test);
        var path = Path.Combine(test.Directory, "scan.pdf");
        await File.WriteAllBytesAsync(path, CreateScan(test.CreateDocument("source.pdf", 1)));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var before = tab.TryGetDocument()!.GetCharacterCount(0);

        _ = await tab.TextRecognition.RecognizeCommand.Execute().ToTask();

        await Assert.That(before).IsEqualTo(0);
        await Assert.That(tab.TryGetDocument()!.GetCharacterCount(0)).IsGreaterThan(0);
        await Assert.That(tab.HasUnsavedChanges).IsTrue();
        await Assert.That(tab.Notice).IsEqualTo(TextRecognitionViewModel.Describe(1, 0, 1, false));
        await Assert.That(tab.TextRecognition.IsRunning).IsFalse();
    }

    /// <summary>
    /// Verifies a fresh profile, with nothing downloaded, recognises English with the Tesseract and English data shipped
    /// in the app's own folder, never a system copy.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FreshProfileRecognisesEnglishWithShippedTesseract()
    {
        using var test = new TestServices();
        RequireTesseract(test);
        var packs = test.Services.Ocr.LanguageDirectory;
        var path = Path.Combine(test.Directory, "fresh.pdf");
        await File.WriteAllBytesAsync(path, CreateScan(test.CreateDocument("fresh-source.pdf", 1)));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;

        _ = await tab.TextRecognition.RecognizeCommand.Execute().ToTask();

        var bundled = TesseractEngine.BundledLibraryPath ?? string.Empty;
        await Assert.That(bundled).StartsWith(AppContext.BaseDirectory);
        await Assert.That(TesseractEngine.LibraryPath).IsEqualTo(bundled);
        await Assert.That(test.Services.Ocr.BundledLanguageDirectory).IsEqualTo(Path.Combine(AppContext.BaseDirectory, "tessdata"));
        await Assert.That(Directory.Exists(packs) ? Directory.GetFiles(packs) : []).IsEmpty();
        await Assert.That(tab.TextRecognition.IsLanguageBarOpen).IsFalse();
        await Assert.That(tab.TryGetDocument()!.GetCharacterCount(0)).IsGreaterThan(0);
        await Assert.That(tab.Notice).IsEqualTo(TextRecognitionViewModel.Describe(1, 0, 1, false));
        var loaded = await LoadedTesseractFilesAsync();
        await Assert.That(loaded.Count > 0 || !OperatingSystem.IsLinux()).IsTrue();
        await Assert.That(loaded.TrueForAll(line => line.EndsWith(bundled, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Verifies a document that already has text is left alone, with a plain explanation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesTextDocumentAlone()
    {
        using var test = new TestServices();
        RequireTesseract(test);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("text.pdf", TextPages)]);
        var tab = main.SelectedTab!;

        _ = await tab.TextRecognition.RecognizeCommand.Execute().ToTask();

        await Assert.That(tab.HasUnsavedChanges).IsFalse();
        await Assert.That(tab.Notice).IsEqualTo("Every page already has text, so there was nothing to recognise.");
    }

    /// <summary>Verifies the outcome messages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesOutcome()
    {
        await Assert.That(TextRecognitionViewModel.Describe(1, 0, 1, false)).StartsWith("Text recognised on 1 page.");
        await Assert.That(TextRecognitionViewModel.Describe(TextPages, 0, TextPages, true)).StartsWith("Stopped. Text recognised on 2 pages.");
        await Assert.That(TextRecognitionViewModel.Describe(0, 1, TextPages, false)).IsEqualTo("No text was found on the scanned pages.");
    }

    /// <summary>Checks Tesseract starts, skipping the test only on runtimes the app ships no Tesseract for.</summary>
    /// <param name="test">The services.</param>
    /// <exception cref="InvalidOperationException">The shipped Tesseract did not start.</exception>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">No Tesseract ships for this runtime.</exception>
    private static void RequireTesseract(TestServices test)
    {
        using var engine = test.Services.CreateOcrEngine();
        if (engine.IsAvailable)
        {
            return;
        }

        if (TesseractEngine.BundledLibraryPath is { } bundled)
        {
            throw new InvalidOperationException($"The shipped Tesseract at {bundled} did not start: {engine.Status}.");
        }

        throw new TUnit.Core.Exceptions.SkipTestException("No Tesseract ships for this runtime.");
    }

    /// <summary>Lists the Tesseract files the process has loaded, from its own memory map on Linux.</summary>
    /// <returns>The map lines naming Tesseract; empty on other systems.</returns>
    private static async Task<List<string>> LoadedTesseractFilesAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync("/proc/self/maps");
        return [.. lines.Where(static line => line.Contains("libtesseract", StringComparison.Ordinal))];
    }

    /// <summary>Renders a document's first page at scanning resolution and wraps it in an image-only PDF.</summary>
    /// <param name="sourcePath">The document.</param>
    /// <returns>The scan.</returns>
    private static byte[] CreateScan(string sourcePath)
    {
        using var source = new PdfiumEngine().Open(sourcePath, null);
        var size = source.GetPageSizes()[0];
        var width = (int)MathF.Ceiling(size.Width * ScanScale);
        var height = (int)MathF.Ceiling(size.Height * ScanScale);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = source.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
        var grey = new byte[width * height];
        OcrRunner.ToGrey(pixels, grey);
        return TestPdf.CreateScan(grey, width, height);
    }
}
