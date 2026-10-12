// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

namespace HyperPdf.App.ViewModels;

/// <summary>Chooses a certificate file for a document tab.</summary>
internal static class DocumentCertificateBrowsing
{
    /// <summary>Copies the selected local file path into the tab.</summary>
    /// <param name="tab">The tab requesting a file.</param>
    /// <returns>A task for the file picker.</returns>
    internal static async Task BrowseAsync(DocumentTabViewModel tab)
    {
        var token = tab.SelectedWorkToken;
        try
        {
            if (await tab.CertificateBrowseInteraction.Handle(RxVoid.Default).ToTask(token).ConfigureAwait(true) is { Length: > 0 } path)
            {
                tab.CertificatePathEntry = path;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
