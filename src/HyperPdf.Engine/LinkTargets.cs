// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Converts library actions and rectangles to the viewer's types.</summary>
internal static class LinkTargets
{
    /// <summary>Converts an action to a link target.</summary>
    /// <param name="document">The document, for page heights.</param>
    /// <param name="action">The action.</param>
    /// <returns>The target; <see cref="LinkTarget.None"/> for actions the viewer does not follow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LinkTarget From(PdfDocument document, PdfAction action) => From(document, action, -1);

    /// <summary>Converts an action to a link target.</summary>
    /// <param name="document">The document, for page heights.</param>
    /// <param name="action">The action.</param>
    /// <param name="sourcePage">The page holding the link, which the relative named actions move from; -1 when there is none.</param>
    /// <returns>The target; <see cref="LinkTarget.None"/> for actions the viewer does not follow.</returns>
    internal static LinkTarget From(PdfDocument document, PdfAction action, int sourcePage) => action.Value switch
    {
        GoToAction goTo => ForDestination(document, goTo.Destination),
        UriAction uri => LinkTarget.ForUri(uri.Uri),
        RemoteGoToAction remote => LinkTarget.ForFile(LinkTargetKind.OtherDocument, remote.File, remote.PageIndex),
        LaunchAction launch => LinkTarget.ForFile(LinkTargetKind.LaunchFile, launch.File, 0),
        EmbeddedGoToAction => new(LinkTargetKind.EmbeddedDocument, -1, null, null),
        NamedAction named => ForNamedAction(document, named.Name, sourcePage),
        _ => LinkTarget.None,
    };

    /// <summary>Converts a rectangle in physical viewer space, as <see cref="PdfPage.ToPhysicalRectangle"/> returns it, to a page rectangle.</summary>
    /// <param name="viewer">The rectangle; its "bottom" is the smaller, top, edge in viewer space.</param>
    /// <returns>The page rectangle.</returns>
    internal static PageRect ToPageRect(PdfRectangle viewer) => new(viewer.Left, viewer.Bottom, viewer.Width, viewer.Height);

    /// <summary>
    /// Converts a destination, approximating the location from the destination page's height,
    /// so the reader lands at the same place on every page size.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>The target.</returns>
    private static LinkTarget ForDestination(PdfDocument document, PdfDestination destination)
    {
        if (destination.Left is null && destination.Top is null)
        {
            return LinkTarget.ForPage(destination.PageIndex);
        }

        // An unspecified coordinate means the crop box edge; the page's transform applies the crop box and rotation.
        var page = PdfDocumentPages.GetPage(document, destination.PageIndex);
        var point = page.ToPhysical(new(destination.Left ?? page.CropBox.Left, destination.Top ?? page.CropBox.Top));
        return LinkTarget.ForPage(destination.PageIndex, new PagePoint(point.X, point.Y));
    }

    /// <summary>Converts a named action to a page target. Next and previous page move from the source page, so they need one.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The action's name.</param>
    /// <param name="sourcePage">The page holding the link, or -1.</param>
    /// <returns>The target, or none for other names and for relative moves without a source page.</returns>
    private static LinkTarget ForNamedAction(PdfDocument document, string name, int sourcePage)
    {
        var last = document.PageCount - 1;
        var relative = sourcePage >= 0 && sourcePage <= last;
        return name switch
        {
            "FirstPage" when last >= 0 => LinkTarget.ForPage(0),
            "LastPage" when last >= 0 => LinkTarget.ForPage(last),
            "NextPage" when relative => LinkTarget.ForPage(Math.Min(sourcePage + 1, last)),
            "PrevPage" when relative => LinkTarget.ForPage(Math.Max(sourcePage - 1, 0)),
            _ => LinkTarget.None,
        };
    }
}
