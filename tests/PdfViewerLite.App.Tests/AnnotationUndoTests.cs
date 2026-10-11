// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Checks undo and redo of every annotation change in <see cref="AnnotationsViewModel"/>: adding, deleting, moving,
/// recolouring, restyling, note text, replies and review status; that saving keeps the history; and that new
/// annotations take the chosen colour, line width, text size and author.
/// </summary>
public sealed class AnnotationUndoTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The thick line width.</summary>
    private const float Thick = 4;

    /// <summary>The medium line width, the default.</summary>
    private const float Medium = 2;

    /// <summary>The extra large text size.</summary>
    private const float ExtraLarge = 24;

    /// <summary>The default text size.</summary>
    private const float DefaultText = 12;

    /// <summary>The large text size.</summary>
    private const float Large = 16;

    /// <summary>How far the note is moved, in points.</summary>
    private const float Shift = 40;

    /// <summary>How close positions must be, in points.</summary>
    private const float Tolerance = 2;

    /// <summary>The changes the full round makes, each undone in turn.</summary>
    private const int Changes = 11;

    /// <summary>Two annotations.</summary>
    private const int Two = 2;

    /// <summary>The note's first text.</summary>
    private const string FirstText = "First thought";

    /// <summary>The author chosen in Preferences.</summary>
    private const string Reviewer = "Sam Reviewer";

    /// <summary>The note's edited text.</summary>
    private const string SecondText = "Second thought";

    /// <summary>A shape's first corner.</summary>
    private static readonly PagePoint From = new(100, 300);

    /// <summary>A shape's opposite corner.</summary>
    private static readonly PagePoint To = new(220, 380);

    /// <summary>Where the note goes.</summary>
    private static readonly PagePoint NoteAt = new(300, 420);

    /// <summary>Where the text goes.</summary>
    private static readonly PagePoint TextAt = new(300, 500);

    /// <summary>A drawing.</summary>
    private static readonly PagePoint[] Stroke = [new(100, 600), new(150, 580), new(200, 620)];

    /// <summary>
    /// Every kind of change is undone one at a time in reverse order and redone in order, ending where it started; a
    /// new change after an undo clears what could be redone.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoesAndRedoesEveryChange()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("undo.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        var answers = new Queue<string>([FirstText, SecondText, "Words", "A reply"]);
        using var prompt = annotations.PromptInteraction.RegisterHandler(context => context.SetOutput(answers.Dequeue()));

        _ = annotations.AddShape(0, AnnotationKind.Rectangle, From, To);
        await annotations.AddNoteAsync(0, NoteAt);
        annotations.Recolor(Get(annotations, AnnotationKind.Note), AnnotationColors.Clay);
        await annotations.EditNoteAsync(Get(annotations, AnnotationKind.Note));
        var note = Get(annotations, AnnotationKind.Note);
        var moved = annotations.Move(note, note.Bounds with { Top = note.Bounds.Top + Shift }, false);
        var thick = annotations.Restyle(Get(annotations, AnnotationKind.Rectangle), Thick);
        await annotations.AddTextAsync(0, TextAt);
        var larger = annotations.Resize(Get(annotations, AnnotationKind.TextBox), ExtraLarge);
        await annotations.ReplyAsync(Get(annotations, AnnotationKind.Note));
        var accepted = annotations.SetStatus(Get(annotations, AnnotationKind.Note), ReviewState.Accepted);
        annotations.Delete(Get(annotations, AnnotationKind.Rectangle));
        var finalState = Snapshot(annotations);

        var undone = new List<string>();
        for (var i = 0; i < Changes; i++)
        {
            _ = await annotations.UndoCommand.Execute().ToTask();
            undone.Add(Snapshot(annotations));
        }

        var canUndoAtStart = annotations.CanUndo;
        for (var i = 0; i < Changes; i++)
        {
            _ = await annotations.RedoCommand.Execute().ToTask();
        }

        var redone = Snapshot(annotations);
        _ = await annotations.UndoCommand.Execute().ToTask();
        _ = annotations.AddShape(0, AnnotationKind.Ellipse, From, To);

        await Assert.That(moved && thick && larger && accepted).IsTrue();
        await Assert.That(undone[0]).Contains("Rectangle");
        await Assert.That(undone[1]).DoesNotContain("Accepted");
        await Assert.That(undone[2]).DoesNotContain("A reply");
        await Assert.That(undone[3]).Contains($"Text {DefaultText}");
        await Assert.That(undone[4]).DoesNotContain("Text");
        await Assert.That(undone[5]).Contains($"Rectangle {Medium}");
        await Assert.That(undone[6]).Contains($"top {NoteAt.Y:0}");
        await Assert.That(undone[7]).Contains(FirstText);
        await Assert.That(undone[8]).Contains("Yellow");
        await Assert.That(undone[9]).DoesNotContain("Note");
        await Assert.That(undone[10]).IsEqualTo(string.Empty);
        await Assert.That(canUndoAtStart).IsFalse();
        await Assert.That(redone).IsEqualTo(finalState);
        await Assert.That(finalState).Contains($"top {NoteAt.Y + Shift:0}");
        await Assert.That(finalState).Contains(SecondText);
        await Assert.That(finalState).Contains("Red");
        await Assert.That(annotations.CanUndo).IsTrue();
        await Assert.That(annotations.CanRedo).IsFalse();
    }

    /// <summary>Saving keeps the history: a deletion saved to the file can still be undone, and saving again keeps it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsHistoryAfterSaving()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("history.pdf", Pages);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        _ = annotations.AddShape(0, AnnotationKind.Rectangle, From, To);
        _ = annotations.AddShape(0, AnnotationKind.Ellipse, From, To);
        annotations.Delete(Get(annotations, AnnotationKind.Rectangle));

        var savedWithout = tab.Save(path);
        var kindsSaved = ReadKinds(test, path);
        _ = await annotations.UndoCommand.Execute().ToTask();
        var dirty = tab.HasUnsavedChanges;
        var savedWith = tab.Save(path);

        await Assert.That(savedWithout && savedWith).IsTrue();
        await Assert.That(kindsSaved).IsEquivalentTo([AnnotationKind.Ellipse]);
        await Assert.That(dirty).IsTrue();
        await Assert.That(ReadKinds(test, path)).IsEquivalentTo([AnnotationKind.Rectangle, AnnotationKind.Ellipse]);
    }

    /// <summary>Text marked across two pages is one change: one undo removes both marks and one redo brings them back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoesMarksAcrossPagesTogether()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        var document = tab.TryGetDocument()!;
        var first = new List<PageRect>();
        var second = new List<PageRect>();
        document.GetTextBounds(0, 0, TestPdf.Sentence.Length, first);
        document.GetTextBounds(1, 0, TestPdf.Sentence.Length, second);

        _ = annotations.MarkText(AnnotationKind.Highlight, new Dictionary<int, List<PageRect>> { [0] = first, [1] = second }, AnnotationColors.Sand);
        var marked = annotations.Items.Count;
        _ = await annotations.UndoCommand.Execute().ToTask();
        var afterUndo = annotations.Items.Count;
        _ = await annotations.RedoCommand.Execute().ToTask();

        await Assert.That(marked).IsEqualTo(Two);
        await Assert.That(afterUndo).IsEqualTo(0);
        await Assert.That(annotations.Items.Count).IsEqualTo(Two);
    }

    /// <summary>The chosen colour, line width and text size apply to new drawings, shapes and text, and restyle the picked annotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesTheChosenStyle()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("style.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Styled"));

        _ = await annotations.SetColorCommand.Execute("Green").ToTask();
        _ = await annotations.SetLineWidthCommand.Execute("Thick").ToTask();
        _ = await annotations.SetFontSizeCommand.Execute("Large").ToTask();
        _ = annotations.AddInk(0, Stroke, [Stroke.Length], AnnotationKind.Ink);
        _ = annotations.AddShape(0, AnnotationKind.Rectangle, From, To);
        await annotations.AddTextAsync(0, TextAt);
        var ink = Get(annotations, AnnotationKind.Ink);
        var box = Get(annotations, AnnotationKind.Rectangle);
        var text = Get(annotations, AnnotationKind.TextBox);
        annotations.Select(ink);
        _ = await annotations.SetColorCommand.Execute("Red").ToTask();
        _ = await annotations.SetLineWidthCommand.Execute("Thin").ToTask();
        var restyled = Get(annotations, AnnotationKind.Ink);
        _ = await annotations.UndoCommand.Execute().ToTask();
        _ = await annotations.UndoCommand.Execute().ToTask();
        var undone = Get(annotations, AnnotationKind.Ink);

        await Assert.That(annotations.LineWidthName).IsEqualTo("Line: Thin");
        await Assert.That(annotations.FontSizeName).IsEqualTo("Text: Large");
        await Assert.That(ink.Color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sage));
        await Assert.That(ink.LineWidth).IsEqualTo(Thick);
        await Assert.That(box.LineWidth).IsEqualTo(Thick);
        await Assert.That(text.FontSize).IsEqualTo(Large);
        await Assert.That(text.Color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sage));
        await Assert.That(restyled.Color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Clay));
        await Assert.That(restyled.LineWidth).IsEqualTo(1F);
        await Assert.That(undone.Color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sage));
        await Assert.That(undone.LineWidth).IsEqualTo(Thick);
    }

    /// <summary>New annotations and replies record the author chosen in Preferences, or the user name when none is chosen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordsTheAuthorFromPreferences()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("author.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        using var preferences = new PreferencesViewModel(test.Services);

        _ = annotations.AddShape(0, AnnotationKind.Rectangle, From, To);
        preferences.CommentAuthor = Reviewer;
        _ = annotations.AddShape(0, AnnotationKind.Ellipse, From, To);

        await Assert.That(test.Services.Settings.CommentAuthor).IsEqualTo(Reviewer);
        await Assert.That(Get(annotations, AnnotationKind.Rectangle).Author).IsEqualTo(Environment.UserName);
        await Assert.That(Get(annotations, AnnotationKind.Ellipse).Author).IsEqualTo(Reviewer);
        await Assert.That(annotations.Items[1].Details).StartsWith(Reviewer);
    }

    /// <summary>The person's own stamp words are asked for, kept as the stamp's label and placed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesCustomStamps()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("custom-stamp.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("  SEEN BY ME "));

        _ = await annotations.CustomStampCommand.Execute().ToTask();
        var placed = annotations.AddStamp(0, TextAt);

        await Assert.That(annotations.Tool).IsEqualTo(AnnotationTool.Stamp);
        await Assert.That(annotations.StampButtonLabel).IsEqualTo("Stamp: SEEN BY ME");
        await Assert.That(placed).IsTrue();
        await Assert.That(Get(annotations, AnnotationKind.Stamp).Contents).IsEqualTo("SEEN BY ME");
    }

    /// <summary>Gets the first listed annotation of a kind, as it is now.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The annotation.</returns>
    private static PageAnnotation Get(AnnotationsViewModel annotations, AnnotationKind kind) =>
        annotations.Items.First(item => item.Annotation.Kind == kind).Annotation;

    /// <summary>Describes the listed annotations in a line each: kind, width or size, top, colour, note, replies and status.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <returns>The description.</returns>
    private static string Snapshot(AnnotationsViewModel annotations) => string.Join(
        "\n",
        annotations.Items.Select(static item =>
        {
            var a = item.Annotation;
            var style = a.Kind == AnnotationKind.TextBox ? a.FontSize : a.LineWidth;
            var top = a.Kind == AnnotationKind.Note ? $" top {a.Bounds.Top:0}" : string.Empty;
            return $"{item.KindName} {style}{top} {item.ColorName} {a.Contents} {item.RepliesText} {item.Status}";
        }));

    /// <summary>Opens a saved file and reads the kinds of annotation on its first page.</summary>
    /// <param name="test">The services.</param>
    /// <param name="path">The file.</param>
    /// <returns>The kinds.</returns>
    private static AnnotationKind[] ReadKinds(TestServices test, string path)
    {
        using var document = test.Services.Engine.Open(path, null);
        var annotations = new List<PageAnnotation>();
        ((IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!).GetAnnotations(0, annotations);
        return [.. annotations.Select(static a => a.Kind)];
    }
}
