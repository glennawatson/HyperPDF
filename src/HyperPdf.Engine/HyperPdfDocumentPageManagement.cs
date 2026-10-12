// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentPageManagement over the document's owned state.</summary>
internal static class HyperPdfDocumentPageManagement
{
    /// <summary>Gets PageManager.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static IPageManager GetPageManager(HyperPdfDocument self)
    {
        lock (self.EditGate)
        {
            ObjectDisposedException.ThrowIf(self.IsDisposed, self);
            return self.PageManager ??= new(self, self.EditGate);
        }
    }
}
