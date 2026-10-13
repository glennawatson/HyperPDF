// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>How the content of pages is transformed.</summary>
[DebuggerDisplay("PdfContentTransformOptions: clip {Clip}, annotations {TransformAnnotations}")]
public sealed record PdfContentTransformOptions
{
    /// <summary>Gets the options for a transform without a clip that moves the annotations with the content.</summary>
    public static PdfContentTransformOptions Default { get; } = new();

    /// <summary>
    /// Gets the rectangle the transformed content is clipped to, in the page's user space after the transform, or
    /// <see langword="null"/> for no clip. The clip hides page content only: annotations are not clipped and the page's
    /// boxes are not changed.
    /// </summary>
    public PdfRectangle? Clip { get; init; }

    /// <summary>Gets whether the annotations move with the content: their rectangles, point lists and appearances follow the matrix.</summary>
    public bool TransformAnnotations { get; init; } = true;
}
