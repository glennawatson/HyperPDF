// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Presentation;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdf.Engine;

/// <summary>Reads the transition and advance time a page asks for in a presentation (ISO 32000-2 §12.4.4).</summary>
internal static class HyperPdfDocumentTransitions
{
    /// <summary>The name of the vertical split and blinds dimension.</summary>
    private const string VerticalDimension = "V";

    /// <summary>The name of the inward motion.</summary>
    private const string InwardMotion = "I";

    /// <summary>Gets what a page asks for.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The transition and advance time, or <see langword="null"/> when the page asks for neither or a damaged entry cannot be read.</returns>
    internal static PageTransition? GetTransition(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)pageIndex >= (uint)self.Document.PageCount)
        {
            return null;
        }

        try
        {
            var page = PdfDocumentPages.GetPage(self.Document, pageIndex);
            var advance = PdfDocumentViewerPreferences.GetAdvanceAfter(page);
            if (PdfDocumentViewerPreferences.GetTransition(self.Document, page) is { } transition)
            {
                return Convert(transition);
            }

            // A page may ask to advance by itself without asking for an effect.
            return advance is null ? null : new(TransitionStyle.Replace, 0, false, false, 0, advance);
        }
        catch (PdfException)
        {
            // A damaged transition only means the page appears at once.
            return null;
        }
    }

    /// <summary>Converts the library's transition.</summary>
    /// <param name="transition">The library's transition.</param>
    /// <returns>The transition.</returns>
    internal static PageTransition Convert(PdfPageTransition transition) => new(
        StyleOf(transition.Style),
        transition.Duration,
        transition.Dimension == VerticalDimension,
        transition.Motion == InwardMotion,
        transition.Direction,
        transition.AdvanceAfter);

    /// <summary>Maps a style name to a style; an unknown name is a plain replacement, which the standard names the default.</summary>
    /// <param name="name">The style name.</param>
    /// <returns>The style.</returns>
    internal static TransitionStyle StyleOf(string name) => name switch
    {
        "Split" => TransitionStyle.Split,
        "Blinds" => TransitionStyle.Blinds,
        "Box" => TransitionStyle.Box,
        "Wipe" => TransitionStyle.Wipe,
        "Dissolve" => TransitionStyle.Dissolve,
        "Glitter" => TransitionStyle.Glitter,
        "Fly" => TransitionStyle.Fly,
        "Push" => TransitionStyle.Push,
        "Cover" => TransitionStyle.Cover,
        "Uncover" => TransitionStyle.Uncover,
        "Fade" => TransitionStyle.Fade,
        _ => TransitionStyle.Replace,
    };
}
