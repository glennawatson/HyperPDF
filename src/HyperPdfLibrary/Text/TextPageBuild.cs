// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Text;

/// <summary>Builds text pages with independently leased state for each extraction.</summary>
internal static class TextPageBuild
{
    /// <summary>Gets a cleaned state from the document, or makes one when another extraction is using it.</summary>
    /// <param name="document">The document that owns the reusable state.</param>
    /// <returns>The state exclusively owned by this extraction.</returns>
    internal static TextPageBuildState Rent(PdfDocument document) => Interlocked.Exchange(ref document.State.TextBuild, null) ?? new();

    /// <summary>Offers a cleaned state to its document for another extraction.</summary>
    /// <param name="document">The document that owns the reusable state.</param>
    /// <param name="state">The cleaned state.</param>
    internal static void Return(PdfDocument document, TextPageBuildState state)
    {
        if (document.IsDisposed)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref document.State.TextBuild, state, null) is null && document.IsDisposed)
        {
            _ = Interlocked.CompareExchange(ref document.State.TextBuild, null, state);
        }
    }

    /// <summary>Builds a text page from the collected runs, then clears the state.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="page">The page.</param>
    /// <param name="rightToLeft">Whether the document asks for right-to-left reading order.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage Build(TextPageBuildState state, PdfPage page, bool rightToLeft)
    {
        state.Display = page.ViewerTransform;
        state.PageWidth = page.Width;
        state.PageHeight = page.Height;
        state.RightToLeft = rightToLeft;
        try
        {
            TextRunCollection.ProcessRuns(state);
            return TextPageAssembly.CreatePage(state, page);
        }
        finally
        {
            TextPageAssembly.Clear(state);
        }
    }
}
