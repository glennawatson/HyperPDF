// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests that the sidebar annotation list rebuilds only when it is out of date and patches itself in place.</summary>
public sealed class AnnotationListRefreshTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>Two items, or two pages.</summary>
    private const int Two = 2;

    /// <summary>Three items.</summary>
    private const int Three = 3;

    /// <summary>The longest the filter text may take to apply, in milliseconds.</summary>
    private const int FilterWaitMilliseconds = 5000;

    /// <summary>The note text.</summary>
    private const string RememberText = "Remember this";

    /// <summary>How often the filter result is checked.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>Where the notes go.</summary>
    private static readonly PagePoint NoteAt = new(300, 300);

    /// <summary>Verifies showing the panel again does not rebuild an up-to-date list, and an invalidated one is rebuilt with the same contents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RebuildsOnlyWhenInvalidated()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("list-refresh.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Note"));
        await annotations.AddNoteAsync(0, NoteAt);
        await annotations.AddNoteAsync(1, NoteAt);
        _ = annotations.ShowItems();
        var first = annotations.Items[0];

        var rebuiltWhenCurrent = annotations.ShowItems();
        annotations.InvalidateItems();
        var rebuiltWhenStale = annotations.ShowItems();

        await Assert.That(rebuiltWhenCurrent).IsFalse();
        await Assert.That(rebuiltWhenStale).IsTrue();
        await Assert.That(annotations.Items.Count).IsEqualTo(Two);
        await Assert.That(annotations.Items[0].Annotation).IsEqualTo(first.Annotation);
        await Assert.That(annotations.VisibleItems.Count).IsEqualTo(Two);
    }

    /// <summary>Verifies adding an annotation on a later page keeps the rows already shown, in the same order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsUnchangedRowsWhenOneIsAdded()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("list-patch.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Note"));
        await annotations.AddNoteAsync(0, NoteAt);
        await annotations.AddNoteAsync(Two, NoteAt);
        var firstRow = annotations.VisibleItems[0];
        var lastRow = annotations.VisibleItems[1];

        await annotations.AddNoteAsync(1, NoteAt);

        await Assert.That(annotations.VisibleItems.Count).IsEqualTo(Three);
        await Assert.That(ReferenceEquals(annotations.VisibleItems[0], firstRow)).IsTrue();
        await Assert.That(annotations.VisibleItems[1].Annotation.PageIndex).IsEqualTo(1);
        await Assert.That(annotations.VisibleItems[Two].Annotation.PageIndex).IsEqualTo(Two);
        await Assert.That(ReferenceEquals(annotations.VisibleItems[Two], lastRow)).IsTrue();
    }

    /// <summary>Verifies the type filter applies at once and the text filter finds notes once typing pauses.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiltersByTypeAndText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("list-filter.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput(RememberText));
        await annotations.AddNoteAsync(0, NoteAt);
        await annotations.AddNoteAsync(1, NoteAt);

        annotations.FilterType = Two;
        var notesShown = annotations.VisibleItems.Count;
        annotations.FilterType = 0;
        annotations.FilterText = "zzz-not-there";
        using var timeout = new CancellationTokenSource(FilterWaitMilliseconds);
        using var poll = new PeriodicTimer(PollInterval);
        while ((annotations.VisibleItems.Count != 0 || !annotations.NoMatches) && !timeout.IsCancellationRequested)
        {
            _ = await poll.WaitForNextTickAsync(CancellationToken.None);
        }

        await Assert.That(notesShown).IsEqualTo(Two);
        await Assert.That(annotations.VisibleItems.Count).IsEqualTo(0);
        await Assert.That(annotations.NoMatches).IsTrue();
    }

    /// <summary>Verifies an item builds its filter text once and keeps it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CachesSearchText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("list-search-text.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput(RememberText));
        await annotations.AddNoteAsync(0, NoteAt);
        var item = annotations.Items[0];

        await Assert.That(ReferenceEquals(item.SearchText, item.SearchText)).IsTrue();
        await Assert.That(item.SearchText.Contains(RememberText, StringComparison.Ordinal)).IsTrue();
    }
}
