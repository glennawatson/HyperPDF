// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;
using TUnit.Assertions.Enums;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the viewer runs a page's close actions and the document's additional actions when it reaches those events.</summary>
public sealed class DocumentActionEventTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 3;

    /// <summary>The page shown after the one the document opens at.</summary>
    private const int FirstPage = 1;

    /// <summary>The page shown next.</summary>
    private const int SecondPage = 2;

    /// <summary>The page actions that run when one page turns to another: the close actions, then the open actions.</summary>
    private const int ActionsPerTurn = 2;

    /// <summary>Verifies that turning a page runs the old page's close actions before the new page's open actions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TurningAPage_RunsCloseActionsBeforeOpenActions()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages-closed.pdf", Pages)]);
        var tab = main.SelectedTab!;

        tab.ReportPosition(new(FirstPage, 0), FirstPage);
        _ = await UiWait.UntilAsync(() => engine.PageActions.Count == ActionsPerTurn, static () => "The first turn's actions did not run.");
        tab.ReportPosition(new(SecondPage, 0), SecondPage);
        _ = await UiWait.UntilAsync(() => engine.PageActions.Count == ActionsPerTurn + ActionsPerTurn, () => $"Actions run: {engine.PageActions.Count}.");

        await Assert.That(engine.PageActions.ToArray()).IsEquivalentTo(
            [new RecordedPageAction(0, false), new RecordedPageAction(FirstPage, true), new RecordedPageAction(FirstPage, false), new RecordedPageAction(SecondPage, true)],
            CollectionOrdering.Matching);
    }

    /// <summary>Verifies that saving runs the will-save actions, then the did-save actions once the file is written.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saving_RunsWillSaveThenDidSave()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("saved.pdf", 1)]);
        var tab = main.SelectedTab!;

        var saved = await tab.SaveAsync(Path.Combine(Path.GetDirectoryName(tab.FilePath)!, "saved-copy.pdf"), CancellationToken.None);

        await Assert.That(saved).IsTrue();
        await Assert.That(engine.DocumentEvents.ToArray()).IsEquivalentTo([FormDocumentEvent.WillSave, FormDocumentEvent.DidSave], CollectionOrdering.Matching);
    }

    /// <summary>Verifies that closing a tab runs the will-close actions first.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClosingATab_RunsWillClose()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("closed.pdf", 1)]);

        await main.CloseTabAsync(main.SelectedTab);

        await Assert.That(engine.DocumentEvents.ToArray()).IsEquivalentTo([FormDocumentEvent.WillClose], CollectionOrdering.Matching);
        await Assert.That(main.Tabs.Count).IsEqualTo(0);
    }

    /// <summary>Verifies that printing runs the will-print actions, then the did-print actions once the job is sent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Printing_RunsWillPrintThenDidPrint()
    {
        var printer = new RecordingPrinter();
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(new PrintingPlatform(printer), engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("printed.pdf", 1)]);
        var tab = main.SelectedTab!;
        using var handler = tab.Printing.PrintPreviewInteraction.RegisterHandler(ConfirmWhenReadyAsync);

        _ = await tab.Printing.PrintCommand.Execute().ToTask();

        await Assert.That(engine.DocumentEvents.ToArray()).IsEquivalentTo([FormDocumentEvent.WillPrint, FormDocumentEvent.DidPrint], CollectionOrdering.Matching);
    }

    /// <summary>Verifies that cancelling the print preview runs the will-print actions but not the did-print actions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancellingThePrintPreview_RunsOnlyWillPrint()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter()), engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("cancelled.pdf", 1)]);
        var tab = main.SelectedTab!;
        using var handler = tab.Printing.PrintPreviewInteraction.RegisterHandler(static context => context.SetOutput(false));

        _ = await tab.Printing.PrintCommand.Execute().ToTask();

        await Assert.That(engine.DocumentEvents.ToArray()).IsEquivalentTo([FormDocumentEvent.WillPrint], CollectionOrdering.Matching);
    }

    /// <summary>Waits for the print preview to be ready, then agrees to print.</summary>
    /// <param name="context">The preview question.</param>
    /// <returns>A task.</returns>
    private static async Task ConfirmWhenReadyAsync(IInteractionContext<PrintPreviewViewModel, bool> context)
    {
        var preview = context.Input;
        _ = await UiWait.UntilAsync(
            () => preview.IsValid && !preview.IsBuilding && preview.Destination == PrintDestination.Printer,
            static () => "The print preview was not ready.");
        context.SetOutput(true);
    }
}
