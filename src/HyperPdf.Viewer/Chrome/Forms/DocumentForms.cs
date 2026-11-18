// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.Windowing.Rendering;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Describes the windows about one document or the app itself: document properties, licences and printing.</summary>
internal static class DocumentForms
{
    /// <summary>The row of the licences window that searches the components.</summary>
    private const int SearchRow = 0;

    /// <summary>The rows of the licences window that each choose a component.</summary>
    private const int ComponentRow = 1;

    /// <summary>The row of the licences window that copies the licence shown.</summary>
    private const int CopyRow = 2;

    /// <summary>The row of the print window that chooses where the print goes.</summary>
    private const int TargetRow = 0;

    /// <summary>The row of the print window that chooses which pages print.</summary>
    private const int PagesRow = 1;

    /// <summary>The row of the print window that lists the pages to print.</summary>
    private const int CustomPagesRow = 2;

    /// <summary>The row of the print window that sets the copies.</summary>
    private const int CopiesRow = 3;

    /// <summary>The row of the print window that chooses the layout.</summary>
    private const int LayoutRow = 4;

    /// <summary>The row of the print window that turns colour on or off.</summary>
    private const int ColourRow = 5;

    /// <summary>The row of the print window that turns printing on both sides on or off.</summary>
    private const int TwoSidedRow = 6;

    /// <summary>The row of the print window that includes or leaves out notes and marks.</summary>
    private const int MarksRow = 7;

    /// <summary>The row of the print window that shows the sheet before the one shown.</summary>
    private const int PreviousSheetRow = 8;

    /// <summary>The row of the print window that shows the sheet after the one shown.</summary>
    private const int NextSheetRow = 9;

    /// <summary>The row of the print window that chooses how many pages go on each sheet.</summary>
    private const int PagesPerSheetRow = 10;

    /// <summary>The row of the print window that chooses the paper size.</summary>
    private const int PaperRow = 11;

    /// <summary>The row of the print window that chooses how the pages fit the paper.</summary>
    private const int ScalingRow = 12;

    /// <summary>The row of the print window that sets the custom scale.</summary>
    private const int ScalePercentRow = 13;

    /// <summary>The row of the print window that chooses the edge sheets turn on when both sides print.</summary>
    private const int BindingRow = 14;

    /// <summary>The names of the pages a print can cover, in the order of <see cref="PrintPageChoice"/>.</summary>
    private static readonly string[] PageChoices = ["All pages", "The page shown", "Pages I list"];

    /// <summary>The names of the paper sizes, in the order of <see cref="PaperSize"/>.</summary>
    private static readonly string[] PaperChoices = ["A4", "Letter", "A3", "A5", "Legal", "Tabloid"];

    /// <summary>The names of the ways the pages fit the paper, in the order of <see cref="PrintScaling"/>.</summary>
    private static readonly string[] ScalingChoices = ["Fit to paper", "Actual size", "Shrink oversized pages", "Custom scale"];

    /// <summary>The names of the edges sheets turn on, in the order of <see cref="DuplexBinding"/>.</summary>
    private static readonly string[] BindingChoices = ["Long edge", "Short edge"];

    /// <summary>Makes the document properties window.</summary>
    /// <param name="properties">The properties.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest Properties(PropertiesViewModel properties) => new(
        properties.Title,
        form =>
        {
            foreach (var entry in properties.Entries)
            {
                form.Pair(entry.DisplayName, entry.DisplayValue);
            }
        },
        null,
        null,
        Signal.Never<RxVoid>(),
        null,
        static _ => EmptyDisposable.Instance);

    /// <summary>Makes the licences window: the notices of everything the app is built from, with a search.</summary>
    /// <param name="licences">The licences.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest Licences(LicencesViewModel licences) => new(
        UiText.Get("Licences"),
        form => DescribeLicences(form, licences),
        null,
        null,
        Signal.Merge(
            licences.WhenChanged(static l => l.Nodes).AsSignal(),
            licences.WhenChanged(static l => l.SelectedNode).AsSignal(),
            licences.WhenChanged(static l => l.Status).AsSignal(),
            licences.WhenChanged(static l => l.LicenceText).AsSignal()),
        null,
        edits => new MultipleDisposable(
            edits.WriteRow(SearchRow, licences, static (l, edit) => l.Search = edit.Text),
            edits.WriteRow(ComponentRow, licences, static (l, edit) => l.SelectedNode = edit.Target as LicenceNode),
            edits.WhereRow(CopyRow).AsSignal().InvokeCommand(licences.CopyCommand)));

    /// <summary>Makes the print window.</summary>
    /// <param name="print">The print settings.</param>
    /// <param name="pictures">Draws the sheet shown beside the settings, in the room it is given.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest Print(PrintPreviewViewModel print, Func<PrintPreviewPage, SurfaceSize, IRenderSurface?> pictures) => new(
        UiText.Format("Print {0}", print.FileName),
        form => DescribePrint(form, print),
        UiText.Get("Print"),
        () => print.IsReady,
        Signal.Merge(
            print.WhenChanged(static p => p.IsBuilding).AsSignal(),
            print.WhenChanged(static p => p.SelectedTarget).AsSignal(),
            print.WhenChanged(static p => p.PageChoice).AsSignal(),
            ((IList)print.Targets).CollectionChanges.AsSignal(),
            print.WhenChanged(static p => p.SelectedSheetIndex).AsSignal(),
            print.WhenChanged(static p => p.LayoutIndex).AsSignal(),
            print.WhenChanged(static p => p.PagesPerSheetIndex).AsSignal(),
            print.WhenChanged(static p => p.Scaling).AsSignal(),
            print.WhenChanged(static p => p.TwoSided).AsSignal(),
            ((IList)print.Sheets).CollectionChanges.AsSignal()),
        room => ShownSheet(print) is { } sheet ? pictures(sheet, room) : null,
        edits => new MultipleDisposable(
            edits.WriteRow(TargetRow, print, static (p, edit) => p.SelectedTarget = (uint)edit.Index < (uint)p.Targets.Count ? p.Targets[edit.Index] : p.SelectedTarget),
            edits.WriteRow(PagesRow, print, static (p, edit) => p.PageChoice = (PrintPageChoice)edit.Index),
            edits.WriteRow(CustomPagesRow, print, static (p, edit) => p.CustomPages = edit.Text),
            edits.WriteRow(CopiesRow, print, static (p, edit) => p.Copies = ReadCopies(edit.Text)),
            edits.WriteRow(LayoutRow, print, static (p, edit) => p.LayoutIndex = edit.Index),
            edits.WriteRow(PagesPerSheetRow, print, static (p, edit) => p.PagesPerSheetIndex = edit.Index),
            edits.WriteRow(PaperRow, print, static (p, edit) => p.Paper = (PaperSize)edit.Index),
            edits.WriteRow(ScalingRow, print, static (p, edit) => p.Scaling = (PrintScaling)edit.Index),
            edits.WriteRow(ScalePercentRow, print, static (p, edit) => p.ScalePercent = ReadPercent(edit.Text, p.ScalePercent)),
            edits.WriteRow(BindingRow, print, static (p, edit) => p.Binding = (DuplexBinding)edit.Index),
            edits.WriteRow(ColourRow, print, static (p, edit) => p.Colour = edit.On),
            edits.WriteRow(TwoSidedRow, print, static (p, edit) => p.TwoSided = edit.On),
            edits.WriteRow(MarksRow, print, static (p, edit) => p.IncludeAnnotations = edit.On),
            edits.WriteRow(PreviousSheetRow, print, static (p, _) => p.SelectedSheetIndex = Math.Max(0, p.SelectedSheetIndex - 1)),
            edits.WriteRow(NextSheetRow, print, static (p, _) => p.SelectedSheetIndex = Math.Min(p.Sheets.Count - 1, p.SelectedSheetIndex + 1))));

    /// <summary>Finds the sheet the print window shows a picture of.</summary>
    /// <param name="print">The print settings.</param>
    /// <returns>The chosen sheet, the first sheet when none is chosen, or <see langword="null"/> while the sheets are being made.</returns>
    private static PrintPreviewPage? ShownSheet(PrintPreviewViewModel print) =>
        print.Sheets.Count == 0 ? null : print.Sheets[Math.Clamp(print.SelectedSheetIndex, 0, print.Sheets.Count - 1)];

    /// <summary>Reads a scale in percent as it is typed.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="current">The scale kept when the text is not a whole number.</param>
    /// <returns>The scale.</returns>
    private static int ReadPercent(string text, int current) => int.TryParse(text, NumberStyles.None, CultureInfo.CurrentCulture, out var percent) ? percent : current;

    /// <summary>Reads a number of copies as it is typed. Anything that is not a whole number above zero counts as one copy.</summary>
    /// <param name="text">The typed text.</param>
    /// <returns>The copies, at least one.</returns>
    private static int ReadCopies(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.CurrentCulture, out var copies) ? Math.Max(1, copies) : 1;

    /// <summary>Adds the rows of the licences window.</summary>
    /// <param name="form">Receives the rows.</param>
    /// <param name="licences">The licences.</param>
    private static void DescribeLicences(FormBuilder form, LicencesViewModel licences)
    {
        form.Text(licences.VersionHeading);
        form.Field(UiText.Get("Find a component"), licences.Search, SearchRow);
        form.Text(licences.Summary);
        foreach (var node in licences.Nodes)
        {
            AddNode(form, licences, node, string.Empty);
        }

        if (licences.SelectedNode is null)
        {
            return;
        }

        form.Heading(licences.DetailTitle);
        form.Text(licences.Details);
        form.Action(UiText.Get("Copy this licence"), true, CopyRow, null);
        form.Text(licences.LicenceText);
    }

    /// <summary>Adds a component and the components under it.</summary>
    /// <param name="form">Receives the rows.</param>
    /// <param name="licences">The licences.</param>
    /// <param name="node">The component.</param>
    /// <param name="indent">The space that sets the component in under its parent.</param>
    private static void AddNode(FormBuilder form, LicencesViewModel licences, LicenceNode node, string indent)
    {
        var chosen = ReferenceEquals(node, licences.SelectedNode);
        var mark = chosen ? "● " : string.Empty;
        form.Action($"{indent}{mark}{node.Title}", true, ComponentRow, node);
        foreach (var child in node.Children)
        {
            AddNode(form, licences, child, $"{indent}    ");
        }
    }

    /// <summary>Adds the rows that say how pages go on the paper: how many to a sheet, the paper size, how pages fit it and the custom scale. Each shows only when it applies.</summary>
    /// <param name="form">Receives the rows.</param>
    /// <param name="print">The print settings.</param>
    private static void DescribeSheetSettings(FormBuilder form, PrintPreviewViewModel print)
    {
        if (print.LayoutIndex == 0)
        {
            var counts = new string[print.PagesPerSheetChoices.Length];
            for (var i = 0; i < counts.Length; i++)
            {
                counts[i] = print.PagesPerSheetChoices[i].ToString(CultureInfo.CurrentCulture);
            }

            form.Choice(UiText.Get("Pages per sheet"), counts, print.PagesPerSheetIndex, PagesPerSheetRow);
        }

        if (print.ShowsPaper)
        {
            form.Choice(UiText.Get("Paper size"), [..UiText.CreateChoices(PaperChoices)], (int)print.Paper, PaperRow);
        }

        if (print.ShowsScaling)
        {
            form.Choice(UiText.Get("Page size"), [..UiText.CreateChoices(ScalingChoices)], (int)print.Scaling, ScalingRow);
        }

        if (print.ShowsScalePercent)
        {
            form.Field(UiText.Get("Scale (%)"), print.ScalePercent.ToString(CultureInfo.CurrentCulture), ScalePercentRow);
        }
    }

    /// <summary>Adds the rows of the print window.</summary>
    /// <param name="form">Receives the rows.</param>
    /// <param name="print">The print settings.</param>
    private static void DescribePrint(FormBuilder form, PrintPreviewViewModel print)
    {
        var targets = new string[print.Targets.Count];
        for (var i = 0; i < targets.Length; i++)
        {
            targets[i] = print.Targets[i].Label;
        }

        form.Choice(UiText.Get("Send to"), targets, print.SelectedTarget is { } target ? print.Targets.IndexOf(target) : -1, TargetRow);
        form.Choice(UiText.Get("Pages"), [..UiText.CreateChoices(PageChoices)], (int)print.PageChoice, PagesRow);
        if (print.PageChoice == PrintPageChoice.Custom)
        {
            form.Field(UiText.Get("Pages to print, such as 1-3, 7"), print.CustomPages, CustomPagesRow);
        }

        form.Field(UiText.Get("Copies"), print.Copies.ToString(CultureInfo.CurrentCulture), CopiesRow);
        form.Choice(UiText.Get("Layout"), PrintPreviewViewModel.LayoutChoices, print.LayoutIndex, LayoutRow);
        DescribeSheetSettings(form, print);
        form.Toggle(UiText.Get("Colour"), print.Colour, ColourRow);
        form.Toggle(UiText.Get("Print on both sides"), print.TwoSided, TwoSidedRow);
        if (print.TwoSided)
        {
            form.Choice(UiText.Get("Turn pages"), [..UiText.CreateChoices(BindingChoices)], (int)print.Binding, BindingRow);
        }

        form.Toggle(UiText.Get("Include notes and marks"), print.IncludeAnnotations, MarksRow);
        form.Text(print.IsBuilding ? UiText.Get("Getting the pages ready…") : print.Summary);
        if (ShownSheet(print) is not { } sheet)
        {
            return;
        }

        var at = print.Sheets.IndexOf(sheet);
        form.Text(UiText.Format("Sheet shown: {0}", sheet.Caption));
        form.Action(UiText.Get("Show the sheet before"), at > 0, PreviousSheetRow, null);
        form.Action(UiText.Get("Show the next sheet"), at < print.Sheets.Count - 1, NextSheetRow, null);
    }
}
