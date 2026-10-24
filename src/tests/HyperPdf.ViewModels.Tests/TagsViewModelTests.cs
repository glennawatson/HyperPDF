// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that the tags panel lists a document's tags, changes them, and takes the changes back with undo.</summary>
public sealed class TagsViewModelTests
{
    /// <summary>The tags of the tagged test document: a document, a heading, two paragraphs and a picture.</summary>
    private const int TaggedCount = 5;

    /// <summary>The choice the form shows for an attribute a tag does not have.</summary>
    private const string NotSetChoice = "Not set";

    /// <summary>The depth of a paragraph nested inside another paragraph under the document.</summary>
    private const int NestedDepth = 2;

    /// <summary>The position of the header cell in the table document's list: document, table, row, cell.</summary>
    private const int Three = 3;

    /// <summary>The rows the span test gives the cell.</summary>
    private const int TwoRows = 2;

    /// <summary>The description typed for the picture.</summary>
    private const string Typed = "A thick black bar";

    /// <summary>Lists the tags of a tagged document in reading order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsTheTagsOfATaggedDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-list.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;

        tab.Tags.Refresh();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Tags.IsTagged).IsTrue();
            await Assert.That(tab.Tags.Items.Count).IsEqualTo(TaggedCount);
            await Assert.That(tab.Tags.Items[1].Row).IsEqualTo("Heading 1");
            await Assert.That(tab.Tags.Items[TaggedCount - 1].Row).StartsWith("Picture");
        }
    }

    /// <summary>Reports a document without tags as having none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsAnUntaggedDocumentAsHavingNoTags()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-none.pdf", 1)]);
        var tab = main.SelectedTab!;

        tab.Tags.Refresh();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Tags.IsTagged).IsFalse();
            await Assert.That(tab.Tags.Items.Count).IsEqualTo(0);
        }
    }

    /// <summary>Saves a description and a new type for the chosen tag, then undo takes them back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheFormAndUndoTakesItBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-save.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        tab.Tags.SelectedItem = tab.Tags.Items[TaggedCount - 1];
        tab.Tags.AlternateText = Typed;
        tab.Tags.Language = "fr-CA";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Tags.Items[TaggedCount - 1].Tag.AlternateText).IsEqualTo(Typed);
            await Assert.That(tab.Tags.Items[TaggedCount - 1].Tag.Language).IsEqualTo("fr-CA");
            await Assert.That(tab.Tags.SelectedItem?.Id).IsEqualTo(tab.Tags.Items[TaggedCount - 1].Id);
            await Assert.That(tab.Tags.Status).StartsWith("Saved the changes");
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();
        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.Items[TaggedCount - 1].Tag.AlternateText).IsEqualTo(TestPdf.TaggedFigure);
    }

    /// <summary>Refuses a language that is not a tag, and says why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesALanguageThatIsNotATag()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-lang.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        tab.Tags.SelectedItem = tab.Tags.Items[TaggedCount - 1];
        tab.Tags.Language = "not a tag!";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Tags.Status).Contains("not a valid language tag");
            await Assert.That(tab.Tags.Items[TaggedCount - 1].Tag.Language).IsNull();
        }
    }

    /// <summary>A table header cell shows its scope in the form, the form saves a new one, and undo takes it back; other tags show no scope.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheScopeOfATableHeaderCell()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-scope.pdf", TableDocument())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        var table = tab.Tags.Items[1];
        tab.Tags.SelectedItem = table;
        var tableShowsScope = tab.Tags.IsHeaderCell;
        var header = tab.Tags.Items[Three];
        tab.Tags.SelectedItem = header;
        var before = tab.Tags.SelectedScope;
        tab.Tags.SelectedScope = "Column";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tableShowsScope).IsFalse();
            await Assert.That(before).IsEqualTo(NotSetChoice);
            await Assert.That(tab.Tags.Items[Three].Tag.Scope).IsEqualTo(DocumentTableScope.Column);
            await Assert.That(tab.Tags.SelectedScope).IsEqualTo("Column");
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.Items[Three].Tag.Scope).IsEqualTo(DocumentTableScope.None);
    }

    /// <summary>A cell shows how many rows and columns it covers, the form saves new spans, a bad span is refused with a plain message, and undo takes the change back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheSpansOfATableCell()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-spans.pdf", TableDocument())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        tab.Tags.SelectedItem = tab.Tags.Items[Three];
        var shown = (tab.Tags.IsCell, tab.Tags.RowSpanText);
        tab.Tags.RowSpanText = "0";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();
        var refusal = tab.Tags.Status;
        tab.Tags.RowSpanText = "2";
        tab.Tags.ColumnSpanText = "3";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(shown.IsCell).IsTrue();
            await Assert.That(shown.RowSpanText).IsEqualTo("1");
            await Assert.That(refusal).IsEqualTo("A span is a whole number of 1 or more.");
            await Assert.That(tab.Tags.Items[Three].Tag.RowSpan).IsEqualTo(TwoRows);
            await Assert.That(tab.Tags.Items[Three].Tag.ColumnSpan).IsEqualTo(Three);
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.Items[Three].Tag.ColumnSpan).IsEqualTo(1);
    }

    /// <summary>A custom tag type is added, listed, removed when unused, and undo brings it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAndRemovesACustomTagType()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-roles.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        var standard = tab.Tags.StandardTypes.Contains("Div");
        tab.Tags.NewRoleType = "Sidebar";
        tab.Tags.NewRoleTarget = "Div";

        _ = await tab.Tags.AddRoleMappingCommand.Execute().ToTask();
        var listed = tab.Tags.RoleMappings.Count;
        tab.Tags.RoleMappingIndex = 0;

        _ = await tab.Tags.RemoveRoleMappingCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(standard).IsTrue();
            await Assert.That(listed).IsEqualTo(1);
            await Assert.That(tab.Tags.RoleMappings.Count).IsEqualTo(0);
            await Assert.That(tab.Tags.Status).StartsWith("Removed the mapping");
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.RoleMappings.Count).IsEqualTo(1);
    }

    /// <summary>A list shows how it labels its items, the form saves a new numbering, and undo takes it back; a paragraph shows no numbering.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheNumberingOfAList()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-numbering.pdf", ListDocument())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        tab.Tags.SelectedItem = tab.Tags.Items[0];
        var documentShowsNumbering = tab.Tags.IsList;
        tab.Tags.SelectedItem = tab.Tags.Items[1];
        var before = tab.Tags.SelectedListNumbering;
        tab.Tags.SelectedListNumbering = "Decimal";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(documentShowsNumbering).IsFalse();
            await Assert.That(before).IsEqualTo(NotSetChoice);
            await Assert.That(tab.Tags.Items[1].Tag.ListNumbering).IsEqualTo("Decimal");
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.Items[1].Tag.ListNumbering).IsEqualTo(string.Empty);
    }

    /// <summary>Any tag shows how it sits in the text, the form saves a new placement, and undo takes it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesThePlacementOfATag()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-placement.pdf", ListDocument())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        tab.Tags.SelectedItem = tab.Tags.Items[1];
        var before = tab.Tags.SelectedPlacement;
        tab.Tags.SelectedPlacement = "Block";

        _ = await tab.Tags.ApplyCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Tags.CanChangePlacement).IsTrue();
            await Assert.That(before).IsEqualTo(NotSetChoice);
            await Assert.That(tab.Tags.Items[1].Tag.Placement).IsEqualTo("Block");
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.Items[1].Tag.Placement).IsEqualTo(string.Empty);
    }

    /// <summary>The move list leaves out the chosen tag and the tags inside it, and moving a tag into another changes its depth; undo takes it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesATagInsideAnotherTag()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-move-into.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        var paragraph = tab.Tags.Items[2];
        tab.Tags.SelectedItem = paragraph;
        var targetCount = tab.Tags.MoveTargets.Count;
        tab.Tags.MoveTargetIndex = tab.Tags.MoveTargets.Count - 1;

        _ = await tab.Tags.MoveIntoCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(targetCount).IsEqualTo(TaggedCount - 1);
            await Assert.That(tab.Tags.Status).StartsWith("Moved the");
            await Assert.That(tab.Tags.Items.First(i => i.Id == paragraph.Id).Tag.Depth).IsGreaterThan(paragraph.Tag.Depth);
        }

        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(tab.Tags.Items.First(i => i.Id == paragraph.Id).Tag.Depth).IsEqualTo(paragraph.Tag.Depth);
    }

    /// <summary>Moves a paragraph later so a screen reader reads it after the one below, and offers the move only where a sibling exists.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesAParagraphInTheReadingOrder()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-move.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        var first = tab.Tags.Items[2];
        var second = tab.Tags.Items[3];
        tab.Tags.SelectedItem = first;
        var canMoveEarlier = tab.Tags.CanMoveUp;

        _ = await tab.Tags.MoveDownCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(canMoveEarlier).IsTrue();
            await Assert.That(tab.Tags.Items[2].Id).IsEqualTo(second.Id);
            await Assert.That(tab.Tags.Items[3].Id).IsEqualTo(first.Id);
            await Assert.That(tab.Tags.SelectedItem?.Id).IsEqualTo(first.Id);
        }
    }

    /// <summary>Nesting puts a tag one level deeper and un-nesting puts it back; each says so in words, and the row reads its level aloud.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestAndUnnestChangeTheLevelAndSayIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-nest.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();
        tab.Tags.SelectedItem = tab.Tags.Items[1];
        var firstCanNest = tab.Tags.CanNest;
        tab.Tags.SelectedItem = tab.Tags.Items[3];
        var secondCanNest = tab.Tags.CanNest;

        _ = await tab.Tags.NestCommand.Execute().ToTask();
        var nestedDepth = tab.Tags.Items[3].Tag.Depth;
        var nestedSpeech = tab.Tags.Items[3].SpokenName;
        var nestedStatus = tab.Tags.Status;
        _ = await tab.Tags.UnnestCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(firstCanNest).IsFalse();
            await Assert.That(secondCanNest).IsTrue();
            await Assert.That(nestedDepth).IsEqualTo(NestedDepth);
            await Assert.That(nestedSpeech).Contains("level 3");
            await Assert.That(nestedStatus).Contains("one level deeper");
            await Assert.That(tab.Tags.Items[3].Tag.Depth).IsEqualTo(1);
            await Assert.That(tab.Tags.Status).Contains("one level higher");
        }
    }

    /// <summary>The check lists what a screen reader would struggle with and choosing a problem chooses its tag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckListsProblemsAndChoosingOneChoosesItsTag()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tags-check.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        tab.Tags.Refresh();

        _ = await tab.Tags.CheckCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Tags.Problems.Count).IsGreaterThan(0);
            await Assert.That(tab.Tags.ProblemSummary).IsNotEqualTo("The check has not run yet.");
        }
    }

    /// <summary>Builds a document with one list.</summary>
    /// <returns>The file.</returns>
    private static byte[] ListDocument() => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        "<< /Type /StructElem /S /L /P 4 0 R /Pg 3 0 R /K 0 >>",
    ]);

    /// <summary>Builds a document with a table holding one row with one header cell.</summary>
    /// <returns>The file.</returns>
    private static byte[] TableDocument() => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        "<< /Type /StructElem /S /Table /P 4 0 R /K [7 0 R] >>",
        "<< /Type /StructElem /S /TR /P 6 0 R /K [8 0 R] >>",
        "<< /Type /StructElem /S /TH /P 7 0 R /Pg 3 0 R /K 0 >>",
    ]);
}
