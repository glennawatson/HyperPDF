// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Checks a screen reader hears a tagged page in its logical reading order on each engine, and hears the same text from
/// HyperPDF as from PDFium.
/// </summary>
public sealed class TaggedReadingEngineTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Gets both engines, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The engines.</returns>
    public static IEnumerable<TestEngineChoice> Engines() => [TestEngineChoice.Pdfium, TestEngineChoice.HyperPdf];

    /// <summary>The page's screen reader value reads the tagged heading, then the paragraphs in tag order, and skips the artifact.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Engines))]
    public async Task PageValueFollowsTheTags(TestEngineChoice engine)
    {
        var value = await ReadPageValueAsync(engine);
        var heading = value.IndexOf(TestPdf.TaggedHeading, StringComparison.Ordinal);
        var first = value.IndexOf(TestPdf.TaggedFirst, StringComparison.Ordinal);
        var second = value.IndexOf(TestPdf.TaggedSecond, StringComparison.Ordinal);

        await Assert.That(heading).IsGreaterThanOrEqualTo(0);
        await Assert.That(first).IsGreaterThan(heading);
        await Assert.That(second).IsGreaterThan(first);
        await Assert.That(value).DoesNotContain(TestPdf.TaggedHeader);
    }

    /// <summary>HyperPDF gives the screen reader exactly the text PDFium gives it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HyperPdfReadsLikePdfium()
    {
        var expected = await ReadPageValueAsync(TestEngineChoice.Pdfium);
        var actual = await ReadPageValueAsync(TestEngineChoice.HyperPdf);

        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Opens the tagged test document with an engine and reads the page canvas's screen reader value.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The value.</returns>
    private static async Task<string> ReadPageValueAsync(TestEngineChoice engine)
    {
        using var test = new TestServices(engine);
        var path = test.CreateDocument("tagged.pdf", TestPdf.CreateTagged());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var value = (IValueProvider)ControlAutomationPeer.CreatePeerForElement(canvas);
            _ = await UiWait.UntilAsync(() => value.Value?.Contains(TestPdf.TaggedSecond, StringComparison.Ordinal) == true);
            return value.Value ?? string.Empty;
        }
        finally
        {
            window.Close();
        }
    }
}
