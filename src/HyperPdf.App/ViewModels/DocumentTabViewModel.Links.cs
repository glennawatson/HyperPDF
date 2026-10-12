// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.ViewModels;

/// <content>Links to other files: other PDFs open in a tab; other files open with the desktop's app once the reader agrees.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Raises files the reader agreed to open with the desktop's app.</summary>
    private readonly Signal<string> _fileLaunchRequests = new();

    /// <summary>Gets how another PDF is opened in a tab, at a zero-based page; set by the window.</summary>
    public Action<string, int>? OpenDocument { get; init; }

    /// <summary>Gets the question asked before a file opens in another app: the file's name in, <see langword="true"/> to open it.</summary>
    public Interaction<string, bool> ConfirmOpenFileInteraction { get; } = new();

    /// <summary>Gets the files to open with the desktop's app, as full paths.</summary>
    public IObservable<string> FileLaunchRequests => _fileLaunchRequests;

    /// <summary>Opens a file a link or attachment points to: a PDF in a tab, anything else with another app after asking.</summary>
    /// <param name="fullPath">The file.</param>
    /// <param name="pageIndex">The zero-based page, for a PDF.</param>
    /// <returns><see langword="true"/> when the file opened or was handed to the desktop.</returns>
    public async Task<bool> OpenFileAsync(string fullPath, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        var name = Path.GetFileName(fullPath);
        if (!File.Exists(fullPath))
        {
            Notice = $"The linked file {name} was not found. It may have been moved or not sent with this document.";
            return false;
        }

        if (LinkedFiles.IsPdf(fullPath))
        {
            OpenDocument?.Invoke(fullPath, pageIndex);
            return OpenDocument is not null;
        }

        if (LinkedFiles.IsRunnable(fullPath))
        {
            Notice = $"{name} is a program or script. HyperPDF never starts programs from documents.";
            return false;
        }

        if (!await ConfirmOpenFileInteraction.Handle(name).ToTask().ConfigureAwait(true))
        {
            return false;
        }

        _fileLaunchRequests.OnNext(fullPath);
        return true;
    }

    /// <summary>Follows a link to another file, or to a document embedded in this one.</summary>
    /// <param name="target">The target.</param>
    private void NavigateToFile(in LinkTarget target)
    {
        if (target.Kind == LinkTargetKind.EmbeddedDocument)
        {
            SidebarVisible = true;
            SidebarMode = SidebarMode.Attachments;
            Notice = "This link opens a document attached to this one. Choose it in Attachments, then Open Attachment.";
            return;
        }

        if (LinkedFiles.Resolve(FilePath, target.Uri ?? string.Empty) is not { } path)
        {
            Notice = "This link points to a file that cannot be opened from here.";
            return;
        }

        _ = OpenFileAsync(path, Math.Max(0, target.PageIndex));
    }
}
