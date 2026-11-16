// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Output;

/// <summary>Printing a tab's document: the print preview, the desktop's print dialog and saving the printed pages as a PDF.</summary>
[DebuggerDisplay("TabPrintingViewModel: {_owner.FileName}")]
public sealed partial class TabPrintingViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="TabPrintingViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    internal TabPrintingViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets the interaction showing the print preview; it returns whether the reader chose to print.</summary>
    public Interaction<PrintPreviewViewModel, bool> PrintPreviewInteraction { get; } = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => LocalisationRefresh.RefreshCore(_owner, visited);

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

    /// <summary>Writes the given pages of the document to a new file.</summary>
    /// <param name="exporter">The document's page exporter.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="file">The file to create.</param>
    /// <returns><see langword="true"/> when the pages were written.</returns>
    private static bool ExportPages(IPageExporter exporter, int[] pages, string file)
    {
        using var stream = File.Create(file);
        return exporter.ExportPages(pages, SheetLayout.Default, stream);
    }

    /// <summary>Copies the printed pages to the path the reader chose.</summary>
    /// <param name="owner">The owning tab, which says what went wrong.</param>
    /// <param name="file">The printed pages.</param>
    /// <param name="path">Where to save them, or empty when the choice was given up.</param>
    /// <returns><see langword="true"/> when the pages were saved.</returns>
    private static bool CopyPrinted(DocumentTabViewModel owner, string file, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            File.Copy(file, path, true);
            return true;
        }
        catch (IOException ex)
        {
            owner.SetNotice(LocalizedMessage.Format(UiText, "Could not save: {0}", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            owner.SetNotice(LocalizedMessage.Format(UiText, "Could not save: {0}", ex.Message));
        }

        return false;
    }

    /// <summary>
    /// Shows the print preview, then sends the file it built straight to the chosen printer, saves it as a PDF or opens
    /// the desktop's print dialog, saying what happened in the notice.
    /// </summary>
    /// <returns>A signal that ends once the print is done or was given up.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Print() =>
        // The document's own actions bracket the print: they may change fields before the pages are built.
        Bracket(PreviewAndPrint);

    /// <summary>Hands the whole document, as it looks now, straight to the desktop's print dialog.</summary>
    /// <returns>A signal that ends once the dialog was opened or could not be.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> PrintWithSystemDialog() => Bracket(SendToSystemDialog);

    /// <summary>Runs the document's will-print actions, then the print, then its did-print actions when the pages were printed.</summary>
    /// <param name="print">The print.</param>
    /// <returns>A signal that ends once the actions and the print are done.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Bracket(Func<IObservable<bool>> print) =>
        _owner.Forms.RunDocumentEvent(FormDocumentEvent.WillPrint)
            .SwitchMap(_ => print())
            .SwitchMap(printed => printed ? _owner.Forms.RunDocumentEvent(FormDocumentEvent.DidPrint) : Signal.Return(FormActionResult.None))
            .Select(static _ => RxVoid.Default);

    /// <summary>Shows the print preview, then prints, saves the printed pages as a PDF or opens the desktop's print dialog.</summary>
    /// <returns>A cold signal giving <see langword="true"/> when the pages were handed to a printer, the print dialog or a saved file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> PreviewAndPrint() =>
        Signal.Using(
            () => new PrintPreviewViewModel(_owner, _owner.Services),
            preview => PrintPreviewInteraction.Handle(preview).ToSignal()
                .SwitchMap(chosen => chosen ? Send(preview) : Signal.Return(false)));

    /// <summary>Sends what the preview built to where the reader chose.</summary>
    /// <param name="preview">The preview.</param>
    /// <returns>A cold signal giving <see langword="true"/> when the pages were handed on.</returns>
    private IObservable<bool> Send(PrintPreviewViewModel preview)
    {
        var destination = preview.Destination;
        var label = preview.SelectedTarget?.Label ?? string.Empty;
        var job = preview.JobOptions;
        var file = preview.TakePrintFile();
        if (destination == PrintDestination.SystemDialog)
        {
            _ = file is not null && TryDelete(file);
            return SendToSystemDialog();
        }

        if (file is null)
        {
            return Signal.Return(false);
        }

        return (destination == PrintDestination.SaveAsPdf ? SavePrintAsPdf(file) : Submit(file, label, job))
            .Do(_ => TryDelete(file))
            .Finally(() => TryDelete(file));
    }

    /// <summary>Hands the printed pages to the platform's print service.</summary>
    /// <param name="file">The printed pages.</param>
    /// <param name="label">The printer's name.</param>
    /// <param name="job">How the job is printed.</param>
    /// <returns>A cold signal giving <see langword="true"/> when the printer took the job.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> Submit(string file, string label, PrintJobOptions job) =>
        _owner.Services.Platform.Printer.Submit(file, _owner.FileName, job)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(outcome => Sent(outcome, label));

    /// <summary>Says whether the printer took the job.</summary>
    /// <param name="outcome">What the print service said.</param>
    /// <param name="label">The printer's name.</param>
    /// <returns><see langword="true"/> when the printer took the job.</returns>
    private bool Sent(PrintOutcome outcome, string label)
    {
        _owner.SetNotice(outcome.Sent ? LocalizedMessage.Format(UiText, "Sent to {0}.", label) : LocalizedMessage.Format(UiText, "Could not print: {0}", outcome.Detail));
        return outcome.Sent;
    }

    /// <summary>Exports the whole document, as it looks now, and opens the desktop's print dialog for it.</summary>
    /// <returns>A cold signal giving <see langword="true"/> when the print dialog was opened.</returns>
    private IObservable<bool> SendToSystemDialog()
    {
        if (!_owner.Services.Platform.Printer.IsAvailable)
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "Printing is not available on this desktop. Choose Save as PDF instead."));
            return Signal.Return(false);
        }

        if (DocumentFeature<IPageExporter>.Get(_owner.TryGetDocument()) is not { } exporter || _owner.Source.PageCount == 0)
        {
            return Signal.Return(false);
        }

        var file = Path.Combine(Path.GetTempPath(), $"hyperpdf-print-{Guid.NewGuid():N}.pdf");
        var pages = new int[_owner.Source.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = i;
        }

        // Exporting every page runs on the pool, so the window stays alive while a long document is prepared.
        return Signal.Start(() => ExportPages(exporter, pages, file), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(written => written ? OpenDialog(file) : CouldNotPrepare())
            .Do(_ => TryDelete(file))
            .Finally(() => TryDelete(file));
    }

    /// <summary>Says the document could not be prepared for printing.</summary>
    /// <returns>A signal giving <see langword="false"/>.</returns>
    private IObservable<bool> CouldNotPrepare()
    {
        _owner.SetNotice(LocalizedMessage.Get(UiText, "Could not prepare the document for printing."));
        return Signal.Return(false);
    }

    /// <summary>Opens the desktop's print dialog for the exported file.</summary>
    /// <param name="file">The exported file.</param>
    /// <returns>A cold signal giving <see langword="true"/> when the dialog was opened.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> OpenDialog(string file) =>
        _owner.Services.Platform.Printer.Print(file, _owner.FileName)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(DialogOpened);

    /// <summary>Says when the print dialog could not be opened.</summary>
    /// <param name="opened">Whether the dialog was opened.</param>
    /// <returns><paramref name="opened"/>.</returns>
    private bool DialogOpened(bool opened)
    {
        if (!opened)
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "The print dialog could not be opened."));
        }

        return opened;
    }

    /// <summary>Asks where to save the printed pages as a PDF and copies them there.</summary>
    /// <param name="file">The printed pages.</param>
    /// <returns>A cold signal giving <see langword="true"/> when the pages were saved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> SavePrintAsPdf(string file) =>
        _owner.SaveAsInteraction.Handle(_owner.FileName).ToSignal().Select(path => CopyPrinted(_owner, file, path));
}
