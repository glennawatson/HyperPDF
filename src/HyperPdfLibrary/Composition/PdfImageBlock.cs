// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>An image on its own line. Without alternative text a tagged document treats the image as decoration.</summary>
/// <param name="Image">The image, added to the document with one of the creator's <c>Add</c> methods.</param>
[DebuggerDisplay("PdfImageBlock: {Image}")]
public sealed record PdfImageBlock(PdfCreatedImage Image) : PdfBlock
{
    /// <summary>Gets the width in points, or <see langword="null"/> to take it from the height or the image's own size.</summary>
    public float? Width { get; init; }

    /// <summary>Gets the height in points, or <see langword="null"/> to take it from the width or the image's own size.</summary>
    public float? Height { get; init; }

    /// <summary>Gets what the image shows, for people who cannot see it; <see langword="null"/> marks the image as decoration.</summary>
    public string? AlternativeText { get; init; }

    /// <summary>Gets where the image sits between the left and right edges.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Gets the space above the image in points.</summary>
    public float SpaceBefore { get; init; }

    /// <summary>Gets the space below the image in points.</summary>
    public float SpaceAfter { get; init; }
}
