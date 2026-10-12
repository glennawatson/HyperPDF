// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>Printing a tab's document: the print preview, the desktop's print dialog and saving the printed pages as a PDF.</summary>
[DebuggerDisplay("TabPrintingViewModel: {_owner.FileName}")]
public sealed partial class TabPrintingViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="TabPrintingViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    internal TabPrintingViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets the interaction showing the print preview; it returns whether the reader chose to print.</summary>
    public Interaction<PrintPreviewViewModel, bool> PrintPreviewInteraction { get; } = new();

    /// <summary>Deletes a file, ignoring failures.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when deleted.</returns>
    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Shows the print preview, then sends the file it built straight to the chosen printer, saves it as a PDF or opens
    /// the desktop's print dialog, saying what happened in the notice.
    /// </summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PrintAsync()
    {
        using var preview = new PrintPreviewViewModel(_owner, _owner.Services);
        if (!await PrintPreviewInteraction.Handle(preview).ToTask().ConfigureAwait(true))
        {
            return;
        }

        var destination = preview.Destination;
        var label = preview.SelectedTarget?.Label ?? string.Empty;
        var job = preview.JobOptions;
        var file = preview.TakePrintFile();
        if (destination == PrintDestination.SystemDialog)
        {
            _ = file is not null && TryDelete(file);
            await PrintWithSystemDialogAsync().ConfigureAwait(true);
            return;
        }

        if (file is null)
        {
            return;
        }

        try
        {
            if (destination == PrintDestination.SaveAsPdf)
            {
                await SavePrintAsPdfAsync(file).ConfigureAwait(true);
                return;
            }

            var outcome = await _owner.Services.Platform.Printer.SubmitAsync(file, _owner.FileName, job, CancellationToken.None).ConfigureAwait(true);
            _owner.Notice = outcome.Sent ? $"Sent to {label}." : $"Could not print: {outcome.Detail}";
        }
        finally
        {
            _ = TryDelete(file);
        }
    }

    /// <summary>Hands the whole document, as it looks now, straight to the desktop's print dialog.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PrintWithSystemDialogAsync()
    {
        var printer = _owner.Services.Platform.Printer;
        if (!printer.IsAvailable)
        {
            _owner.Notice = "Printing is not available on this desktop. Choose Save as PDF instead.";
            return;
        }

        if (DocumentFeature<IPageExporter>.Get(_owner.TryGetDocument()) is not { } exporter || _owner.Source.PageCount == 0)
        {
            return;
        }

        var file = Path.Combine(Path.GetTempPath(), $"hyperpdf-print-{Guid.NewGuid():N}.pdf");
        try
        {
            var pages = new int[_owner.Source.PageCount];
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i] = i;
            }

            bool written;
            await using (var stream = File.Create(file))
            {
                written = exporter.ExportPages(pages, SheetLayout.Default, stream);
            }

            if (!written)
            {
                _owner.Notice = "Could not prepare the document for printing.";
            }
            else if (!await printer.PrintAsync(file, _owner.FileName, CancellationToken.None).ConfigureAwait(true))
            {
                _owner.Notice = "The print dialog could not be opened.";
            }
        }
        finally
        {
            _ = TryDelete(file);
        }
    }

    /// <summary>Asks where to save the printed pages as a PDF and copies them there.</summary>
    /// <param name="file">The printed pages.</param>
    /// <returns>A task.</returns>
    private async Task SavePrintAsPdfAsync(string file)
    {
        var path = await _owner.SaveAsInteraction.Handle(_owner.FileName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Copy(file, path, true);
        }
        catch (IOException ex)
        {
            _owner.Notice = $"Could not save: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.Notice = $"Could not save: {ex.Message}";
        }
    }
}
