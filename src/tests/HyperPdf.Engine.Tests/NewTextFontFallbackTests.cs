// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that new text in a script the built-in and bundled fonts lack is written with an installed font when the bundled fonts lack its letters.</summary>
public sealed class NewTextFontFallbackTests
{
    /// <summary>Hebrew words.</summary>
    private const string Hebrew = "שלום עולם";

    /// <summary>Arabic words, whose letters join.</summary>
    private const string Arabic = "مرحبا بالعالم";

    /// <summary>A line that mixes Latin letters and Hebrew words.</summary>
    private const string Mixed = "Total שלום עולם done";

    /// <summary>Japanese words.</summary>
    private const string Japanese = "日本語のテキスト";

    /// <summary>Where the line goes, across.</summary>
    private const float Left = 100;

    /// <summary>Where the line goes, down.</summary>
    private const float Top = 500;

    /// <summary>The width of the line's box.</summary>
    private const float Width = 200;

    /// <summary>The size of the text in points.</summary>
    private const float Size = 18;

    /// <summary>Adds a line of Hebrew, which the bundled font has, and the page lists it as one text object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task HebrewIsWrittenWithAnInstalledFont() => AddAndReadAsync(Hebrew);

    /// <summary>Adds a line of Arabic, which the bundled font has, and the page lists it as one text object in the order it was typed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task ArabicReadsBackInTheOrderItWasTyped() => AddAndReadAsync(Arabic);

    /// <summary>Adds a line that mixes directions, and the page reads it back in the order it was typed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task MixedDirectionsReadBackInTheOrderTheyWereTyped() => AddAndReadAsync(Mixed);

    /// <summary>Adds a line of Japanese, which no built-in or bundled font has, and the page lists it as one text object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task JapaneseIsWrittenWithAnInstalledFont() => AddAndReadAsync(Japanese);

    /// <summary>Adds a line to a document opened with the machine's installed fonts and reads the page back.</summary>
    /// <param name="words">The words.</param>
    /// <returns>A task.</returns>
    private static async Task AddAndReadAsync(string words)
    {
        var catalog = new Lazy<FontCatalog>(static () => FontCatalog.Scan(FontCatalog.SystemDirectories()));
        using var test = new HyperPdfTestDocument(TestPdf.CreateTagged(), HyperPdfEngine.WithFontCatalog(catalog));
        if (PdfNewTextRun.NeedsFallback(words, AppearanceFont.Helvetica) && NewTextFonts.Find(test.Document.FontState, words, AppearanceFont.Helvetica) is null)
        {
            Skip.Test("No installed font on this machine has the letters of this script.");
        }

        var draft = new PageObjectDraft(PageObjectDraftKind.Text, new PageRect(Left, Top, Width, Size), true, 0, false, 0, 0F, words, Size, PageTextFamily.Sans, false, false);

        await HyperPdfDocumentPageContentAdding.AddAsync(test.Document, 0, draft, CancellationToken.None);
        var objects = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, 0, CancellationToken.None);

        var found = Array.Exists(objects, o => o.Kind == PageObjectKind.Text && o.Text == words);
        await Assert.That(found).IsTrue().Because(string.Join("|", objects.Select(static o => o.Text)));
    }
}
