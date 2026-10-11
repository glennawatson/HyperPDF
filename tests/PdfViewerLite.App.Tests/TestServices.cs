// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

/// <summary>Application services isolated in a temporary directory, with generated documents.</summary>
internal sealed class TestServices : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class.</summary>
    internal TestServices()
        : this(new FallbackPlatform())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class with a desktop integration.</summary>
    /// <param name="platform">The desktop integration.</param>
    internal TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform)
        : this(platform, new FakeSpeech(true, false))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class with a desktop integration and a fake voice.</summary>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="speech">The fake voice and sound output.</param>
    internal TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform, FakeSpeech speech)
        : this(platform, speech, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class with a fake text recogniser.</summary>
    /// <param name="ocr">The fake recogniser and language packs.</param>
    internal TestServices(FakeOcr ocr)
        : this(new FallbackPlatform(), new FakeSpeech(true, false), ocr)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class that opens documents with one engine.</summary>
    /// <param name="engine">The engine every document opens with.</param>
    internal TestServices(TestEngineChoice engine)
        : this(new FallbackPlatform(), new FakeSpeech(true, false), null, EngineFor(engine))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class that opens documents with an injected engine.</summary>
    /// <param name="engine">The engine every document opens with.</param>
    internal TestServices(IDocumentEngine engine)
        : this(new FallbackPlatform(), new FakeSpeech(true, false), null, engine)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class.</summary>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="speech">The fake voice and sound output.</param>
    /// <param name="ocr">
    /// The fake recogniser, or <see langword="null"/> for the Tesseract and English data shipped with the app, with an
    /// empty pack folder in the test folder and downloads never fetched from the network.
    /// </param>
    /// <remarks>
    /// Documents open with HyperPDF unless a test explicitly injects PDFium as a parity reference.
    /// </remarks>
    private TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform, FakeSpeech speech, FakeOcr? ocr)
        : this(platform, speech, ocr, new HyperPdfEngine())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class.</summary>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="speech">The fake voice and sound output.</param>
    /// <param name="ocr">The fake recogniser, or <see langword="null"/> for the shipped one.</param>
    /// <param name="engine">The engine documents open with.</param>
    private TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform, FakeSpeech speech, FakeOcr? ocr, IDocumentEngine engine)
    {
        Speech = speech;
        Directory = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-app-{Guid.NewGuid():N}");
        _ = System.IO.Directory.CreateDirectory(Directory);
        var packs = Path.Combine(Directory, "tessdata");
        Services = new(new SettingsStore(Path.Combine(Directory, "settings.json")), engine, platform)
        {
            Speech = speech.CreateSetup(Directory),
            Ocr = ocr?.CreateSetup(packs) ?? OcrSetup.CreateDefault() with { LanguageDirectory = packs, DownloadPacks = new FakeOcr(true).DownloadAsync },
            CreateSpellChecker = static () => FakeSpellChecker.Instance,
        };
    }

    /// <summary>Gets the working directory.</summary>
    internal string Directory { get; }

    /// <summary>Gets the fake voice and sound output.</summary>
    internal FakeSpeech Speech { get; }

    /// <summary>Gets the services.</summary>
    internal AppServices Services { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Services.Dispose();
        System.IO.Directory.Delete(Directory, true);
    }

    /// <summary>Opens the selected document and waits until its asynchronous load finishes.</summary>
    /// <param name="main">The viewer.</param>
    /// <param name="items">The documents to open.</param>
    /// <returns>A task that completes when the selected document is loaded.</returns>
    /// <exception cref="InvalidOperationException">Opening did not select a document.</exception>
    internal static async Task OpenAndWaitAsync(MainViewModel main, IEnumerable<string> items)
    {
        main.Open(items);
        var selected = main.SelectedTab ?? throw new InvalidOperationException("Opening did not select a document.");
        _ = await UiWait.UntilAsync(() => selected.IsLoaded, () => selected.ErrorMessage ?? "Document has not loaded.");
    }

    /// <summary>Writes a generated document.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The path.</returns>
    internal string CreateDocument(string name, int pageCount)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllBytes(path, TestPdf.Create(pageCount));
        return path;
    }

    /// <summary>Writes a document from its bytes.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The PDF.</param>
    /// <returns>The path.</returns>
    internal string CreateDocument(string name, byte[] bytes)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Creates the requested engine for a test without a production engine selector.</summary>
    /// <param name="engine">The engine to use.</param>
    /// <returns>The engine.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="engine"/> is not a supported test engine.</exception>
    private static IDocumentEngine EngineFor(TestEngineChoice engine) => engine switch
    {
        TestEngineChoice.HyperPdf => new HyperPdfEngine(),
        TestEngineChoice.Pdfium => new PdfiumEngine(),
        _ => throw new ArgumentOutOfRangeException(nameof(engine)),
    };
}
