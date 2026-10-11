// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests scrolling and recycling the sheets in the print preview.</summary>
public sealed class PrintPreviewWindowTests
{
    /// <summary>The sheets in the scrolling document.</summary>
    private const int PageCount = 80;

    /// <summary>A custom print scale.</summary>
    private const int CustomPercent = 80;

    /// <summary>The test document's sideways sheet.</summary>
    private const int WideSheet = 1;

    /// <summary>The number of end-to-end scrolls.</summary>
    private const int ScrollCount = 4;

    /// <summary>The limit for a scrolling test in milliseconds.</summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>The minimum contrast for active text.</summary>
    private const double TextContrast = 7;

    /// <summary>The RGB bits of a brush colour.</summary>
    private const uint RgbMask = 0xFFFFFFU;

    /// <summary>A dark blue desktop accent that is unsuitable for text on a dark surface.</summary>
    private const uint NavyAccent = 0x15335EU;

    /// <summary>Verifies that the duplex edge control follows the checkbox and updates the printer job.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesDuplexBinding()
    {
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter()));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("duplex.pdf", 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var window = new PrintPreviewWindow { ViewModel = preview };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => preview.Destination == PrintDestination.Printer)).IsTrue();
            var binding = window.FindControl<ComboBox>("BindingBox")!;
            var sides = window.FindControl<CheckBox>("TwoSidedBox")!;
            await Assert.That(binding.IsEffectivelyVisible).IsTrue();
            await Assert.That(binding.IsEnabled).IsFalse();
            sides.IsChecked = true;
            await Assert.That(await UiWait.UntilAsync(() => binding.IsEnabled && preview.TwoSided)).IsTrue();
            binding.SelectedIndex = (int)DuplexBinding.ShortEdge;
            await Assert.That(await UiWait.UntilAsync(() => preview.JobOptions.Binding == DuplexBinding.ShortEdge)).IsTrue();
            sides.IsChecked = false;
            await Assert.That(await UiWait.UntilAsync(() => !binding.IsEnabled && !preview.TwoSided)).IsTrue();
            await Assert.That(binding.SelectedIndex).IsEqualTo((int)DuplexBinding.ShortEdge);
            preview.SelectedTarget = preview.Targets.Single(static target => target.Kind == PrintDestination.SaveAsPdf);
            await Assert.That(await UiWait.UntilAsync(() => !binding.IsEffectivelyVisible)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that printer scaling choices show only where they apply and reach the sheet layout.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesPrintScaling()
    {
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter()));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("scaling.pdf", 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var window = new PrintPreviewWindow { ViewModel = preview };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => preview.Destination == PrintDestination.Printer && preview.IsValid && !preview.IsBuilding)).IsTrue();
            var scaling = window.FindControl<ComboBox>("ScalingBox")!;
            var percent = window.FindControl<NumericUpDown>("ScalePercentBox")!;
            await Assert.That(scaling.IsEffectivelyVisible).IsTrue();
            await Assert.That(percent.IsEffectivelyVisible).IsFalse();
            scaling.SelectedIndex = (int)PrintScaling.Custom;
            percent.Value = CustomPercent;
            await Assert.That(await UiWait.UntilAsync(() => percent.IsEffectivelyVisible && preview.CurrentLayout().ScalePercent == CustomPercent)).IsTrue();
            await Assert.That(preview.CurrentLayout().Scaling).IsEqualTo(PrintScaling.Custom);
            window.FindControl<ComboBox>("PaperBox")!.SelectedIndex = (int)PaperSize.Tabloid;
            await Assert.That(await UiWait.UntilAsync(() => preview.CurrentLayout().Paper == PaperSize.Tabloid && preview.IsValid && !preview.IsBuilding)).IsTrue();
            preview.LayoutIndex = 1;
            await Assert.That(await UiWait.UntilAsync(() => !scaling.IsEffectivelyVisible && !percent.IsEffectivelyVisible)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that the system print action uses readable text even with a dark desktop accent.</summary>
    /// <param name="choice">The interface scheme.</param>
    /// <param name="navyAccent">Whether the desktop supplies a dark blue accent.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ColorSchemeChoice.Calm, false)]
    [Arguments(ColorSchemeChoice.Dark, false)]
    [Arguments(ColorSchemeChoice.Light, false)]
    [Arguments(ColorSchemeChoice.HighContrast, false)]
    [Arguments(ColorSchemeChoice.Calm, true)]
    public async Task SystemDialogTextIsReadable(ColorSchemeChoice choice, bool navyAccent)
    {
        using var test = new TestServices();
        test.Services.Settings.ColorScheme = choice;
        test.Services.ApplySettings();
        var theme = test.Services.CurrentTheme;
        if (navyAccent)
        {
            theme = theme with { Scheme = theme.Scheme with { Accent = NavyAccent } };
        }

        DesktopThemeApplier.Apply(Application.Current!, theme);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("print-colour.pdf", 1)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var window = new PrintPreviewWindow { ViewModel = preview };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding)).IsTrue();
            var button = window.FindControl<Button>("SystemDialogButton")!;
            var text = button.GetVisualDescendants().OfType<TextBlock>().Single();
            var colour = ((ISolidColorBrush)text.Foreground!).Color.ToUInt32() & RgbMask;
            await Assert.That(ColorMath.Contrast(colour, theme.Scheme.Header)).IsGreaterThanOrEqualTo(TextContrast);
        }
        finally
        {
            window.Close();
            DesktopThemeApplier.Apply(Application.Current!, ThemeResolver.Resolve(new(), null));
        }
    }

    /// <summary>Verifies that scrolling to the last sheet keeps a rendered preview and a stable scroll extent.</summary>
    /// <param name="cancellationToken">Cancellation when the scrolling test exceeds its time limit.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Timeout(TimeoutMilliseconds)]
    public async Task ScrollingToLastSheetKeepsPreview(CancellationToken cancellationToken)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("scroll-print.pdf", PageCount)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var window = new PrintPreviewWindow { ViewModel = preview };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding)).IsTrue();
            var list = window.FindControl<ListBox>("SheetList")!;
            var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
            double? extent = null;
            for (var i = 0; i < ScrollCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                list.ScrollIntoView(preview.Sheets[0]);
                await Assert.That(await UiWait.UntilAsync(() => list.GetVisualDescendants().OfType<PrintPreviewPageView>().Any(v => v.ViewModel == preview.Sheets[0]))).IsTrue();
                list.ScrollIntoView(preview.Sheets[^1]);
                _ = await UiWait.UntilAsync(() => list.GetVisualDescendants().OfType<PrintPreviewPageView>().Any(v => v.ViewModel == preview.Sheets[^1]));
                await Assert.That(string.Join(", ", list.GetVisualDescendants().OfType<PrintPreviewPageView>().Select(static v => v.ViewModel?.Caption))).Contains(preview.Sheets[^1].Caption);
                var last = list.GetVisualDescendants().OfType<PrintPreviewPageView>().Single(v => v.ViewModel == preview.Sheets[^1]);
                await Assert.That(last.GetVisualDescendants().OfType<Image>().Single().Source).IsNotNull();
                extent ??= scroll.Extent.Height;
                await Assert.That(scroll.Extent.Height).IsEqualTo(extent.Value);
            }

            await Assert.That(preview.Sheets.Count).IsEqualTo(PageCount);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Verifies that upright and sideways sheets take the same space, so loading and releasing previews while scrolling
    /// never moves the chosen sheet, and that rebuilding the preview keeps the chosen sheet.
    /// </summary>
    /// <param name="cancellationToken">Cancellation when the scrolling test exceeds its time limit.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Timeout(TimeoutMilliseconds)]
    public async Task ChosenSheetStaysInPlace(CancellationToken cancellationToken)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("mixed-print.pdf", PageCount)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var window = new PrintPreviewWindow { ViewModel = preview };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding)).IsTrue();
            var list = window.FindControl<ListBox>("SheetList")!;
            var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
            await Assert.That(preview.Sheets[WideSheet].Size.Width).IsGreaterThan(preview.Sheets[WideSheet].Size.Height);
            await Assert.That(await UiWait.UntilAsync(() => ShownSheet(list, WideSheet) is not null)).IsTrue();
            var heights = list.GetVisualDescendants().OfType<ListBoxItem>().Select(static item => item.Bounds.Height).Distinct().ToList();
            await Assert.That(heights.Count).IsEqualTo(1);

            list.SelectedIndex = WideSheet;
            await Assert.That(await UiWait.UntilAsync(() => preview.SelectedSheetIndex == WideSheet)).IsTrue();
            var place = list.ContainerFromIndex(WideSheet)!.Bounds.Top;
            var extent = scroll.Extent.Height;
            for (var i = 0; i < ScrollCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                list.ScrollIntoView(preview.Sheets[^1]);
                await Assert.That(await UiWait.UntilAsync(() => ShownSheet(list, preview.Sheets.Count - 1) is not null)).IsTrue();
                await Assert.That(scroll.Extent.Height).IsEqualTo(extent);
                list.ScrollIntoView(preview.Sheets[WideSheet]);
                await Assert.That(await UiWait.UntilAsync(() => ShownSheet(list, WideSheet)?.GetVisualDescendants().OfType<Image>().Single().Source is not null)).IsTrue();
                await Assert.That(list.ContainerFromIndex(WideSheet)!.Bounds.Top).IsEqualTo(place);
                await Assert.That(scroll.Extent.Height).IsEqualTo(extent);
            }

            preview.IncludeAnnotations = false;
            await Assert.That(await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding && preview.Sheets.Count == PageCount)).IsTrue();
            await Assert.That(preview.SelectedSheetIndex).IsEqualTo(WideSheet);
            await Assert.That(list.SelectedIndex).IsEqualTo(WideSheet);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Finds the view showing a sheet, when it is realised.</summary>
    /// <param name="list">The sheet list.</param>
    /// <param name="index">The sheet.</param>
    /// <returns>The view, or <see langword="null"/>.</returns>
    private static PrintPreviewPageView? ShownSheet(ListBox list, int index) =>
        list.ContainerFromIndex(index)?.GetVisualDescendants().OfType<PrintPreviewPageView>().FirstOrDefault();
}
