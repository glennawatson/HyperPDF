// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.App.ViewModels;

/// <summary>Links to other files: other PDFs open in a tab; other files open with the desktop's app once the reader agrees.</summary>
[DebuggerDisplay("TabFileLinks: {_owner.FileName}")]
public sealed class TabFileLinks : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="TabFileLinks"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    internal TabFileLinks(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets the question asked before a file opens in another app: the file's name in, <see langword="true"/> to open it.</summary>
    public Interaction<string, bool> ConfirmOpenFileInteraction { get; } = new();

    /// <summary>Gets the request to open a file the reader agreed to open with the desktop's app, as a full path.</summary>
    public Interaction<string, RxVoid> LaunchFileInteraction { get; } = new();

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
            _owner.Notice = $"The linked file {name} was not found. It may have been moved or not sent with this document.";
            return false;
        }

        if (LinkedFiles.IsPdf(fullPath))
        {
            _owner.OpenDocument?.Invoke(fullPath, pageIndex);
            return _owner.OpenDocument is not null;
        }

        if (LinkedFiles.IsRunnable(fullPath))
        {
            _owner.Notice = $"{name} is a program or script. HyperPDF never starts programs from documents.";
            return false;
        }

        if (!await ConfirmOpenFileInteraction.Handle(name).ToTask().ConfigureAwait(true))
        {
            return false;
        }

        _ = await LaunchFileInteraction.Handle(fullPath).ToTask().ConfigureAwait(true);
        return true;
    }

    /// <summary>Follows a link to another file, or to a document embedded in this one.</summary>
    /// <param name="target">The target.</param>
    internal void NavigateToFile(in LinkTarget target)
    {
        if (target.Kind == LinkTargetKind.EmbeddedDocument)
        {
            _owner.SidebarVisible = true;
            _owner.SidebarMode = SidebarMode.Attachments;
            _owner.Notice = "This link opens a document attached to this one. Choose it in Attachments, then Open Attachment.";
            return;
        }

        if (LinkedFiles.Resolve(_owner.FilePath, target.Uri ?? string.Empty) is not { } path)
        {
            _owner.Notice = "This link points to a file that cannot be opened from here.";
            return;
        }

        _ = OpenFileAsync(path, Math.Max(0, target.PageIndex));
    }
}
