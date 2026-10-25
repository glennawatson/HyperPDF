// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the page canvas exposes tagged reading order to a screen reader.</summary>
public sealed class TaggedReadingViewTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The page's screen reader value reads the tagged heading, then the paragraphs in tag order, and skips the artifact.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageScreenReaderValueFollowsTagOrder()
    {
        var value = await ReadPageValueAsync();
        var heading = value.IndexOf(TestPdf.TaggedHeading, StringComparison.Ordinal);
        var first = value.IndexOf(TestPdf.TaggedFirst, StringComparison.Ordinal);
        var second = value.IndexOf(TestPdf.TaggedSecond, StringComparison.Ordinal);

        await Assert.That(heading).IsGreaterThanOrEqualTo(0);
        await Assert.That(first).IsGreaterThan(heading);
        await Assert.That(second).IsGreaterThan(first);
        await Assert.That(value).DoesNotContain(TestPdf.TaggedHeader);
    }

    /// <summary>Opens the tagged test document and reads the page canvas's screen reader value.</summary>
    /// <returns>The value.</returns>
    private static async Task<string> ReadPageValueAsync()
    {
        using var test = new TestServices();
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
