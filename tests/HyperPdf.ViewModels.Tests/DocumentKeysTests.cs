// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks what each key does in the document view, with no toolkit to press them.</summary>
public sealed class DocumentKeysTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The last page, counting from zero.</summary>
    private const int LastPage = Pages - 1;

    /// <summary>Where the note goes.</summary>
    private static readonly PagePoint NoteAt = new(300, 300);

    /// <summary>End goes to the last page and Home to the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HomeAndEndGoToTheEdgePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edges.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = false;

        var end = DocumentKeys.HandleEdgePage(tab, ViewerKey.End);
        var last = tab.Destination.PageIndex;
        var home = DocumentKeys.HandleEdgePage(tab, ViewerKey.Home);
        var first = tab.Destination.PageIndex;

        await Assert.That(end).IsTrue();
        await Assert.That(last).IsEqualTo(LastPage);
        await Assert.That(home).IsTrue();
        await Assert.That(first).IsEqualTo(0);
    }

    /// <summary>In caret mode Home and End stay with the text cursor.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretModeKeepsHomeAndEnd()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-keys.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = true;

        await Assert.That(DocumentKeys.HandleEdgePage(tab, ViewerKey.End)).IsFalse();
    }

    /// <summary>Other keys do not go to a page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherKeysDoNotGoToAPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("other-keys.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = false;

        await Assert.That(DocumentKeys.HandleEdgePage(tab, ViewerKey.Enter)).IsFalse();
    }

    /// <summary>While auto-scrolling, the arrows change the speed and Escape stops it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoScrollKeysChangeSpeedAndStop()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("auto.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.PageTools.IsAutoScrolling = true;
        var before = tab.PageTools.AutoScrollSpeed;

        var faster = DocumentKeys.HandleAutoScroll(tab, ViewerKey.Up, ViewerModifiers.None);
        var afterUp = tab.PageTools.AutoScrollSpeed;
        var slower = DocumentKeys.HandleAutoScroll(tab, ViewerKey.Minus, ViewerModifiers.None);
        var afterDown = tab.PageTools.AutoScrollSpeed;
        var stopped = DocumentKeys.HandleAutoScroll(tab, ViewerKey.Escape, ViewerModifiers.None);

        await Assert.That(faster).IsTrue();
        await Assert.That(afterUp).IsGreaterThan(before);
        await Assert.That(slower).IsTrue();
        await Assert.That(afterDown).IsEqualTo(before);
        await Assert.That(stopped).IsTrue();
        await Assert.That(tab.PageTools.IsAutoScrolling).IsFalse();
    }

    /// <summary>The auto-scroll keys do nothing when it is off or when Control is held.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoScrollKeysNeedAutoScrollAndNoModifier()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("auto-off.pdf", Pages)]);
        var tab = main.SelectedTab!;

        var off = DocumentKeys.HandleAutoScroll(tab, ViewerKey.Up, ViewerModifiers.None);
        tab.PageTools.IsAutoScrolling = true;
        var withControl = DocumentKeys.HandleAutoScroll(tab, ViewerKey.Up, ViewerModifiers.Control);

        await Assert.That(off).IsFalse();
        await Assert.That(withControl).IsFalse();
    }

    /// <summary>Measuring keys are ignored when no measurement is on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasureKeysNeedAMeasurement()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("measure.pdf", Pages)]);
        var tab = main.SelectedTab!;

        var off = DocumentKeys.HandleMeasure(tab.Measure, ViewerKey.Enter);
        tab.Measure.IsOn = true;
        var finish = DocumentKeys.HandleMeasure(tab.Measure, ViewerKey.Escape);
        var other = DocumentKeys.HandleMeasure(tab.Measure, ViewerKey.Left);

        await Assert.That(off).IsFalse();
        await Assert.That(finish).IsTrue();
        await Assert.That(other).IsFalse();
    }

    /// <summary>Placing keys do nothing when no mark is being placed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacementKeysNeedAMark()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("placing.pdf", Pages)]);
        var tab = main.SelectedTab!;

        var used = DocumentKeys.HandlePlacement(tab.FillAndSign, ViewerKey.Enter, ViewerModifiers.None, tab, static (_, _, _) => true);

        await Assert.That(used).IsFalse();
    }

    /// <summary>Form keys do nothing on a document with no form.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormKeysNeedAForm()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("no-form.pdf", Pages)]);

        var result = DocumentKeys.HandleForm(main.SelectedTab!, ViewerKey.Tab, ViewerModifiers.None);

        await Assert.That(result.IsHandled).IsFalse();
        await Assert.That(result.Field).IsNull();
    }

    /// <summary>Delete removes the picked annotation, and other keys leave it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteRemovesThePickedAnnotation()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("delete.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Remove me"));
        await annotations.AddNoteAsync(0, NoteAt);
        annotations.Select(annotations.Items[0].Annotation);

        var other = DocumentKeys.DeleteSelected(annotations, ViewerKey.Enter);
        var deleted = DocumentKeys.DeleteSelected(annotations, ViewerKey.Delete);

        await Assert.That(other).IsFalse();
        await Assert.That(deleted).IsTrue();
        await Assert.That(annotations.Items.Count).IsEqualTo(0);
    }

    /// <summary>Enter edits only a picked text box, so a picked note is left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EnterEditsOnlyTextBoxes()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-text.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("A note"));
        await annotations.AddNoteAsync(0, NoteAt);
        annotations.Select(annotations.Items[0].Annotation);

        var edited = DocumentKeys.EditPickedText(annotations, ViewerKey.Enter, ViewerModifiers.None);

        await Assert.That(edited).IsFalse();
        await Assert.That(annotations.Selected!.Kind).IsEqualTo(AnnotationKind.Note);
    }

    /// <summary>Missing arguments are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingTabIsRefused() =>
        await Assert.That(static () => DocumentKeys.HandleEdgePage(null!, ViewerKey.Home)).Throws<ArgumentNullException>();
}
