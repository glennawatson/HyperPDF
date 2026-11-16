// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Signing;

/// <summary>Chooses a certificate file for a document tab.</summary>
internal static class DocumentCertificateBrowsing
{
    /// <summary>Copies the selected local file path into the tab.</summary>
    /// <param name="tab">The tab requesting a file.</param>
    /// <returns>A cold signal that ends once the picker is closed; unsubscribing, because the tab was deselected, gives it up.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<RxVoid> Browse(DocumentTabViewModel tab) =>
        tab.CertificateBrowseInteraction.Handle(RxVoid.Default).ToSignal().Select(path => Choose(tab, path));

    /// <summary>Puts the chosen file in the tab.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="path">The file, or empty when the choice was given up.</param>
    /// <returns>The completion value.</returns>
    private static RxVoid Choose(DocumentTabViewModel tab, string? path)
    {
        if (path is { Length: > 0 })
        {
            tab.CertificatePathEntry = path;
        }

        return RxVoid.Default;
    }
}
