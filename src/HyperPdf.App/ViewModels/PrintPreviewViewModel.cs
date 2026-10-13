// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The print window, modelled on a browser's print preview: the sheets that will print on the left, built from the very
/// file that is printed, and a few settings on the right. Printing hands that file to the desktop's print dialog, which
/// picks the printer; Save as PDF writes it where the user chooses.
/// </summary>
[DebuggerDisplay("PrintPreviewViewModel: {Summary}")]
public sealed partial class PrintPreviewViewModel : ReactiveObject, IDisposable
{
    /// <summary>The booklet's place in <see cref="LayoutChoices"/>.</summary>
    private const int BookletLayout = 1;

    /// <summary>The first poster's place in <see cref="LayoutChoices"/>.</summary>
    private const int FirstPosterLayout = 2;

    /// <summary>The sheets across the smallest poster.</summary>
    private const int MinPosterTiles = 2;

    /// <summary>The most copies offered.</summary>
    private const int MaxCopies = 999;

    /// <summary>The Save as PDF destination.</summary>
    private static readonly PrintTarget SaveAsPdfTarget = new(PrintDestination.SaveAsPdf, string.Empty, "Save as PDF");

    /// <summary>The system print dialog destination.</summary>
    private static readonly PrintTarget SystemDialogTarget = new(PrintDestination.SystemDialog, string.Empty, "System print dialog");

    /// <summary>The tab being printed.</summary>
    private readonly DocumentTabViewModel _tab;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>The subscriptions rebuilding the preview.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>Raises <see cref="Answered"/> with <see langword="true"/>.</summary>
    private readonly Signal<RxVoid> _confirmed = new();

    /// <summary>Whether the settings are valid and no build is running, so the print can be confirmed.</summary>
    private readonly IObservable<bool> _ready;

    /// <summary>The pages chosen, reused between rebuilds.</summary>
    private readonly List<int> _chosen = [];

    /// <summary>Counts rebuilds, so a slow one that finishes late is dropped.</summary>
    private int _version;

    /// <summary>The preview document.</summary>
    private IDocument? _preview;

    /// <summary>Initializes a new instance of the <see cref="PrintPreviewViewModel"/> class.</summary>
    /// <param name="tab">The tab being printed.</param>
    /// <param name="services">The application services.</param>
    public PrintPreviewViewModel(DocumentTabViewModel tab, AppServices services)
    {
        _tab = tab;
        _services = services;
        _ready = this.WhenChanged(static vm => vm.IsValid, static vm => vm.IsBuilding, static (valid, building) => valid && !building);
        SelectedTarget = SystemDialogTarget;
        _ = LoadPrintersAsync();
        _subscriptions =
        [

            // Keeps the copies within range; the first value is the current one.
            this.WhenChanged(static vm => vm.Copies)
                .Skip(1)
                .SubscribeSafe(ClampCopies, static error => Trace.TraceError(error.ToString())),
            this.WhenChanged(
                    static vm => vm.PageChoice,
                    static vm => vm.CustomPages,
                    static vm => vm.PagesPerSheetIndex,
                    static vm => vm.LayoutIndex,
                    static vm => vm.Paper,
                    static vm => vm.IncludeAnnotations,
                    static vm => vm.SelectedTarget,
                    static (_, _, _, _, _, _, _) => RxVoid.Default)
                .SubscribeSafe(OnSettingsChanged, static error => Trace.TraceError(error.ToString())),

            // Scaling changes rebuild too; the first value is the current one, already built above.
            this.WhenChanged(static vm => vm.Scaling, static vm => vm.ScalePercent, static (_, _) => RxVoid.Default)
                .Skip(1)
                .SubscribeSafe(OnScalingChanged, static error => Trace.TraceError(error.ToString())),
        ];
        Answered = Signal.Merge(_confirmed.Select(static _ => true), CancelCommand);
    }

    /// <summary>Gets the layout choices: pages in order, a booklet, or posters of 2, 3 or 4 sheets across.</summary>
    public static IReadOnlyList<string> LayoutChoices { get; } =
    [
        "Pages in order",
        "Booklet (fold in half)",
        "Poster, 2 × 2 sheets a page",
        "Poster, 3 × 3 sheets a page",
        "Poster, 4 × 4 sheets a page",
    ];

    /// <summary>Gets the document's file name, for the window title.</summary>
    public string FileName => _tab.FileName;

    /// <summary>Gets the destinations: the printers (the default first), Save as PDF and the system dialog.</summary>
    public ObservableCollection<PrintTarget> Targets { get; } = [SaveAsPdfTarget, SystemDialogTarget];

    /// <summary>Gets or sets the chosen destination.</summary>
    [Reactive(nameof(Destination), nameof(ShowsPaper), nameof(ShowsScaling), nameof(ShowsScalePercent))]
    public partial PrintTarget? SelectedTarget { get; set; }

    /// <summary>Gets where the print goes.</summary>
    public PrintDestination Destination => SelectedTarget?.Kind ?? PrintDestination.SaveAsPdf;

    /// <summary>Gets the job settings for the chosen printer.</summary>
    public PrintJobOptions JobOptions => new(SelectedTarget?.Name ?? string.Empty, Copies, Colour, TwoSided, Paper) { Binding = Binding };

    /// <summary>Gets or sets the number of copies sent to a printer.</summary>
    [Reactive]
    public partial int Copies { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether a printer prints in colour rather than black and white.</summary>
    [Reactive]
    public partial bool Colour { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a printer prints on both sides of the paper.</summary>
    [Reactive]
    public partial bool TwoSided { get; set; }

    /// <summary>Gets or sets the edge used to turn sheets printed on both sides.</summary>
    [Reactive]
    public partial DuplexBinding Binding { get; set; }

    /// <summary>Gets or sets which pages print.</summary>
    [Reactive]
    public partial PrintPageChoice PageChoice { get; set; }

    /// <summary>Gets or sets the typed page ranges, used when <see cref="PageChoice"/> is custom.</summary>
    [Reactive]
    public partial string CustomPages { get; set; } = string.Empty;

    /// <summary>Gets or sets the index into <see cref="PagesPerSheetChoices"/>.</summary>
    [Reactive(nameof(ShowsPaper), nameof(ShowsScaling), nameof(ShowsScalePercent))]
    public partial int PagesPerSheetIndex { get; set; }

    /// <summary>Gets or sets the index into <see cref="LayoutChoices"/>.</summary>
    [Reactive(nameof(ShowsPaper), nameof(ShowsScaling), nameof(ShowsScalePercent))]
    public partial int LayoutIndex { get; set; }

    /// <summary>Gets a value indicating whether the paper size matters: for a printer, several pages per sheet, a booklet or a poster.</summary>
    public bool ShowsPaper => PagesPerSheetIndex > 0 || LayoutIndex > 0 || Destination == PrintDestination.Printer;

    /// <summary>Gets the choices for pages per sheet.</summary>
    public IReadOnlyList<int> PagesPerSheetChoices => SheetGrid.Choices;

    /// <summary>Gets or sets the paper used when several pages share a sheet.</summary>
    [Reactive]
    public partial PaperSize Paper { get; set; }

    /// <summary>Gets or sets how each page is sized on the paper when printing straight to a printer.</summary>
    [Reactive(nameof(ShowsScalePercent))]
    public partial PrintScaling Scaling { get; set; }

    /// <summary>Gets or sets the percentage of true size used with a custom scale.</summary>
    [Reactive]
    public partial int ScalePercent { get; set; } = PrintScale.TrueSize;

    /// <summary>Gets a value indicating whether the scaling choice matters: one page per sheet, in order, to a printer.</summary>
    public bool ShowsScaling => Destination == PrintDestination.Printer && LayoutIndex == 0 && PagesPerSheetIndex == 0;

    /// <summary>Gets a value indicating whether the custom percentage is shown.</summary>
    public bool ShowsScalePercent => ShowsScaling && Scaling == PrintScaling.Custom;

    /// <summary>Gets or sets a value indicating whether notes, highlights and drawings print.</summary>
    [Reactive]
    public partial bool IncludeAnnotations { get; set; } = true;

    /// <summary>Gets a value indicating whether the settings name pages to print.</summary>
    [Reactive]
    public partial bool IsValid { get; private set; }

    /// <summary>Gets a value indicating whether the preview is being built.</summary>
    [Reactive]
    public partial bool IsBuilding { get; private set; }

    /// <summary>Gets the sheet count, or what to fix, for the top of the settings panel.</summary>
    [Reactive]
    public partial string Summary { get; private set; } = string.Empty;

    /// <summary>Gets the sheets that will print.</summary>
    public ObservableCollection<PrintPreviewPage> Sheets { get; } = [];

    /// <summary>Gets or sets the chosen sheet, kept in place when the preview is rebuilt; -1 when none is chosen.</summary>
    [Reactive]
    public partial int SelectedSheetIndex { get; set; } = -1;

    /// <summary>Gets the file that will print, once built.</summary>
    public string? PreviewPath { get; private set; }

    /// <summary>Gets the answer, which closes the window: <see langword="true"/> to print.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Describes a sheet count.</summary>
    /// <param name="sheets">The sheets.</param>
    /// <returns>The description.</returns>
    public static string DescribeSheets(int sheets) => sheets == 1 ? "1 sheet" : string.Create(CultureInfo.CurrentCulture, $"{sheets} sheets");

    /// <summary>Gets the layout the settings describe.</summary>
    /// <returns>The layout.</returns>
    public SheetLayout CurrentLayout()
    {
        var perSheet = SheetGrid.Choices[Math.Clamp(PagesPerSheetIndex, 0, SheetGrid.Choices.Count - 1)];
        return LayoutIndex switch
        {
            BookletLayout => new SheetLayout(1, Paper, IncludeAnnotations) { Imposition = PrintImposition.Booklet },
            >= FirstPosterLayout => new SheetLayout(1, Paper, IncludeAnnotations) { Imposition = PrintImposition.Poster, PosterTiles = LayoutIndex - FirstPosterLayout + MinPosterTiles },
            _ => new SheetLayout(perSheet, Paper, IncludeAnnotations) { FitToPaper = Destination == PrintDestination.Printer, Scaling = Scaling, ScalePercent = PrintScale.ClampPercent(ScalePercent) },
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // A build still running when the window closes is now out of date; it deletes its own file instead of opening it.
        _version++;
        _subscriptions.Dispose();
        _confirmed.Dispose();
        ClosePreview();
    }

    /// <summary>Hands over the file that will print: the preview closes and the caller deletes the file when done.</summary>
    /// <returns>The file, or <see langword="null"/> when none is ready.</returns>
    public string? TakePrintFile()
    {
        var path = PreviewPath;
        PreviewPath = null;
        ClosePreview();
        return path;
    }

    /// <summary>Writes the chosen pages to a new temporary file.</summary>
    /// <param name="exporter">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="layout">The sheet layout.</param>
    /// <returns>The file, or <see langword="null"/> on failure.</returns>
    private static string? Build(IPageExporter exporter, int[] pages, in SheetLayout layout)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-print-{Guid.NewGuid():N}.pdf");
        try
        {
            bool written;
            using (var stream = File.Create(path))
            {
                written = exporter.ExportPages(pages, layout, stream);
            }

            if (written)
            {
                return path;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        Delete(path);
        return null;
    }

    /// <summary>Deletes a file, ignoring failures.</summary>
    /// <param name="path">The file.</param>
    private static void Delete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Closes the window without printing.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Lists the printers off the UI thread and chooses the default one, when there are any.</summary>
    /// <returns>A task.</returns>
    private async Task LoadPrintersAsync()
    {
        var printer = _services.Platform.Printer;
        var printers = await Task.Run(printer.GetPrinters).ConfigureAwait(true);
        for (var i = 0; i < printers.Count; i++)
        {
            Targets.Insert(i, new(PrintDestination.Printer, printers[i].Name, printers[i].DisplayName));
        }

        if (printers.Count > 0 && SelectedTarget == SystemDialogTarget)
        {
            SelectedTarget = Targets[0];
        }
        else if (printers.Count == 0 && !printer.IsAvailable)
        {
            SelectedTarget = SaveAsPdfTarget;
        }
    }

    /// <summary>Confirms the print.</summary>
    [ReactiveCommand(CanExecute = nameof(_ready))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Confirm() => _confirmed.OnNext(RxVoid.Default);

    /// <summary>Skips these settings and prints the whole document from the desktop's own dialog.</summary>
    [ReactiveCommand]
    private void SystemDialog()
    {
        SelectedTarget = SystemDialogTarget;
        _confirmed.OnNext(RxVoid.Default);
    }

    /// <summary>Keeps the number of copies between one and the most offered.</summary>
    /// <param name="copies">The copies now set.</param>
    private void ClampCopies(int copies)
    {
        var clamped = Math.Clamp(copies, 1, MaxCopies);
        if (clamped != copies)
        {
            Copies = clamped;
        }
    }

    /// <summary>Rebuilds the preview after the scale changed, only when the scale applies to these sheets.</summary>
    /// <param name="change">Unused.</param>
    private void OnScalingChanged(RxVoid change)
    {
        if (ShowsScaling)
        {
            _ = RebuildAsync();
        }
    }

    /// <summary>Rebuilds the preview after a setting changed.</summary>
    /// <param name="change">Unused.</param>
    private void OnSettingsChanged(RxVoid change) => _ = RebuildAsync();

    /// <summary>Chooses the pages from the settings.</summary>
    /// <returns><see langword="true"/> when at least one page is chosen.</returns>
    private bool ChoosePages()
    {
        _chosen.Clear();
        var count = _tab.Source.PageCount;
        switch (PageChoice)
        {
            case PrintPageChoice.Current:
                {
                    _chosen.Add(Math.Clamp(_tab.CurrentPageIndex, 0, Math.Max(0, count - 1)));
                    return count > 0;
                }

            case PrintPageChoice.Custom:
                {
                    return PageRanges.TryParse(CustomPages, count, _chosen);
                }

            default:
                {
                    for (var i = 0; i < count; i++)
                    {
                        _chosen.Add(i);
                    }

                    return count > 0;
                }
        }
    }

    /// <summary>Builds the file that will print off the UI thread, then shows its sheets.</summary>
    /// <returns>A task.</returns>
    private async Task RebuildAsync()
    {
        var version = ++_version;
        if (DocumentFeature<IPageExporter>.Get(_tab.TryGetDocument()) is not
            {
            } exporter || !ChoosePages())
        {
            IsValid = false;
            Summary = PageChoice == PrintPageChoice.Custom ? "Type pages like 1-3, 7" : "Nothing to print";
            return;
        }

        IsBuilding = true;
        var pages = _chosen.ToArray();
        var layout = CurrentLayout();
        var path = await Task.Run(() => Build(exporter, pages, layout)).ConfigureAwait(true);
        if (version != _version)
        {
            Delete(path);
            return;
        }

        IsBuilding = false;
        var selected = SelectedSheetIndex;
        ClosePreview();
        if (path is null)
        {
            IsValid = false;
            Summary = "Could not prepare the pages";
            return;
        }

        PreviewPath = path;
        _preview = _services.Engine.Open(path, null);
        var sizes = _preview.GetPageSizes();
        for (var i = 0; i < sizes.Length; i++)
        {
            Sheets.Add(new(_preview, i, sizes[i], string.Create(CultureInfo.CurrentCulture, $"{i + 1} of {sizes.Length}")));
        }

        SelectedSheetIndex = Math.Min(selected, sizes.Length - 1);
        Summary = DescribeSheets(sizes.Length);
        IsValid = true;
    }

    /// <summary>Closes and deletes the current preview.</summary>
    private void ClosePreview()
    {
        Sheets.Clear();
        _preview?.Dispose();
        _preview = null;
        Delete(PreviewPath);
        PreviewPath = null;
    }
}
