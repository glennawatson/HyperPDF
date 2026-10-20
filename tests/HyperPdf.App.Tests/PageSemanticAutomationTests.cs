// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks that a tagged page gives assistive technology its PDF/UA semantics: language changes, figure descriptions, empty cells, artifacts, form fields, annotations and XMP.</summary>
public sealed class PageSemanticAutomationTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>A language change is said on the block that has it, and not on the blocks around it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LanguageChangesAreSaid()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, peer) = await OpenAsync(test, main);
        try
        {
            var blocks = await BlocksAsync(peer);
            var french = blocks.Single(static block => block.GetName() == AccessibleTestPdf.FrenchText);
            var heading = blocks.Single(static block => block.GetName() == "Overview");

            await Assert.That(french.GetHelpText()).Contains("French");
            await Assert.That(french.Block.Language).IsEqualTo(AccessibleTestPdf.FrenchLanguage);
            await Assert.That(heading.GetHelpText() ?? string.Empty).DoesNotContain("language");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A figure is named by its description and an empty table cell says it is empty, with its place in the table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiguresAndEmptyCellsAreDescribed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, peer) = await OpenAsync(test, main);
        try
        {
            var blocks = await BlocksAsync(peer);
            var figure = blocks.Single(static block => block.GetAutomationControlType() == AutomationControlType.Image);
            var cell = Items(peer).Single(static item => item.Item.Kind == PageSemanticKind.EmptyCell);

            await Assert.That(figure.GetName()).IsEqualTo(AccessibleTestPdf.FigureDescription);
            await Assert.That(cell.GetAutomationControlType()).IsEqualTo(AutomationControlType.DataItem);
            await Assert.That(cell.GetHelpText()).IsEqualTo("table cell, row 1, column 2, empty");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A running header is kept out of the content view, so a screen reader skips it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArtifactsAreLeftOutOfTheContentView()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, peer) = await OpenAsync(test, main);
        try
        {
            var artifact = Items(peer).Single(static item => item.Item.Kind == PageSemanticKind.Artifact);

            await Assert.That(artifact.GetName()).IsEqualTo(AccessibleTestPdf.ArtifactText);
            await Assert.That(artifact.IsContentElement()).IsFalse();
            await Assert.That(artifact.IsControlElement()).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Form fields give their role, label, value, state and formatting, and a field in a switched off layer is off screen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFieldsGiveTheirStateAndTheHiddenOnesAreOffScreen()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, peer) = await OpenAsync(test, main);
        try
        {
            var fields = Items(peer).Where(static item => item.Item.Kind == PageSemanticKind.FormField).ToArray();
            var check = fields.Single(static field => field.GetName() == AccessibleTestPdf.CheckBoxLabel);
            var notes = fields.Single(static field => field.GetName() == AccessibleTestPdf.RichFieldLabel);
            var hidden = fields.Single(static field => field.GetName() == AccessibleTestPdf.HiddenFieldLabel);

            await Assert.That(check.GetAutomationControlType()).IsEqualTo(AutomationControlType.CheckBox);
            await Assert.That(check.GetHelpText()).Contains("checked");
            await Assert.That(notes.GetAutomationControlType()).IsEqualTo(AutomationControlType.Edit);
            await Assert.That(((IValueProvider)notes).Value).IsEqualTo("Plain");
            await Assert.That(notes.GetHelpText()).Contains("formatted text, bold, italic");
            await Assert.That(hidden.IsOffscreen()).IsTrue();
            await Assert.That(check.IsOffscreen()).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A link or note with a description is named by it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsAreNamedByTheirDescription()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, peer) = await OpenAsync(test, main);
        try
        {
            var link = Items(peer).Single(static item => item.GetName() == AccessibleTestPdf.LinkText);

            await Assert.That(link.GetAutomationControlType()).IsEqualTo(AutomationControlType.Hyperlink);
            await Assert.That(Items(peer).Any(static item => item.GetName() == AccessibleTestPdf.NoteText)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every XMP property of the catalog is a row in the properties window, with a name for assistive technology.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PropertiesListEveryXmpProperty()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, _) = await OpenAsync(test, main);
        try
        {
            var properties = new PropertiesViewModel(main.SelectedTab!);
            var rows = properties.Entries.ToDictionary(static entry => entry.Name, static entry => entry.Value);

            await Assert.That(rows["XMP dc:title"]).IsEqualTo("Quarterly Report");
            await Assert.That(rows["XMP dc:creator"]).IsEqualTo("Ann; Bob");
            await Assert.That(rows["XMP {http://example.com/ns/custom/}Department"]).IsEqualTo("Finance");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Opens the rich page in a window.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>The window and the page canvas' peer.</returns>
    private static async Task<(Window Window, AutomationPeer Peer)> OpenAsync(TestServices test, MainViewModel main)
    {
        var path = Path.Combine(test.Directory, "accessible.pdf");
        await File.WriteAllBytesAsync(path, AccessibleTestPdf.CreateRich());
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
        return (window, ControlAutomationPeer.CreatePeerForElement(canvas));
    }

    /// <summary>Waits for the page's reading blocks, which are worked out in the background.</summary>
    /// <param name="peer">The page canvas' peer.</param>
    /// <returns>The block peers.</returns>
    private static async Task<ReadingBlockAutomationPeer[]> BlocksAsync(AutomationPeer peer)
    {
        _ = await UiWait.UntilAsync(() => peer.GetChildren().OfType<ReadingBlockAutomationPeer>().Any(static block => block.GetName() == AccessibleTestPdf.FrenchText));
        return [.. peer.GetChildren().OfType<ReadingBlockAutomationPeer>()];
    }

    /// <summary>Gets the peers of the page's empty cells, artifacts, form fields and annotations.</summary>
    /// <param name="peer">The page canvas' peer.</param>
    /// <returns>The item peers.</returns>
    private static PageSemanticAutomationPeer[] Items(AutomationPeer peer) => [.. peer.GetChildren().OfType<PageSemanticAutomationPeer>()];
}
