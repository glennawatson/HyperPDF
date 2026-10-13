// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Forms;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentFormHighlight over the document's owned state.</summary>
internal static class HyperPdfDocumentFormHighlight
{
    /// <summary>Gets Highlight.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormHighlight GetHighlight(HyperPdfDocument self) => HyperPdfFormRuntime.Unpack(Volatile.Read(ref self.Highlight));

    /// <summary>Sets Highlight.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="value">The value to use.</param>
    internal static void SetHighlight(HyperPdfDocument self, FormHighlight value)
    {
        // Renders hold the page read lock while they make or use the renderer, so with the write lock no renderer can be
        // made with the old tint and published after the reset below.
        using var access = HyperPdfNavigation.EnterPageWrite(self);
        if (self.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref self.Highlight, HyperPdfFormRuntime.Pack(value));

        // The renderer holds the tint it was made with, so the next page drawn uses a new one.
        HyperPdfRendering.ResetRenderer(self);
    }
}
