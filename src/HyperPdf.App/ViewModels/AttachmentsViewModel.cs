// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// A tab's embedded files. The attachments panel only appears for documents that have some; a file is saved where the
/// user picks, and never opened automatically, so nothing runs that the user did not choose to run.
/// </summary>
[DebuggerDisplay("AttachmentsViewModel: {Items.Count} attachments")]
public sealed partial class AttachmentsViewModel : ReactiveObject
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

    /// <summary>Describes a portfolio in words.</summary>
    /// <param name="portfolio">The portfolio.</param>
    /// <returns>The description.</returns>
    public static string DescribePortfolio(PortfolioSummary portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var view = portfolio.View.ToUpperInvariant() switch
        {
            "T" => "tiles",
            "H" => "hidden",
            _ => "details",
        };
        var files = portfolio.FileCount == 1 ? "1 file" : string.Create(CultureInfo.CurrentCulture, $"{portfolio.FileCount} files");
        var first = string.IsNullOrEmpty(portfolio.InitialFile) ? string.Empty : $" It opens at {portfolio.InitialFile}.";
        return $"PDF portfolio: {files}, {view} view.{first}";
    }

    /// <summary>Describes a file size in words, for example "12 KB".</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The description.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        1 => "1 byte",
        < (long)Kilobyte => string.Create(CultureInfo.CurrentCulture, $"{bytes} bytes"),
        < (long)Megabyte => string.Create(CultureInfo.CurrentCulture, $"{bytes / Kilobyte:0} KB"),
        _ => string.Create(CultureInfo.CurrentCulture, $"{bytes / Megabyte:0.0} MB"),
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
    public async Task<bool> SaveToAsync(DocumentAttachment attachment, string path)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var saved = await WriteAsync(attachment, path).ConfigureAwait(true);
        if (saved)
        {
            _owner.Notice = $"Saved {attachment.Name}.";
        }

        return saved;
    }

    /// <summary>Opens an embedded file: a PDF in a new tab, anything else with another app after asking.</summary>
    /// <param name="attachment">The file.</param>
    /// <returns><see langword="true"/> when it opened.</returns>
    public async Task<bool> OpenAsync(DocumentAttachment attachment)
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
            _owner.Notice = $"Could not open {attachment.Name}: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.Notice = $"Could not open {attachment.Name}: {ex.Message}";
            return false;
        }

        var path = Path.Combine(folder, name.Length > 0 ? name : UnnamedFile);
        return await WriteAsync(attachment, path).ConfigureAwait(true) && await _owner.Links.OpenFileAsync(path, 0).ConfigureAwait(true);
    }

    /// <summary>Gets the place a file takes in the list: the portfolio's order, then the files it does not describe in their own order.</summary>
    /// <param name="attachment">The file.</param>
    /// <returns>The rank, which is the portfolio order when the file has one and a larger number otherwise.</returns>
    private static long PortfolioRank(DocumentAttachment attachment) =>
        attachment.PortfolioOrder >= 0 ? attachment.PortfolioOrder : (long)int.MaxValue + attachment.Index;

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
    /// <returns><see langword="true"/> when written.</returns>
    private async Task<bool> WriteAsync(DocumentAttachment attachment, string path)
    {
        if (DocumentFeature<IAttachmentSource>.Get(_owner.TryGetDocument()) is not
            {
            } source)
        {
            return false;
        }

        try
        {
            // Extracting and writing the file is synchronous, so it runs on the pool and a large attachment does not freeze the window.
            var index = attachment.Index;
            var written = await Task.Run(() => WriteFile(source, index, path), _owner.LifetimeToken).ConfigureAwait(true);

            if (!written)
            {
                _owner.Notice = $"Could not read {attachment.Name} from the document.";
            }

            return written;
        }
        catch (IOException ex)
        {
            _owner.Notice = $"Could not save {attachment.Name}: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.Notice = $"Could not save {attachment.Name}: {ex.Message}";
            return false;
        }
        catch (InvalidOperationException)
        {
            _owner.Notice = $"Could not save {attachment.Name}: the document was closed.";
            return false;
        }
    }

    /// <summary>Opens the selected file.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private async Task OpenSelectedAsync()
    {
        if (Selected is { } attachment)
        {
            _ = await OpenAsync(attachment).ConfigureAwait(true);
        }
    }

    /// <summary>Asks where to save the selected file, then saves it.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private async Task SaveAsync()
    {
        if (Selected is not { } attachment)
        {
            return;
        }

        var path = await SaveInteraction.Handle(Path.GetFileName(attachment.Name)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            _ = await SaveToAsync(attachment, path).ConfigureAwait(true);
        }
    }
}
