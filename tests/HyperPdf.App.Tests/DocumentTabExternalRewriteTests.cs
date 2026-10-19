// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.IO;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks real file watcher reloads without losing reading position or unsaved edits.</summary>
public sealed class DocumentTabExternalRewriteTests
{
    /// <summary>The original page count.</summary>
    private const int OriginalPages = 6;

    /// <summary>The replacement page count.</summary>
    private const int ReplacementPages = 3;

    /// <summary>The page being read before replacement.</summary>
    private const int ReadingPage = 2;

    /// <summary>The position of the unsaved note.</summary>
    private static readonly PagePoint NotePosition = new(100, 100);

    /// <summary>A clean tab automatically reloads externally replaced bytes and keeps a valid current page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanTabAutomaticallyReloadsReplacement()
    {
        var engine = new HighlightedHyperPdfEngine(static () => FormHighlight.Default, static () => RenderSharpness.Default);
        using var test = new TestServices(engine);
        await Assert.That(test.Services.Settings.FileChangeAction).IsEqualTo(FileChangeAction.ReloadAutomatically);
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("reload.pdf", OriginalPages);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var original = tab.TryGetDocument();

        // Check before replacing bytes so an unsafe source fails without faulting the test process.
        await Assert.That(original is HyperPdfDocument { Document.Objects.Source: StreamPdfByteSource }).IsTrue();
        tab.ReportPosition(new(ReadingPage, 0), ReadingPage);
        var destinations = new List<int>();
        using var navigation = tab.Destinations.SubscribeSafe(request => destinations.Add(request.PageIndex), static _ => { });

        await ReplaceAsync(path);

        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded && tab.PageCount == ReplacementPages && destinations.Count != 0)).IsTrue();
        await Assert.That(tab.TryGetDocument()).IsNotEqualTo(original);
        await Assert.That(tab.Thumbnails.Count).IsEqualTo(ReplacementPages);
        await Assert.That(destinations[^1]).IsEqualTo(ReadingPage);
        await Assert.That(tab.HasPendingReload).IsFalse();
        await Assert.That(tab.ErrorMessage).IsNull();
    }

    /// <summary>External replacement prompts instead of silently discarding a tab's unsaved note.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DirtyTabKeepsEditsAndOffersReload()
    {
        var engine = new HighlightedHyperPdfEngine(static () => FormHighlight.Default, static () => RenderSharpness.Default);
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("dirty.pdf", OriginalPages);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var original = tab.TryGetDocument();
        await Assert.That(original is HyperPdfDocument { Document.Objects.Source: StreamPdfByteSource }).IsTrue();
        using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Keep my note"));
        await tab.Annotations.AddNoteAsync(0, NotePosition);
        await Assert.That(tab.Source.HasUnsavedChanges).IsTrue();

        await ReplaceAsync(path);

        await Assert.That(await UiWait.UntilAsync(() => tab.HasPendingReload)).IsTrue();
        await Assert.That(tab.TryGetDocument()).IsEqualTo(original);
        await Assert.That(tab.PageCount).IsEqualTo(OriginalPages);
        await Assert.That(tab.Source.HasUnsavedChanges).IsTrue();
        await Assert.That(tab.Annotations.Items.Single().Annotation.Contents).IsEqualTo("Keep my note");
    }

    /// <summary>Writes a smaller replacement over the watched file.</summary>
    /// <param name="path">The watched path.</param>
    /// <returns>A task.</returns>
    private static Task ReplaceAsync(string path) =>
        File.WriteAllBytesAsync(path, TestPdf.Create(ReplacementPages), CancellationToken.None);
}
