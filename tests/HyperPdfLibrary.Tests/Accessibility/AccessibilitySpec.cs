// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Describes a one-page tagged PDF for the accessibility tests. Every default makes a document with no findings.</summary>
[DebuggerDisplay("AccessibilitySpec")]
internal sealed record AccessibilitySpec
{
    /// <summary>Gets the page content; the default draws three tagged lines and a figure.</summary>
    internal string Content { get; init; } = AccessibilityPdfs.Lines(AccessibilityPdfs.DefaultLines) + AccessibilityPdfs.FigureContent(AccessibilityPdfs.DefaultLines);

    /// <summary>Gets the catalog's language entry.</summary>
    internal string Lang { get; init; } = "/Lang (en-AU)";

    /// <summary>Gets the catalog's mark info entry.</summary>
    internal string MarkInfo { get; init; } = "/MarkInfo << /Marked true >>";

    /// <summary>Gets a value indicating whether the document has a structure tree.</summary>
    internal bool Tree { get; init; } = true;

    /// <summary>Gets the XMP packet, or <see langword="null"/> for none.</summary>
    internal string? Xmp { get; init; } = AccessibilityPdfs.Packet(1, true);

    /// <summary>Gets the catalog's viewer preferences entry.</summary>
    internal string ViewerPreferences { get; init; } = "/ViewerPreferences << /DisplayDocTitle true >>";

    /// <summary>Gets extra catalog entries.</summary>
    internal string CatalogExtra { get; init; } = string.Empty;

    /// <summary>Gets extra structure tree root entries, such as <c>/RoleMap</c>.</summary>
    internal string RootExtra { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the document element is in the PDF 2.0 structure namespace, which the tree root lists.</summary>
    internal bool Pdf20Root { get; init; }

    /// <summary>Gets extra page entries.</summary>
    internal string PageExtra { get; init; } = string.Empty;

    /// <summary>Gets the function that writes the test font <c>/F1</c>'s body, given the builder; <see langword="null"/> for a Type 3 font that maps every character to Unicode.</summary>
    internal Func<TaggedPdfBuilder, string>? Font { get; init; }

    /// <summary>Gets the layout: adds elements and returns the document element's kids, given the builder and the document element's number.</summary>
    internal Func<TaggedPdfBuilder, int, string> Layout { get; init; } = AccessibilityPdfs.DefaultLayout;
}
