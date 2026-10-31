// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures;
using HyperPdf.ViewModels.Dialogs;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Output;

/// <summary>Decides how a save is rewritten as PDF 2.0, and asks first when that costs the file its signatures or its protection.</summary>
internal static class Pdf20SavePlan
{
    /// <summary>Works out how to save a document as PDF 2.0.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="hasChanges">Whether the document has unsaved changes; an unchanged document is not rewritten.</param>
    /// <param name="confirm">Asks the person to agree when signatures or protection must go.</param>
    /// <param name="cancellationToken">Cancels waiting for the answer.</param>
    /// <returns>The upgrade to make, or <see langword="null"/> to save in the file's own version.</returns>
    internal static async Task<Pdf20Upgrade?> PlanAsync(IDocument document, bool hasChanges, Interaction<ConfirmRequest, bool> confirm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(confirm);
        if (!hasChanges || DocumentFeature<IPdf20CopyWriter>.Get(document) is not { } writer)
        {
            return null;
        }

        var signed = HoldsSignature(DocumentFeature<ISignatureSource>.Get(document));
        var protectedFile = document.GetMetadata().IsEncrypted;
        if (!signed && !protectedFile)
        {
            return new(writer, default);
        }

        var request = new ConfirmRequest(UiText.Get("Save as PDF 2.0?"), Describe(signed, protectedFile), UiText.Get("Save as PDF 2.0"));
        var agreed = await confirm.Handle(request).ToTask(cancellationToken).ConfigureAwait(true);
        return agreed ? new(writer, new(signed, protectedFile)) : null;
    }

    /// <summary>Tells whether a document holds a signature that has been made, as opposed to an empty signature field.</summary>
    /// <param name="source">The signature source, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a signature has contents.</returns>
    private static bool HoldsSignature(ISignatureSource? source)
    {
        if (source is null || source.SignatureCount == 0)
        {
            return false;
        }

        var signatures = source.GetSignatures();
        for (var i = 0; i < signatures.Count; i++)
        {
            if (signatures[i].Contents.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Says what saving as PDF 2.0 will remove from the file.</summary>
    /// <param name="signed">Whether the file is signed.</param>
    /// <param name="protectedFile">Whether the file is protected with a password.</param>
    /// <returns>The sentence for the question.</returns>
    private static string Describe(bool signed, bool protectedFile)
    {
        var lost = signed && protectedFile
            ? UiText.Get("signatures and its password protection")
            : UiText.Get(signed ? "signatures" : "password protection");
        return UiText.Format("Saving your changes as PDF 2.0 removes this file's {0}. Choose Cancel to save in the file's own version instead.", lost);
    }
}
