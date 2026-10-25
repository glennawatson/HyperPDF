// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks what the right-click menus offer, which every render head draws the same.</summary>
public sealed class ContextMenusTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The entries in the page menu.</summary>
    private const int PageEntries = 6;

    /// <summary>The entries in the selection menu.</summary>
    private const int SelectionEntries = 7;

    /// <summary>The review states a comment can be set to.</summary>
    private const int ReviewStates = 4;

    /// <summary>Where the note goes.</summary>
    private static readonly PagePoint NoteAt = new(300, 300);

    /// <summary>The page menu offers reading aloud, a note and text, and annotating.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageMenuOffersReadingAndAnnotating()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("page-menu.pdf", Pages)]);
        var tab = main.SelectedTab!;
        PageLocation here = new(0, NoteAt);

        var items = ContextMenus.ForPage(tab, here);

        await Assert.That(items.Count).IsEqualTo(PageEntries);
        await Assert.That(items[0].Header).IsEqualTo("_Read Aloud from Here");
        await Assert.That(items[0].Parameter).IsEqualTo(here);
        await Assert.That(items[1].Kind).IsEqualTo(MenuEntryKind.Separator);
        await Assert.That(items[2].Header).IsEqualTo("Add _Note Here");
        await Assert.That(items[2].IsEnabled).IsEqualTo(tab.Annotations.CanAnnotate);
    }

    /// <summary>The annotation menu names the annotation, offers a status and colours, and ends with deleting it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationMenuOffersStatusColourAndDelete()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("annotation-menu.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("A note"));
        await annotations.AddNoteAsync(0, NoteAt);
        var note = annotations.Items[0].Annotation;

        var items = ContextMenus.ForAnnotation(annotations, note);
        var status = items.Single(static item => item.Header == "_Status");
        var colour = items.Single(static item => item.Header == "C_olour");

        await Assert.That(items[0].Header).IsEqualTo(AnnotationNames.Get(AnnotationKind.Note));
        await Assert.That(items[0].IsEnabled).IsFalse();
        await Assert.That(status.Kind).IsEqualTo(MenuEntryKind.Submenu);
        await Assert.That(status.Children.Count).IsEqualTo(ReviewStates);
        await Assert.That(colour.Children.Count).IsEqualTo(AnnotationColors.All.Count);
        await Assert.That(colour.Children.All(static child => child.Swatch is not null)).IsTrue();
        await Assert.That(items[^1].Header).IsEqualTo($"_Delete {AnnotationNames.Get(AnnotationKind.Note)}");
        await Assert.That(items[^1].Gesture).IsEqualTo("Delete");
    }

    /// <summary>A note with text is edited, and one without is added to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoteMenuWordsEditOrAdd()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("note-words.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Some text"));
        await annotations.AddNoteAsync(0, NoteAt);
        var note = annotations.Items[0].Annotation;

        var items = ContextMenus.ForAnnotation(annotations, note);

        await Assert.That(items.Exists(static item => item.Header == "_Edit Note")).IsTrue();
    }

    /// <summary>The selection menu copies the text, reads from the start and marks it in colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectionMenuCopiesAndMarks()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("selection-menu.pdf", Pages)]);
        var tab = main.SelectedTab!;
        Dictionary<int, List<PageRect>> lines = [];
        CharacterPosition readFrom = new(0, 0);

        var items = ContextMenus.ForSelection(tab, tab.Annotations.UndoCommand, "chosen text", lines, readFrom);
        var highlight = items.Single(static item => item.Header == "_Highlight");

        await Assert.That(items.Count).IsEqualTo(SelectionEntries);
        await Assert.That(items[0].Header).IsEqualTo("_Copy");
        await Assert.That(items[0].Gesture).IsEqualTo("Ctrl+C");
        await Assert.That(items[0].Parameter).IsEqualTo("chosen text");
        await Assert.That(items[1].Parameter).IsEqualTo(readFrom);
        await Assert.That(highlight.Children.Count).IsEqualTo(AnnotationColors.All.Count);
    }

    /// <summary>A separator has no command, and an item keeps its wording.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EntriesKeepWhatTheyWereGiven()
    {
        var separator = MenuEntry.Separator();
        var tinted = MenuEntry.Title("Name").WithSwatch(AnnotationColors.Clay);

        await Assert.That(separator.Kind).IsEqualTo(MenuEntryKind.Separator);
        await Assert.That(separator.Command).IsNull();
        await Assert.That(tinted.Swatch).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(tinted.Header).IsEqualTo("Name");
    }
}
