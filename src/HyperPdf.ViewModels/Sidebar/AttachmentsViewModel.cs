// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Dialogs;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// A tab's embedded files. The attachments panel only appears for documents that have some; a file is saved where the
/// user picks, and never opened automatically, so nothing runs that the user did not choose to run.
/// </summary>
[DebuggerDisplay("AttachmentsViewModel: {Items.Count} attachments")]
public sealed partial class AttachmentsViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>Bytes in a kilobyte.</summary>
    private const double Kilobyte = 1024;

    /// <summary>Bytes in a megabyte.</summary>
    private const double Megabyte = Kilobyte * Kilobyte;

    /// <summary>The temporary folder opened attachments are written to.</summary>
    private const string TempFolderName = "hyperpdf-attachments";

    /// <summary>The name used for an attachment with no name of its own.</summary>
    private const string UnnamedFile = "attachment";

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Whether a file is selected, so it can be saved.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Initializes a new instance of the <see cref="AttachmentsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public AttachmentsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        _canSave = this.WhenChanged(static vm => vm.Selected).Select(static item => item is not null);
    }

    /// <summary>Gets the embedded files.</summary>
    public ObservableCollection<DocumentAttachment> Items { get; } = [];

    /// <summary>Gets a value indicating whether the document has embedded files.</summary>
    [Reactive]
    public partial bool HasAttachments { get; private set; }

    /// <summary>Gets or sets the selected file.</summary>
    [Reactive]
    public partial DocumentAttachment? Selected { get; set; }

    /// <summary>Gets how the document's portfolio asks for its files to be shown, or <see langword="null"/> when the document is not a portfolio.</summary>
    [Reactive]
    public partial PortfolioSummary? Portfolio { get; private set; }

    /// <summary>Gets a plain-words line about the portfolio, or <see langword="null"/> when the document is not a portfolio.</summary>
    [Reactive]
    public partial string? PortfolioText { get; private set; }

    /// <summary>Gets a value indicating whether the document is a portfolio that asks to be shown as a list of files.</summary>
    public bool ShowsPortfolio => Portfolio is { IsHidden: false };

    /// <summary>Gets the interaction asking where to save a file; the input is the suggested name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets the interaction asking which files to attach to the document.</summary>
    public Interaction<RxVoid, string[]> AttachInteraction { get; } = new();

    /// <summary>Gets the interaction asking whether to take a file out of the document. Cancel is the answer a plain Enter gives.</summary>
    public Interaction<ConfirmRequest, bool> RemoveInteraction { get; } = new();

    /// <summary>Describes a portfolio in words.</summary>
    /// <param name="portfolio">The portfolio.</param>
    /// <returns>The description.</returns>
    public static string DescribePortfolio(PortfolioSummary portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var view = portfolio.View.ToUpperInvariant() switch
        {
            "T" => UiText.Get("tiles"),
            "H" => UiText.Get("hidden"),
            _ => UiText.Get("details"),
        };
        var files = portfolio.FileCount == 1 ? UiText.Get("1 file") : UiText.Format("{0} files", portfolio.FileCount);
        var first = string.IsNullOrEmpty(portfolio.InitialFile) ? string.Empty : UiText.Format(" It opens at {0}.", portfolio.InitialFile);
        return UiText.Format("PDF portfolio: {0}, {1} view.{2}", files, view, first);
    }

    /// <summary>Describes a file size in words, for example "12 KB".</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The description.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        1 => UiText.Get("1 byte"),
        < (long)Kilobyte => UiText.Format("{0} bytes", bytes),
        < (long)Megabyte => UiText.Format("{0:0} KB", bytes / Kilobyte),
        _ => UiText.Format("{0:0.0} MB", bytes / Megabyte),
    };

    /// <summary>Lists the document's files; called when the document loads.</summary>
    public void Refresh()
    {
        Items.Clear();
        Selected = null;
        Portfolio = null;
        PortfolioText = null;
        if (DocumentFeature<IAttachmentSource>.Get(_owner.TryGetDocument()) is
            {
            } source)
        {
            var attachments = new List<DocumentAttachment>(source.GetAttachments());

            // Files the portfolio sorts come first, in its order; the rest keep their own order after them.
            attachments.Sort(static (first, second) => PortfolioRank(first).CompareTo(PortfolioRank(second)));
            foreach (var attachment in attachments)
            {
                Items.Add(attachment);
            }

            Portfolio = source.GetPortfolioSummary();
            PortfolioText = Portfolio is null ? null : DescribePortfolio(Portfolio);
            Selected = Portfolio is null ? null : FindInitial();
        }

        HasAttachments = Items.Count > 0;
        if (!HasAttachments && _owner.IsAttachmentsMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>
    /// Shows the file a file attachment annotation holds: the panel opens with the file selected, so Open and Save are one click
    /// away. Nothing is opened or saved until the reader asks, so a click on an icon never runs a file by surprise.
    /// </summary>
    /// <param name="target">The attached file link target.</param>
    /// <returns><see langword="true"/> when the file was found.</returns>
    public bool Reveal(LinkTarget target)
    {
        if (target.Kind != LinkTargetKind.AttachedFile || !int.TryParse(target.Uri, NumberStyles.None, CultureInfo.InvariantCulture, out var annotationId))
        {
            return false;
        }

        foreach (var item in Items)
        {
            if (item.AnnotationId != annotationId)
            {
                continue;
            }

            Selected = item;
            _owner.SidebarMode = SidebarMode.Attachments;
            _owner.SidebarVisible = true;
            return true;
        }

        return false;
    }

    /// <summary>Saves an embedded file to a path.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="path">Where to save it.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public IObservable<bool> SaveTo(DocumentAttachment attachment, string path)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return Write(attachment, path).Do(saved => Saved(attachment, saved));
    }

    /// <summary>Opens an embedded file: a PDF in a new tab, anything else with another app after asking.</summary>
    /// <param name="attachment">The file.</param>
    /// <returns>A cold signal that emits <see langword="true"/> when it opened, then completes.</returns>
    public IObservable<bool> Open(DocumentAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        // Each opening gets its own folder, so files with the same name never replace one another.
        var folder = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString("N"));
        var name = Path.GetFileName(attachment.Name);
        try
        {
            _ = Directory.CreateDirectory(folder);
        }
        catch (IOException ex)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Could not open {0}: {1}", attachment.Name, ex.Message));
            return Signal.Return(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Could not open {0}: {1}", attachment.Name, ex.Message));
            return Signal.Return(false);
        }

        var path = Path.Combine(folder, name.Length > 0 ? name : UnnamedFile);
        return Write(attachment, path).SwitchMap(written => written ? _owner.Links.OpenFile(path, 0) : Signal.Return(false));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => LocalisationRefresh.RefreshCore(_owner, visited);

    /// <summary>Gets the place a file takes in the list: the portfolio's order, then the files it does not describe in their own order.</summary>
    /// <param name="attachment">The file.</param>
    /// <returns>The rank, which is the portfolio order when the file has one and a larger number otherwise.</returns>
    private static long PortfolioRank(DocumentAttachment attachment) =>
        attachment.PortfolioOrder >= 0 ? attachment.PortfolioOrder : (long)int.MaxValue + attachment.Index;

    /// <summary>Reads each file and attaches it.</summary>
    /// <param name="editor">The document's attachment editor.</param>
    /// <param name="paths">The files.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The number of files attached.</returns>
    private static async Task<int> AttachAllAsync(IAttachmentEditor editor, string[] paths, CancellationToken cancellationToken)
    {
        var added = 0;
        foreach (var path in paths)
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var result = await editor.AddAttachment(Path.GetFileName(path), bytes).ToTask(cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                added++;
            }
        }

        return added;
    }

    /// <summary>Extracts an embedded file into a new file.</summary>
    /// <param name="source">The document's attachments.</param>
    /// <param name="index">The attachment.</param>
    /// <param name="path">Where to write it.</param>
    /// <returns><see langword="true"/> when written.</returns>
    private static bool WriteFile(IAttachmentSource source, int index, string path)
    {
        using var stream = File.Create(path);
        return source.SaveAttachment(index, stream);
    }

    /// <summary>Finds the file the portfolio names as the one to show first.</summary>
    /// <returns>The file, or <see langword="null"/> when the portfolio names none.</returns>
    private DocumentAttachment? FindInitial()
    {
        foreach (var item in Items)
        {
            if (item.IsPortfolioInitial)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Writes an embedded file to a path, saying what went wrong when it cannot.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="path">Where to write it.</param>
    /// <returns>A cold signal that extracts on the pool, so a large attachment does not freeze the window, and emits <see langword="true"/> when written.</returns>
    private IObservable<bool> Write(DocumentAttachment attachment, string path)
    {
        if (DocumentFeature<IAttachmentSource>.Get(_owner.TryGetDocument()) is not { } source)
        {
            return Signal.Return(false);
        }

        var index = attachment.Index;
        return Signal.Start(() => WriteFile(source, index, path), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Do(written => Read(attachment, written))
            .Catch<bool, Exception>(error => Failed(attachment, error));
    }

    /// <summary>Says the file could not be read from the document when it was not written.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="written">Whether it was written.</param>
    private void Read(DocumentAttachment attachment, bool written)
    {
        if (!written)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Could not read {0} from the document.", attachment.Name));
        }
    }

    /// <summary>Says the file was saved, when it was.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="saved">Whether it was saved.</param>
    private void Saved(DocumentAttachment attachment, bool saved)
    {
        if (saved)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Saved {0}.", attachment.Name));
        }
    }

    /// <summary>Says why the file was not written, or passes on a failure that is not expected.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="error">The failure.</param>
    /// <returns>A signal giving <see langword="false"/> for an expected failure, or failing with the same error.</returns>
    private IObservable<bool> Failed(DocumentAttachment attachment, Exception error)
    {
        var expected = error is IOException or UnauthorizedAccessException or InvalidOperationException;
        if (expected)
        {
            _owner.SetNotice(error is InvalidOperationException
                ? LocalizedMessage.Format(UiText, "Could not save {0}: the document was closed.", attachment.Name)
                : LocalizedMessage.Format(UiText, "Could not save {0}: {1}", attachment.Name, error.Message));
        }

        return expected ? Signal.Return(false) : Signal.Throw<bool>(error);
    }

    /// <summary>Opens the selected file.</summary>
    /// <returns>A signal that ends when the file opened or was refused.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private IObservable<RxVoid> OpenSelected() =>
        Selected is { } attachment ? Open(attachment).Select(static _ => RxVoid.Default) : Signal.Return(RxVoid.Default);

    /// <summary>Asks which files to attach, then adds each to the document. The list shows them once they are in.</summary>
    /// <returns>A signal that ends once the files are attached or the choice is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> AttachFiles() =>
        DocumentFeature<IAttachmentEditor>.Get(_owner.TryGetDocument()) is not { } editor
            ? Signal.Return(RxVoid.Default)
            : AttachInteraction.Handle(RxVoid.Default).ToSignal()
                .SwitchMap(paths => paths is { Length: > 0 } ? AttachAll(editor, paths) : Signal.Return(RxVoid.Default));

    /// <summary>Asks whether to take the selected file out of the document, then takes it out.</summary>
    /// <returns>A signal that ends once the file is removed or the question is answered no.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private IObservable<RxVoid> RemoveSelected()
    {
        if (Selected is not { } attachment || DocumentFeature<IAttachmentEditor>.Get(_owner.TryGetDocument()) is not { } editor)
        {
            return Signal.Return(RxVoid.Default);
        }

        var question = new ConfirmRequest(
            UiText.Format("Take {0} out of the document?", attachment.Name),
            UiText.Get("The file is no longer kept inside this PDF once you save. Save a copy of it first if you need it."),
            UiText.Get("Take It Out"));
        return RemoveInteraction.Handle(question).ToSignal()
            .SwitchMap(yes => !yes
                ? Signal.Return(RxVoid.Default)
                : editor.RemoveAttachment(attachment.Index)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(result => AttachmentsChanged(result, UiText.Format("Took {0} out of the document.", attachment.Name))));
    }

    /// <summary>Reads each file and attaches it, one after another, so the files keep the order they were chosen in.</summary>
    /// <param name="editor">The document's attachment editor.</param>
    /// <param name="paths">The files.</param>
    /// <returns>A signal that ends once every file is attached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> AttachAll(IAttachmentEditor editor, string[] paths) =>
        Signal.FromAsync(token => AttachAllAsync(editor, paths, token))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(added => AttachmentsChanged(new(added > 0, null), added == 1 ? UiText.Get("Attached 1 file.") : UiText.Format("Attached {0} files.", added)))
            .Catch<RxVoid, IOException>(error => Refused(error.Message))
            .Catch<RxVoid, UnauthorizedAccessException>(error => Refused(error.Message));

    /// <summary>Lists the files again after a change and says what happened.</summary>
    /// <param name="result">What the engine said.</param>
    /// <param name="done">What to say when it worked.</param>
    /// <returns>The completion value.</returns>
    private RxVoid AttachmentsChanged(DocumentEditResult result, string done)
    {
        if (result.Succeeded)
        {
            Refresh();
            _owner.Pages.Refresh();
        }

        _owner.SetNotice(LocalizedMessage.Literal(result.Succeeded ? done : result.Problem ?? UiText.Get("The attached files could not be changed.")));
        return RxVoid.Default;
    }

    /// <summary>Says a file could not be read to attach it.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>A signal that ends at once.</returns>
    private IObservable<RxVoid> Refused(string reason)
    {
        _owner.SetNotice(LocalizedMessage.Format(UiText, "Could not attach the file: {0}", reason));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Asks where to save the selected file, then saves it.</summary>
    /// <returns>A signal that ends when the file is saved or the choice is given up.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private IObservable<RxVoid> Save() =>
        Selected is not { } attachment
            ? Signal.Return(RxVoid.Default)
            : SaveInteraction.Handle(Path.GetFileName(attachment.Name)).ToSignal()
                .SwitchMap(path => string.IsNullOrEmpty(path) ? Signal.Return(RxVoid.Default) : SaveTo(attachment, path).Select(static _ => RxVoid.Default));
}
