// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Links to other files: other PDFs open in a tab; other files open with the desktop's app once the reader agrees.</summary>
[DebuggerDisplay("TabFileLinks: {_owner.FileName}")]
public sealed class TabFileLinks : ReactiveObject, ILocalisationChildren, IDisposable
{
    /// <summary>The links being followed, ended when the tab closes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

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
    /// <returns>A cold signal that emits <see langword="true"/> when the file opened or was handed to the desktop, then completes.</returns>
    public IObservable<bool> OpenFile(string fullPath, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        var name = Path.GetFileName(fullPath);
        if (!File.Exists(fullPath))
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "The linked file {0} was not found. It may have been moved or not sent with this document.", name));
            return Signal.Return(false);
        }

        if (LinkedFiles.IsPdf(fullPath))
        {
            _owner.OpenDocument?.Invoke(fullPath, pageIndex);
            return Signal.Return(_owner.OpenDocument is not null);
        }

        if (LinkedFiles.IsRunnable(fullPath))
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "{0} is a program or script. HyperPDF never starts programs from documents.", name));
            return Signal.Return(false);
        }

        return ConfirmOpenFileInteraction.Handle(name).ToSignal()
            .SwitchMap(confirmed => confirmed ? LaunchFileInteraction.Handle(fullPath).ToSignal().Select(static _ => true) : Signal.Return(false));
    }

    /// <summary>Ends the links being followed.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => LocalisationRefresh.RefreshCore(_owner, visited);

    /// <summary>Follows a link to another file, or to a document embedded in this one.</summary>
    /// <param name="target">The target.</param>
    internal void NavigateToFile(in LinkTarget target)
    {
        if (target.Kind == LinkTargetKind.EmbeddedDocument)
        {
            _owner.SidebarVisible = true;
            _owner.SidebarMode = SidebarMode.Attachments;
            _owner.SetNotice(LocalizedMessage.Get(UiText, "This link opens a document attached to this one. Choose it in Attachments, then Open Attachment."));
            return;
        }

        if (LinkedFiles.Resolve(_owner.FilePath, target.Uri ?? string.Empty) is not { } path)
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "This link points to a file that cannot be opened from here."));
            return;
        }

        _subscriptions.Add(OpenFile(path, Math.Max(0, target.PageIndex)).SubscribeSafe(static _ => { }, ErrorTrace.Write));
    }
}
