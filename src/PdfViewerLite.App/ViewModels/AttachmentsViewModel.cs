// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

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
    private const string TempFolderName = "pdfviewerlite-attachments";

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

    /// <summary>Gets the interaction asking where to save a file; the input is the suggested name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

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
        if (DocumentFeature<IAttachmentSource>.Get(_owner.TryGetDocument()) is
            {
            } source)
        {
            foreach (var attachment in source.GetAttachments())
            {
                Items.Add(attachment);
            }
        }

        HasAttachments = Items.Count > 0;
        if (!HasAttachments && _owner.IsAttachmentsMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>Saves an embedded file to a path.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="path">Where to save it.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public bool Save(DocumentAttachment attachment, string path)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var saved = Write(attachment, path);
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
        return Write(attachment, path) && await _owner.OpenFileAsync(path, 0).ConfigureAwait(true);
    }

    /// <summary>Writes an embedded file to a path, saying what went wrong when it cannot.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="path">Where to write it.</param>
    /// <returns><see langword="true"/> when written.</returns>
    private bool Write(DocumentAttachment attachment, string path)
    {
        if (DocumentFeature<IAttachmentSource>.Get(_owner.TryGetDocument()) is not
            {
            } source)
        {
            return false;
        }

        try
        {
            bool written;
            using (var stream = File.Create(path))
            {
                written = source.SaveAttachment(attachment.Index, stream);
            }

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
            _ = Save(attachment, path);
        }
    }
}
