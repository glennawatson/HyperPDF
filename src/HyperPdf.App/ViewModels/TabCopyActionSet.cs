// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The commands of a document tab that open a window to save a changed copy. Each one is off, with a menu text that says
/// why, when the open document's engine cannot make that copy.
/// </summary>
[DebuggerDisplay("TabCopyActionSet")]
public sealed class TabCopyActionSet : ReactiveObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TabCopyActionSet"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    internal TabCopyActionSet(DocumentTabViewModel owner)
    {
        Optimize = new(owner, TabCopyActions.Optimize);
        Archive = new(owner, TabCopyActions.Archive);
        Pdf20 = new(owner, TabCopyActions.Pdf20);
        Flatten = new(owner, TabCopyActions.Flatten);
        Protect = new(owner, TabCopyActions.Protect);
        Unprotect = new(owner, TabCopyActions.Unprotect);
        Redact = new(owner, TabCopyActions.Redact);
        Validation = new(owner, TabCopyActions.Validation);
        ExternalSign = new(owner, TabCopyActions.ExternalSign);
    }

    /// <summary>Gets saving an optimised copy, for engines that can write one.</summary>
    public TabCopyAction<OptimizeCopyViewModel> Optimize { get; }

    /// <summary>Gets saving a PDF/A copy, for engines that can write one.</summary>
    public TabCopyAction<ArchiveCopyViewModel> Archive { get; }

    /// <summary>Gets saving a PDF 2.0 copy, for engines that can write one.</summary>
    public TabCopyAction<Pdf20CopyViewModel> Pdf20 { get; }

    /// <summary>Gets saving a flattened copy, for engines that can write one.</summary>
    public TabCopyAction<FlattenCopyViewModel> Flatten { get; }

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
    public void Dispose()
    {
        Optimize.Dispose();
        Archive.Dispose();
        Pdf20.Dispose();
        Flatten.Dispose();
        Protect.Dispose();
        Unprotect.Dispose();
        Redact.Dispose();
        Validation.Dispose();
        ExternalSign.Dispose();
    }

    /// <summary>Stops the run in every open copy window and closes it, because the document they work on is going away.</summary>
    /// <returns><see langword="true"/> when a window was open.</returns>
    internal bool CloseWindows()
    {
        var closed = Optimize.CloseWindow();
        closed |= Archive.CloseWindow();
        closed |= Pdf20.CloseWindow();
        closed |= Flatten.CloseWindow();
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
        Optimize.Refresh(document);
        Archive.Refresh(document);
        Pdf20.Refresh(document);
        Flatten.Refresh(document);
        Protect.Refresh(document);
        Unprotect.Refresh(document);
        Redact.Refresh(document);
        Validation.Refresh(document);
        ExternalSign.Refresh(document);
    }
}
