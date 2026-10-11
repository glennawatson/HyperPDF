// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for printing through the desktop's print service.</summary>
public sealed class PrintTests
{
    /// <summary>The copies asked for.</summary>
    private const int Copies = 3;

    /// <summary>The size of the printed annotation text.</summary>
    private const float AnnotationTextSize = 12F;

    /// <summary>The pages in the range test document.</summary>
    private const int RangePages = 6;

    /// <summary>The index of two pages per sheet in the choices.</summary>
    private const int TwoPerSheet = 1;

    /// <summary>The booklet's place in the layout list.</summary>
    private const int BookletLayout = 1;

    /// <summary>The 2 × 2 poster's place in the layout list.</summary>
    private const int PosterLayout = 2;

    /// <summary>The sheets each page of a 2 × 2 poster takes.</summary>
    private const int PosterSheetsPerPage = 4;

    /// <summary>The printed sides of each booklet sheet.</summary>
    private const int BookletSides = 2;

    /// <summary>Four pages at two per sheet.</summary>
    private const int ExpectedSheets = 2;

    /// <summary>How many times to check a condition.</summary>
    private const int WaitSteps = 200;

    /// <summary>The pause between checks.</summary>
    private static readonly TimeSpan WaitStep = TimeSpan.FromMilliseconds(25);

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>Verifies the printed copy carries the annotations, is handed over with the file name, and is removed afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsCopyWithAnnotations()
    {
        var printer = new RecordingPrinter();
        using var test = new TestServices(new PrintingPlatform(printer));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("print.pdf", 1)]);
        var tab = main.SelectedTab!;
        _ = ((IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IAnnotationEditor))!).AddText(
            0,
            NoteAt,
            "Printed note",
            AnnotationTextSize,
            AnnotationColors.Sand,
            AnnotationKind.TextBox);
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(ConfirmWhenReadyAsync);

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(printer.Title).IsEqualTo("print.pdf");
        await Assert.That(printer.PageText).Contains("Printed note");
        await Assert.That(File.Exists(printer.Path)).IsFalse();
        await Assert.That(printer.UsedDialog).IsFalse();
        await Assert.That(printer.Options.Printer).IsEqualTo(RecordingPrinter.PrinterQueue);
        await Assert.That(tab.Notice).IsEqualTo($"Sent to {RecordingPrinter.PrinterName}.");
    }

    /// <summary>Verifies a desktop without printing says so plainly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaysWhenPrintingIsUnavailable()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("noprint.pdf", 1)]);
        var tab = main.SelectedTab!;
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(static context =>
        {
            _ = context.Input.SystemDialogCommand.Execute().Subscribe();
            context.SetOutput(true);
        });

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(tab.Notice).IsEqualTo("Printing is not available on this desktop. Choose Save as PDF instead.");
    }

    /// <summary>Verifies a custom range and pages per sheet change what prints, shown before printing.</summary>
    /// <param name="binding">The edge used to turn two-sided sheets.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DuplexBinding.LongEdge)]
    [Arguments(DuplexBinding.ShortEdge)]
    public async Task PreviewsChosenPages(DuplexBinding binding)
    {
        var printer = new RecordingPrinter();
        using var test = new TestServices(new PrintingPlatform(printer));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("range.pdf", RangePages)]);
        var tab = main.SelectedTab!;
        var sheets = 0;
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(async context =>
        {
            var preview = context.Input;
            preview.PageChoice = PrintPageChoice.Custom;
            preview.CustomPages = "1-3, 5";
            preview.PagesPerSheetIndex = TwoPerSheet;
            preview.Copies = Copies;
            preview.TwoSided = true;
            preview.Binding = binding;
            preview.Colour = false;
            _ = await WaitAsync(() => preview.IsValid && !preview.IsBuilding && preview.Summary == "2 sheets");
            sheets = preview.Sheets.Count;
            context.SetOutput(true);
        });

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(sheets).IsEqualTo(ExpectedSheets);
        await Assert.That(printer.PageCount).IsEqualTo(ExpectedSheets);
        await Assert.That(printer.Options.Copies).IsEqualTo(Copies);
        await Assert.That(printer.Options.TwoSided).IsTrue();
        await Assert.That(printer.Options.Binding).IsEqualTo(binding);
        await Assert.That(printer.Options.Colour).IsFalse();
    }

    /// <summary>A booklet and a poster are previewed with the sheets they need, and the booklet is what prints.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewsBookletsAndPosters()
    {
        var printer = new RecordingPrinter();
        using var test = new TestServices(new PrintingPlatform(printer));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("booklet.pdf", RangePages)]);
        var tab = main.SelectedTab!;
        var posterSheets = 0;
        var bookletSheets = 0;
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(async context =>
        {
            var preview = context.Input;
            preview.LayoutIndex = PosterLayout;
            _ = await WaitAsync(() => preview.IsValid && !preview.IsBuilding && preview.Sheets.Count == RangePages * PosterSheetsPerPage);
            posterSheets = preview.Sheets.Count;
            preview.LayoutIndex = BookletLayout;
            _ = await WaitAsync(() => preview.IsValid && !preview.IsBuilding && preview.Sheets.Count < RangePages);
            bookletSheets = preview.Sheets.Count;
            context.SetOutput(true);
        });

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(posterSheets).IsEqualTo(RangePages * PosterSheetsPerPage);
        await Assert.That(bookletSheets).IsEqualTo(Booklet.SheetCount(RangePages) * BookletSides);
        await Assert.That(printer.PageCount).IsEqualTo(bookletSheets);
    }

    /// <summary>Confirms the preview once its sheets are ready and the printers are listed, so the job goes to the printer.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private static async Task ConfirmWhenReadyAsync(IInteractionContext<PrintPreviewViewModel, bool> context)
    {
        var preview = context.Input;
        _ = await WaitAsync(() => preview.IsValid && !preview.IsBuilding && preview.Destination == PrintDestination.Printer);
        context.SetOutput(true);
    }

    /// <summary>Waits for a condition.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true"/> when it held in time.</returns>
    private static async Task<bool> WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < WaitSteps && !condition(); i++)
        {
            await Task.Delay(WaitStep);
        }

        return condition();
    }
}
