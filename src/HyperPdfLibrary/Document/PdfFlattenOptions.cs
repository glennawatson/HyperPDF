// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>What flattening draws into the page content and what else it changes.</summary>
[DebuggerDisplay("PdfFlattenOptions: {Mode}, {Targets}, rotation baked {BakeRotation}")]
public sealed record PdfFlattenOptions
{
    /// <summary>Gets the options for a flatten of every annotation as a viewer shows it on screen.</summary>
    public static PdfFlattenOptions Default { get; } = new();

    /// <summary>Gets which flags decide what is drawn.</summary>
    public PdfFlattenMode Mode { get; init; } = PdfFlattenMode.Display;

    /// <summary>Gets which kinds of annotation are flattened.</summary>
    public PdfFlattenTargets Targets { get; init; } = PdfFlattenTargets.All;

    /// <summary>
    /// Gets whether the page's <c>/Rotate</c> is turned into the content: the page ends upright, with no rotation, and
    /// the annotations that stay move with it (ISO 32000-2 8.3.4, 8.3.3). Destinations that name a position on the page
    /// are not changed.
    /// </summary>
    public bool BakeRotation { get; init; }

    /// <summary>
    /// Gets whether text, choice and push button fields are drawn again from their values when the form asks readers to
    /// (<c>/NeedAppearances</c>, ISO 32000-2 12.7.3.3), even if they already have an appearance. A field without any
    /// appearance is always drawn.
    /// </summary>
    public bool RedrawFields { get; init; }
}
