// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Dialogs;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The commands of a document tab that open a window to save a changed copy. Each one is off, with a menu text that says
/// why, when the open document's engine cannot make that copy.
/// </summary>
[DebuggerDisplay("TabCopyActionSet")]
public sealed class TabCopyActionSet : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>Owns every copy action, and so the window each one has open.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>Initializes a new instance of the <see cref="TabCopyActionSet"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    internal TabCopyActionSet(DocumentTabViewModel owner)
    {
        SaveCopy = new(owner, TabCopyActions.SaveCopy);
        Optimize = new(owner, TabCopyActions.Optimize);
        Archive = new(owner, TabCopyActions.Archive);
        Pdf20 = new(owner, TabCopyActions.Pdf20);
        Flatten = new(owner, TabCopyActions.Flatten);
        ExportPages = new(owner, TabCopyActions.ExportPages);
        Protect = new(owner, TabCopyActions.Protect);
        Unprotect = new(owner, TabCopyActions.Unprotect);
        Redact = new(owner, TabCopyActions.Redact);
        Validation = new(owner, TabCopyActions.Validation);
        ExternalSign = new(owner, TabCopyActions.ExternalSign);
        _subscriptions = [SaveCopy, Optimize, Archive, Pdf20, Flatten, ExportPages, Protect, Unprotect, Redact, Validation, ExternalSign];
    }

    /// <summary>Gets saving a copy in a chosen format, for engines that can write copies.</summary>
    public TabCopyAction<SaveCopyViewModel> SaveCopy { get; }

    /// <summary>Gets saving an optimised copy, for engines that can write one.</summary>
    public TabCopyAction<OptimizeCopyViewModel> Optimize { get; }

    /// <summary>Gets saving a PDF/A copy, for engines that can write one.</summary>
    public TabCopyAction<ArchiveCopyViewModel> Archive { get; }

    /// <summary>Gets saving a PDF 2.0 copy, for engines that can write one.</summary>
    public TabCopyAction<Pdf20CopyViewModel> Pdf20 { get; }

    /// <summary>Gets the question asked before a save rewrites a signed or protected file as PDF 2.0, which costs it its signatures or its protection.</summary>
    public Interaction<ConfirmRequest, bool> Pdf20SaveInteraction { get; } = new();

    /// <summary>Gets saving a flattened copy, for engines that can write one.</summary>
    public TabCopyAction<FlattenCopyViewModel> Flatten { get; }

    /// <summary>Gets exporting pages as picture files or vector drawings, for engines that can write them.</summary>
    public TabCopyAction<ExportPagesViewModel> ExportPages { get; }

    /// <summary>Gets saving a copy with a password and permissions, for engines that can write one.</summary>
    public TabCopyAction<ProtectViewModel> Protect { get; }

    /// <summary>Gets saving a copy without protection, for protected documents.</summary>
    public TabCopyAction<RemoveProtectionViewModel> Unprotect { get; }

    /// <summary>Gets applying redactions, for engines that can remove marked content for good.</summary>
    public TabCopyAction<RedactionViewModel> Redact { get; }

    /// <summary>Gets adding validation data or a document timestamp, for engines that can sign.</summary>
    public TabCopyAction<ValidationDataViewModel> Validation { get; }

    /// <summary>Gets signing with an external signer, for engines that can sign.</summary>
    public TabCopyAction<ExternalSignViewModel> ExternalSign { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        LocalisationRefresh.RefreshCore(SaveCopy, visited);
        LocalisationRefresh.RefreshCore(Optimize, visited);
        LocalisationRefresh.RefreshCore(Archive, visited);
        LocalisationRefresh.RefreshCore(Pdf20, visited);
        LocalisationRefresh.RefreshCore(Flatten, visited);
        LocalisationRefresh.RefreshCore(ExportPages, visited);
        LocalisationRefresh.RefreshCore(Protect, visited);
        LocalisationRefresh.RefreshCore(Unprotect, visited);
        LocalisationRefresh.RefreshCore(Redact, visited);
        LocalisationRefresh.RefreshCore(Validation, visited);
        LocalisationRefresh.RefreshCore(ExternalSign, visited);
    }

    /// <summary>Stops the run in every open copy window and closes it, because the document they work on is going away.</summary>
    /// <returns><see langword="true"/> when a window was open.</returns>
    internal bool CloseWindows()
    {
        var closed = SaveCopy.CloseWindow();
        closed |= Optimize.CloseWindow();
        closed |= Archive.CloseWindow();
        closed |= Pdf20.CloseWindow();
        closed |= Flatten.CloseWindow();
        closed |= ExportPages.CloseWindow();
        closed |= Protect.CloseWindow();
        closed |= Unprotect.CloseWindow();
        closed |= Redact.CloseWindow();
        closed |= Validation.CloseWindow();
        closed |= ExternalSign.CloseWindow();
        return closed;
    }

    /// <summary>Works out which commands the open document's engine supports.</summary>
    /// <param name="document">The open document.</param>
    internal void Refresh(IDocument document)
    {
        SaveCopy.Refresh(document);
        Optimize.Refresh(document);
        Archive.Refresh(document);
        Pdf20.Refresh(document);
        Flatten.Refresh(document);
        ExportPages.Refresh(document);
        Protect.Refresh(document);
        Unprotect.Refresh(document);
        Redact.Refresh(document);
        Validation.Refresh(document);
        ExternalSign.Refresh(document);
    }
}
